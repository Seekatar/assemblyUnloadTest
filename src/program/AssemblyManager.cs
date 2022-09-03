

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Seekatar.Tools;

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
        public Dictionary<string, Assembly> LoadedAssemblies { get; set; } = new();
    }

    private readonly ILogger _logger;
    private const string FirstContextName = "__FirstContext__";
    private Dictionary<string, CollectableAssemblyLoadContext> _contexts = new() { { FirstContextName, new CollectableAssemblyLoadContext(FirstContextName) } };
    private WeakReference _deadContext = new(null);
    CollectableAssemblyLoadContext _x = new();
    private Dictionary<string, Assembly> LoadedAssemblies = new();

    private int _loadCount = 1;

    public AssemblyManager(ILogger logger)
    {
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool LoadFromAssemblyPath(string name, string fileName, string contextName = FirstContextName)
    {
        // ok
        var context = CheckContext(contextName);

        _logger.LogInformation("Loading {assemblyName} into context.", name);
        _logger.LogInformation("Context {contextName} currently has {assemblyCount} assemblies", contextName, context.Assemblies.Count());

        var ret = context.LoadFromAssemblyPath(fileName);
        // ok LoadedAssemblies.Add(name, ret);
        // ok _x.LoadedAssemblies.Add(name, ret);
        context.LoadedAssemblies.Add(name, ret);
        return ret != null;
    }

    private CollectableAssemblyLoadContext CheckContext(string contextName)
    {
        if (!_contexts.TryGetValue(contextName, out var context))
        {
            context = new CollectableAssemblyLoadContext(contextName);
            _contexts.Add(contextName, context);
        }
        return context;
    }

    public Assembly? LoadFromStream(string name, Stream s, Stream? pdbStream = null, string contextName = FirstContextName)
    {
        var context = CheckContext(contextName);
        _logger.LogInformation("Loading {assemblyName} into context {contextName}.", name, contextName);
        _logger.LogInformation("Context currently has {assemblyCount} assemblies", context.Assemblies.Count());

        var ret = context.LoadFromStream(s, pdbStream);
        //context.LoadedAssemblies.Add(name, ret);
        return ret;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public TObj? CreateInstance<TObj>(string name, params object?[]? args) where TObj : class
    {
        var assembly = _contexts.Values.SelectMany(o => o.LoadedAssemblies.Where(o => o.Key == name).Select(o => o.Value)).FirstOrDefault();
        // ok var assembly = _x.LoadedAssemblies.Where(o => o.Key == name).Select(o => o.Value).FirstOrDefault();
        // ok var assembly = LoadedAssemblies.Where(o => o.Key == name).Select( o => o.Value).FirstOrDefault();
        if (assembly == null) return null;
        var type = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(TObj)))?.FirstOrDefault();
        if (type == null) return null;

        return Activator.CreateInstance(type, args) as TObj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unload(string contextName = FirstContextName)
    {
        // ok LoadedAssemblies = new();

        // ok when add this _x.LoadedAssemblies = new();
        // _x.Unload(); _x = null; return;

        if (!_contexts.TryGetValue(contextName, out var context)) return;
        context.LoadedAssemblies = new(); // MUST do this or else ALC won't unload

        _contexts.Remove(contextName);
        // _contexts = new Dictionary<string, CollectableAssemblyLoadContext>();

        _deadContext = new WeakReference(context);
        _logger.LogInformation("Unloading context {contextName}", context.Name);

        context.Unload();
    }

    public bool IsUnloaded(string contextName = FirstContextName)
    {
        if (!_contexts.TryGetValue(contextName, out var context)) return true;

        return !_deadContext.IsAlive;
    }

}