using Shouldly;
using AssemblyUnload;
using Xunit.Abstractions;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.IO;
using Seekatar.Tools;
using System.Runtime.Loader;
using System.Diagnostics;

namespace unit;

public class UnitTests
{
    private readonly ILogger _logger;
    private readonly string _path;

    public UnitTests(ITestOutputHelper output)
    {
        _logger = output.BuildLoggerFor<UnitTests>();
        _path = Assembly.GetExecutingAssembly().Location;
        var parts = _path.Split(Path.DirectorySeparatorChar);

        // where libA & libB build to
        var prefix = Path.DirectorySeparatorChar == '/' ? "/" : "";
        _path = prefix+Path.Combine(Path.Combine(parts[0..(parts.Length-6)]), Path.Combine("assemblies",Path.Combine(parts[^3..^1])));
    }
        string code = @"
using System;
namespace {0};
using static System.Console;
using static Math;

public class {0} : ITest
{{
    public string Name => ""{2}"";
    public int AddTo(int i) => i + {1};

    public string Message(string message) => $""@@@@ {0} '{{message}}"";

    public double Value(double d) => {2};

    ~{0} () {{
        WriteLine(""~~~~ {0} destroyed!"");
    }}
}}
";
    [Theory]
    [InlineData("barf!", "barf!", "barf!", 27)]
    [InlineData("barf", "\"test\"", "1.2", 1)]
    public void CompilerErrorTest(string content0, string content1, string content2, int expectedCount)
    {
        var builder = new AssemblyBuilder(_logger);
        try {
            builder.BuildAssembly("test", string.Format(code,content0,content1,content2));
            false.ShouldBeTrue();
        } catch (ProblemDetailsException e) {
            e.Details.Extensions.Count.ShouldBe(expectedCount);
            foreach ( var ex in e.Details.Extensions.Values.OfType<CompilerError>())
            {
                Debug.WriteLine("---");
                Debug.WriteLine(ex.Text);
                Debug.WriteLine(ex.Diagnostic.ToString());
            }
        }
    }

    [Fact]
    public void LoadLibB()
    {
        var engine = new Engine(_logger);

        engine.Load(Path.Join(_path,"libB.dll"), "B");

        TestContext(AssemblyManager.FirstContextName, 1, "libB");
        engine.Unload();

        TakeOutTheTrash(engine);

        TestNoContext(AssemblyManager.FirstContextName);
    }

    [Fact]
    public void LoadAndUnloadLibAB()
    {
        var engine = new Engine(_logger);

        engine.Load(Path.Join(_path, "libB.dll"), "B");
        TestContext(AssemblyManager.FirstContextName, 1, "libB");
        engine.Load(Path.Join(_path, "libA.dll"), "A");

        TestContext(AssemblyManager.FirstContextName, 2, "libA");

        engine.Unload();

        TakeOutTheTrash(engine);

        TestNoContext(AssemblyManager.FirstContextName);
    }

    [Fact]
    public void LoadCallLibB()
    {
        var engine = new Engine(_logger);

        engine.Load(Path.Join(_path, "libB.dll"), "B");

        TestContext(AssemblyManager.FirstContextName, 1, "libB");

        WeakReference b = new (null);
        engine.DoOn("ClassB", o => b = new WeakReference(o) );
        b.IsAlive.ShouldBeTrue();

        engine.Unload();

        TakeOutTheTrash(engine);

        b.IsAlive.ShouldBeFalse();

        TestNoContext(AssemblyManager.FirstContextName);
    }


    private static void TestNoContext(string contextName)
    {
        AssemblyLoadContext.All.FirstOrDefault(o => o.Name == contextName).ShouldBeNull();
    }

    private static void TakeOutTheTrash(Engine engine)
    {
        // similar to sample code
        for (int i = 0; !engine.IsUnloaded() && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }
        engine.IsUnloaded().ShouldBeTrue();
    }

    private static void TestContext(string contextName, int expectedAssemblyCount, string assemblyName = "")
    {
        var context = AssemblyLoadContext.All.FirstOrDefault(o => o.Name == contextName);
        context.ShouldNotBeNull();
        context.Assemblies.Count().ShouldBe(expectedAssemblyCount);

        if (!string.IsNullOrWhiteSpace(assemblyName))
            context.Assemblies.FirstOrDefault(o => o.GetName().Name == assemblyName).ShouldNotBeNull();
    }
}