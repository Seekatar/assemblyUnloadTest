

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using SerilogTimings;
using static System.Console;

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
    private WeakReference _newContext = new(new CollectableAssemblyLoadContext());

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
        AssemblyLoadContext? context = _newContext.Target as AssemblyLoadContext;
        if (context == null)
        {
            context = new AssemblyLoadContext($"NewContext{_loadCount++}", isCollectible: true);
            // context.Unloading += unloading;
            _newContext = new WeakReference(context);
            _logger.LogInformation("Created new context");
        }

        _logger.LogInformation($"Loading {name} into context.");
        _logger.LogInformation($"Context currently has {context.Assemblies.Count()} assemblies");

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
            return context?.LoadFromStream(s, pdbStream);
        }
        finally
        {
            fs?.Close();
            fs?.Dispose();
        }
    }

    // this works from example
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExecuteAndUnload(bool unload, string assemblyPath, out WeakReference alcWeakRef)
    {
        // Create the unloadable HostAssemblyLoadContext
        var alc = new CollectableAssemblyLoadContext();

        // Create a weak reference to the AssemblyLoadContext that will allow us to detect
        // when the unload completes.
        alcWeakRef = new WeakReference(alc);

        // Load the plugin assembly into the HostAssemblyLoadContext.
        // NOTE: the assemblyPath must be an absolute path.
        Assembly assembly = alc.LoadFromAssemblyPath(assemblyPath);

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(ITest)));

        var t = types.Select(o => Activator.CreateInstance(o) as ITest ?? throw new Exception("ow!"));
        t.First().Message("Hi");

        if (unload)
            alc.Unload();
    }

    // modified from example since returns ITest, doesn't unload
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITest Get(string assemblyPath, out WeakReference alcWeakRef)
    {
        // Create the unloadable HostAssemblyLoadContext
        var alc = new CollectableAssemblyLoadContext();

        // Create a weak reference to the AssemblyLoadContext that will allow us to detect
        // when the unload completes.
        alcWeakRef = new WeakReference(alc);

        // Load the plugin assembly into the HostAssemblyLoadContext.
        // NOTE: the assemblyPath must be an absolute path.
        Assembly assembly = alc.LoadFromAssemblyPath(assemblyPath);

        alc = null;

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(ITest)));

        return types.Select(o => Activator.CreateInstance(o) as ITest ?? throw new Exception("ow!")).First();
    }

    // modified from example since returns ITest, doesn't unload
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GetAndCall(string assemblyPath, out WeakReference alcWeakRef)
    {
        var t = Get(assemblyPath, out alcWeakRef);
        WriteLine(t.Message("From GetAndCall"));

    }


    [MethodImpl(MethodImplOptions.NoInlining)]
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unload()
    {
        AssemblyLoadContext? context = _newContext.Target as AssemblyLoadContext;
        if (context != null) {
            _logger.LogInformation("Unloading context");
            context.Unload();
        }
   }

   public bool IsUnloaded() {
        // Poll and run GC until the AssemblyLoadContext is unloaded.
        // You don't need to do that unless you want to know when the context
        // got unloaded. You can just leave it to the regular GC.
        for (int i = 0; _newContext.IsAlive && (i < 10); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }

        return !_newContext.IsAlive;
   }

}