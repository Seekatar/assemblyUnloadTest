using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Seekatar.Tools;

/// <summary>
/// Class for loading and unloading assemblies
/// </summary>
public class AssemblyManager
{
    /// <summary>
    /// private class to override Load() per doc
    /// </summary>
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
    public const string FirstContextName = "__FirstContext__";
    private Dictionary<string, CollectableAssemblyLoadContext> _contexts = new() { { FirstContextName, new CollectableAssemblyLoadContext(FirstContextName) } };
    private WeakReference _deadContext = new(null);

    private CollectableAssemblyLoadContext CheckContext(string contextName)
    {
        if (!_contexts.TryGetValue(contextName, out var context))
        {
            context = new CollectableAssemblyLoadContext(contextName);
            _contexts.Add(contextName, context);
        }
        return context;
    }

    public AssemblyManager(ILogger<AssemblyManager> logger)
    {
        _logger = logger;
    }

    public AssemblyManager(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load an assembly give the path to a dll
    /// </summary>
    /// <param name="label">Label used in other calls. Should be unique to context</param>
    /// <param name="fileName">Filename of dll</param>
    /// <param name="contextName">Name of the context to load into</param>
    /// <returns>true if loaded</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool LoadFromAssemblyPath(string label, string fileName, string? contextName = null)
    {
        contextName ??= FirstContextName;

        var context = CheckContext(contextName);

        _logger.LogInformation("Loading {assemblyName} into context {contextName} that currently has {assemblyCount} assemblies.", label, contextName, context.Assemblies.Count());

        if (!File.Exists(fileName))  
        {    _logger.LogWarning("Assembly file {assembly} does not exist", fileName);
            return false;
            
        }

        try
        {
            var ret = context.LoadFromAssemblyPath(fileName);
            if (ret != null)
                context.LoadedAssemblies.Add(label, ret);
            return ret != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading {assemblyName} into context {contextName}", label, contextName);
            return false;
        }
    }


    /// <summary>
    /// Load an assembly give the stream
    /// </summary>
    /// <param name="label">Label used in other calls. Should be unique to context</param>
    /// <param name="assembly">Stream of assembly</param>
    /// <param name="assemblySymbols">Stream of assembly symbols</param>
    /// <param name="contextName">Name of the context to load into</param>
    /// <returns>true if loaded</returns>
    public bool LoadFromStream(string label, Stream assembly, Stream? assemblySymbols = null, string? contextName = null)
    {
        contextName ??= FirstContextName;
        if (assembly is null) return false;

        var context = CheckContext(contextName);
        _logger.LogInformation("Loading {assemblyName} into context {contextName} that currently has {assemblyCount} assemblies.", label, contextName, context.Assemblies.Count());

        var ret = context.LoadFromStream(assembly, assemblySymbols);
        if (ret != null)
            context.LoadedAssemblies.Add(label, ret);
        return ret != null;
    }

    /// <summary>
    /// Create an instance of an object from a loaded assembly in the manager's first (default) context
    /// </summary>
    /// <param name="label">Label given to assembly on Load()</param>
    /// <param name="args">Constructor arguments</param>
    /// <returns>The new object, or null</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public TObj? CreateInstance<TObj>(string label, params object?[]? args) where TObj : class
    {
        return CreateInstance<TObj>(label, FirstContextName, args);
    }

    /// <summary>
    /// Create an instance of an object from a loaded assembly from a specific contet
    /// </summary>
    /// <typeparam name="TObj">Type of object to create</typeparam>
    /// <param name="label">Label given to assembly on Load()</param>
    /// <param name="contextName">Name give to assembly on Load</param>
    /// <param name="args">Constructor arguments</param>
    /// <returns>The new object, or null</returns>
    /// <remarks>
    /// WARNING, you must release all references to the returned object before doing Unload(), otherwise the assembly will not unload
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public TObj? CreateInstance<TObj>(string label, string? contextName, params object?[]? args) where TObj : class
    {
        contextName ??= FirstContextName;
        if (_contexts.TryGetValue(contextName, out var context))
        {
            var assembly = context.LoadedAssemblies.Where(o => o.Key == label).Select(o => o.Value).FirstOrDefault();
            if (assembly == null)
            {
                _logger.LogWarning("Didn't find assembly for {assemblyName}", label);
                return null;
            }
            var type = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(TObj)))?.FirstOrDefault();
            if (type == null)
            {
                _logger.LogWarning("Didn't type of {typeName} in {assemblyName}", typeof(TObj).Name, label);
                return null;
            }

            return Activator.CreateInstance(type, args) as TObj;
        }
        else
        {
            _logger.LogWarning("Didn't find context name of {contextName}", contextName);
            return null;
        }
    }

    /// <summary>
    /// Unload a context and all its assemblies
    /// </summary>
    /// <param name="contextName">Name of context to unload</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unload(string? contextName = null)
    {
        contextName ??= FirstContextName;

        if (!_contexts.TryGetValue(contextName, out var context)) return;
        context.LoadedAssemblies = new(); // MUST do this or else ALC won't unload

        _contexts.Remove(contextName);

        _deadContext = new WeakReference(context); // create this to check if unloaded, if we care

        _logger.LogInformation("Unloading context {contextName}", context.Name);

        context.Unload();
    }

    /// <summary>
    /// Diagnostic to check to see if last unloaded context has actually unloaded.
    /// </summary>
    /// <param name="contextName"></param>
    /// <returns></returns>
    public bool IsUnloaded(string? contextName = null)
    {
        return !_deadContext.IsAlive;
    }

}