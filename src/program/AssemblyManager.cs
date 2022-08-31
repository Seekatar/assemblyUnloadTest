

using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using SerilogTimings;

namespace AssemblyContextTest;

public class AssemblyManager
{
    private class CollectableAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectableAssemblyLoadContext() : base(isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName name)
        {
            /* Per https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
             * The Load method returns null. That means that all the dependency assemblies
             * are loaded into the default context, and the new context contains only the
             * assemblies explicitly loaded into it.
             */
            return null;
        }
    }

    private readonly ILogger<AssemblyManager> _logger;
    private AssemblyLoadContext? _newContext = null;

    private List<Assembly> _assemblies = new();
    private List<PortableExecutableReference> _references = new();
    private int _loadCount = 1;
    public AssemblyManager(ILogger<AssemblyManager> logger)
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var trustedAssemblyPaths = trustedAssemblies.Split(Path.PathSeparator);
        _references = trustedAssemblyPaths.Select(path => MetadataReference.CreateFromFile(path)).ToList();
        _logger = logger;
    }

    private void unloading(System.Runtime.Loader.AssemblyLoadContext context)
    {
        _logger.LogInformation($"Context {context.Name} unloading!");
    }

    public IEnumerable<T>? BuildAndGet<T>(string name, string code) where T : class
    {
        var (p, pdb) = BuildAssembly(name, code);
        if (p != null)
        {
            var assembly = LoadAssembly(name, p);

            // var assembly = LoadAssembly(name, p, pdb); // pdb doesn't help much w/o a source file

            return GetImplementationsOf<T>(assembly);
        }
        return null;
    }

    public Assembly? LoadAssembly(string name, Stream? s = null, Stream? pdbStream = null)
    {
        if (_newContext is null)
        {
            _newContext = new AssemblyLoadContext($"NewContext{_loadCount++}", isCollectible: true);
            _newContext.Unloading += unloading;
            _logger.LogInformation("Created new context");
        }

        _logger.LogInformation($"Loading {name} into context.");
        _logger.LogInformation($"Context currently has {_newContext.Assemblies.Count()} assemblies");

        // var assembly = Assembly.LoadFrom(fname); load into default context
        // var assembly = newContext?.LoadFromAssemblyPath(fname); // locks assembly, even unload doesn't unlock it
        FileStream? fs = null;
        if (s == null)
        {
            fs = new FileStream(name, FileMode.Open, FileAccess.Read); // can delete the file if use stream
            s = fs;
        }

        try
        {
            var assembly = _newContext?.LoadFromStream(s, pdbStream);

            if (assembly is not null)
            {
                _assemblies.Add(assembly);

                // it will be our context, this would show that
                // AssemblyLoadContext.GetLoadContext(assembly);

                return assembly;
            }
        }
        finally
        {
            fs?.Close();
            fs?.Dispose();
        }
        return null;
    }

    public IEnumerable<T>? GetImplementationsOf<T>(Assembly? assembly) where T : class
    {
        if (assembly is null) return null;

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(T)));
        _logger.LogInformation($"Found {types.Count()} {typeof(T).Name}");
        return types.Select(o => Activator.CreateInstance(o) as T ?? throw new Exception("ow!"));
    }

    private bool CheckForErrors(List<Diagnostic> diag, string? code = null)
    {
        if (!diag.Any(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) return true;

        _logger.LogInformation($"Compiler errors");
        foreach (var e in diag.Where(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
        {
            _logger.LogInformation($"   {e.ToString()}");
            if (code != null) {
                var index = e.Location.SourceSpan.Start;
                var end = code.IndexOf(Environment.NewLine, index);
                var begin = code.LastIndexOf(Environment.NewLine, index);

                begin = begin < 0 ? 0 : begin + Environment.NewLine.Length;
                end = end < 0 ? code.Length : end;

                _logger.LogInformation(code[begin..end]);
            }
        }
        return false;
    }

    public (Stream? pe, Stream? pdb) BuildAssembly(string name, string code)
    {
        using (Operation.Time("Building assembly"))
        {

            // largely from http://www.albahari.com/nutshell/cs10ian-supplement.pdf p54
            SyntaxTree tree;
            using (Operation.Time("Parsing text"))
            {
                tree = CSharpSyntaxTree.ParseText(code);
            }

            CSharpCompilation compilation;
            using (Operation.Time("Compiling code"))
            {

                compilation = CSharpCompilation
                    .Create($"{name}-{_loadCount}A")
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, moduleName: $"{name}-{_loadCount}"))
                    .AddSyntaxTrees(tree)
                    .AddReferences(_references);

                var diag = compilation.GetDiagnostics();
                if (!CheckForErrors(diag.ToList(), code)) return (null, null);
            }

            using (Operation.Time("Emitting"))
            {
                var peStream = new MemoryStream();
                var pdbStream = new MemoryStream();

                var diag = compilation.Emit(peStream, pdbStream);
                if (!CheckForErrors(diag.Diagnostics.ToList())) return (null, null);

                peStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);
                return (peStream, pdbStream);
            }
        }
    }

    public void Unload()
    {
        _assemblies.Clear();
        _newContext = null;
    }

}