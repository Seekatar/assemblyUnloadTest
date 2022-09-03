

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using SerilogTimings;
using static System.Console;

namespace AssemblyContextTest;

public class AssemblyManager<T>
{
    private class CollectableAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectableAssemblyLoadContext() : base(isCollectible: true)
        {
        }

        public CollectableAssemblyLoadContext(string name) : base(name, isCollectible: true)
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

    private readonly ILogger<AssemblyManager<T>> _logger;
    private CollectableAssemblyLoadContext _context = new();
    private WeakReference _deadContext = new(null);
    private List<PortableExecutableReference> _references = new();

    private int _loadCount = 1;
    private List<Assembly> _assemblies = new();

    public AssemblyManager(ILogger<AssemblyManager<T>> logger)
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

    [MethodImpl(MethodImplOptions.NoInlining)]
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public Assembly? LoadAssembly(string name, Stream? s = null, Stream? pdbStream = null)
    {
        if (_context == null)
        {
            _context = new CollectableAssemblyLoadContext($"NewContext{_loadCount++}");
            _logger.LogInformation("Created new context");
        }

        _logger.LogInformation($"Loading {name} into context.");
        _logger.LogInformation($"Context currently has {_context.Assemblies.Count()} assemblies");

        FileStream? fs = null;
        if (s == null)
        {
            fs = new FileStream(name, FileMode.Open, FileAccess.Read); // can delete the file if use stream
            s = fs;
        }

        try
        {
            return _context.LoadFromStream(s, pdbStream);
        }
        finally
        {
            fs?.Close();
            fs?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Load(string assemblyPath)
    {
        Assembly assembly = _context.LoadFromAssemblyPath(assemblyPath);

        if (assembly is null) return false;

        _assemblies.Add(assembly);

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public TObj? Get<TObj>(string assemblyPath) where TObj : class
    {
        var type = _assemblies.FirstOrDefault()?.GetTypes().Where(o => o.IsAssignableTo(typeof(TObj)))?.FirstOrDefault();
        if (type == null) return null;
        
        return Activator.CreateInstance(type) as TObj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public TObj? LoadAndGet<TObj>(string assemblyPath) where TObj : class
    {
        Assembly assembly = _context.LoadFromAssemblyPath(assemblyPath);

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(TObj)));

        return types.Select(o => Activator.CreateInstance(o) as TObj ?? throw new Exception("ow!")).First();
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    public IEnumerable<TObj>? GetImplementationsOf<TObj>(Assembly? assembly) where TObj : class
    {
        if (assembly is null) return null;

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(TObj)));
        _logger.LogInformation($"Found {types.Count()} {typeof(TObj).Name}");
        return types.Select(o => Activator.CreateInstance(o) as TObj ?? throw new Exception("ow!"));
    }

    private bool CheckForErrors(List<Diagnostic> diag, string? code = null)
    {
        if (!diag.Any(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) return true;

        _logger.LogInformation($"Compiler errors");
        foreach (var e in diag.Where(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
        {
            _logger.LogInformation($"   {e.ToString()}");
            if (code != null)
            {
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unload()
    {
        _assemblies = new();
        if (_context != null)
        {
            _deadContext = new WeakReference(_context);
            _logger.LogInformation($"Unloading context {_context.Name}");
            _context = new(); // allow GC to collect existing one
            _context.Unload();
        }
    }

    public bool IsUnloaded()
    {
        return !_deadContext.IsAlive;
    }

}