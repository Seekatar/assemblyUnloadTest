using Microsoft.Extensions.Logging;
using Seekatar.Tools;


namespace AssemblyUnload;

/// <summary>
/// Sample class to show how to load and unload assemblies
/// </summary>
/// <remarks>
/// This sample loads assemblies to "run", and also loads some in
/// a "test" context that it will unload at times.
/// </remarks>
public class Engine
{
    private readonly ILogger _logger;
    private readonly AssemblyBuilder _builder;
    private readonly AssemblyManager _manager;

    private List<ITest> _active = new();
    private List<ITest> _testing = new();
    private const string TestContextName = "Test";

    public Engine(ILogger logger)
    {
        _logger = logger;
        _builder = new AssemblyBuilder(logger);
        _manager = new AssemblyManager(logger);
    }

    /// <summary>
    /// Build count assemblies and load them
    /// </summary>
    /// <param name="count"></param>
    /// <param name="name"></param>
    /// <param name="code"></param>
    /// <param name="test">Create in test context</param>
    public void Build(int count, string name, string code, bool test = false)
    {
        var contextName = test ? TestContextName : null;
        for (int i = 0; i < count; i++)
        {
            var fullName = $"{name}-{i}";
            var (pe, _) = _builder.BuildAssembly(fullName, code);
            if (pe != null)
            {
                _manager.LoadFromStream(fullName, pe, contextName: contextName);
                var t = _manager.CreateInstance<ITest>(fullName, contextName);
                if (t != null)
                {
                    if (test)
                        _testing.Add(t);
                    else
                        _active.Add(t);
                }
                else
                {
                    _logger.LogWarning("Didn't create {assemblyName}", fullName);
                }
            }
        }
    }

    /// <summary>
    /// Load an assembly and create an instance of ITest from it
    /// </summary>
    /// <param name="path"></param>
    /// <param name="name"></param>
    /// <param name="test"></param>
    public void Load(string path, string name, bool test = false)
    {
        var contextName = test ? TestContextName : null;

        _manager.LoadFromAssemblyPath(name, path, contextName);
        var t = _manager.CreateInstance<ITest>(name, contextName);
        if (t != null)
        {
            if (test)
                _testing.Add(t);
            else
                _active.Add(t);
        }
        else
        {
            _logger.LogWarning("Didn't create instance named {assemblyName}", name);
        }
    }

    /// <summary>
    /// Unload all assemblies
    /// </summary>
    public void Unload(bool test = false)
    {
        if (test)
        {
            _testing = new List<ITest>();
            _manager.Unload(TestContextName);
        }
        else
        {
            _active = new List<ITest>();
            _manager.Unload();
        }
    }

    /// <summary>
    /// Run this action all all the objects loaded.
    /// </summary>
    /// <param name="action"></param>
    public void DoOnAll(Action<ITest> action, bool test = false)
    {
        foreach (var t in test ? _testing : _active)
        {
            action(t);
        }
    }

    /// <summary>
    /// Run this action all all the objects loaded.
    /// </summary>
    /// <param name="action"></param>
    public void DoOn(string name, Action<ITest> action, bool test = false)
    {
        var t = (test ? _testing : _active).SingleOrDefault(o => o.Name == name);
        if (t != null)
        {
            action(t);
        }
    }


    public bool IsUnloaded() => _manager.IsUnloaded();
}
