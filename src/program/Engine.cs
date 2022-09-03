using Microsoft.Extensions.Logging;
using Seekatar.Tools;
using static System.Console;

public class Engine
{
    private readonly ILogger _logger;
    AssemblyBuilder<ITest> _builder;
    private readonly AssemblyManager<ITest> _manager;

    private List<ITest> _tests = new();

    public Engine(ILogger logger)
    {
        _logger = logger;
        _builder = new AssemblyBuilder<ITest>(logger);
        _manager = new AssemblyManager<ITest>(logger); 
    }

    public string? DoIt(ITest? test)
    {
        DoMore(test);
        return test?.Message("From Engine");
    }

    private void DoMore(ITest? test)
    {
        if (DoMore != null)
        {
            WriteLine($"DoMore: {test?.Message("From DoMore")}");
        }
    }

    internal void Build(int count, string name, string code)
    {
        for (int i = 0; i < count; i++)
        {
            var fullName = $"{name}-{count}";
            var (pe, _) = _builder.BuildAssembly(fullName, code);
            _manager.LoadFromStream(fullName, pe);
            var t = _manager.CreateInstance<ITest>(name);
            if (t != null)
            {
                _tests.Add(t);
            }
        }
    }

    internal void Load(string path, string name)
    {
        _manager.LoadFromAssemblyPath(name, path.Replace("program", $"lib{name}"));
        var t = _manager.CreateInstance<ITest>(name);
        if (t != null)
            _tests.Add(t);
    }

    internal void Unload()
    {
        _tests = new List<ITest>();
        _manager.Unload();
    }

    internal void DoOnAll(Action<ITest> action)
    {
        foreach (var t in _tests)
        {
            action(t);
        }
    }
}
