using System.Reflection;
using System.Runtime.Loader;
using static System.Console;
using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;

class C
{
    string _s;
    public C(string s)
    {
        _s = s;
        WriteLine($">>>> CCCCCCCCCCCCCCC {s}");
    }
    ~C() { WriteLine($"~~~~ CCCCCCCCCCCCCCCCCCCCC {_s}");  }
}

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var config = ConsoleBasics.BuildConfiguration();
        var sp = ConsoleBasics.BuildSerilogServiceProvider(config);
        var logger = sp.GetLogger<ITest>();

        Console.OutputEncoding = Encoding.UTF8; // for ascii chart

        List<ITest> tests = new();
        var cmd = ' ';

        var myContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        logger.LogInformation($"Default Context is {myContext?.Name}");

        int loadCount = 1;

        var code = @"
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

        var engine = new Engine(logger);

        var path = Assembly.GetExecutingAssembly().Location;
        while (true)
        {
            WriteLine("\nPress a key: load(a), load(b), (u)nload, (c)all, (g)arbage (q)uit");

            cmd = ReadKey().KeyChar;
            WriteLine($" pressed");
            switch (cmd)
            {
                case 'f':
                    {
                        var c = new C("in switch");
                        c = null;
                        newC();
                    }
                    break;
                case 'q':
                    return;
                case 'a':
                    engine.Load(path, "A");
                    break;
                case 'b':
                    engine.Load(path, "B");
                    break;
                case 'B':
                    engine.Load(path, "B", "ALT");
                    break;
                case 'c': // call
                    engine.DoOnAll(t => WriteLine(t.Message($"From Program {DateTime.Now.ToString()}")));
                    break;
                case 'd': // dump
                    // inline holds the assyn
                    dump();
                    break;
                case 'u': // unload
                    engine.Unload();
                    tests.Clear();
                    break;
                case 'U': // unload
                    engine.Unload("ALT");
                    tests.Clear();
                    break;
                case 't': // test
                    WriteLine($"Is unloaded is {engine.IsUnloaded()}");
                    break;
                case '1':
                    var name = $"ATest1";
                    engine.Build(1, name, string.Format(code, name, 10, "d + 1"));
                    break;
                case '!':
                    name = $"ATest1";
                    engine.Build(1, name, string.Format(code, name, 10, "d + 1"), "ALT");
                    break;
                case '2':
                    name = $"ATest2";
                    engine.Build(100, name, string.Format(code, name, 10, "1/(d + 1)"));
                    break;
                case '3':
                    name = $"ATest3";
                    engine.Build(1000, name, string.Format(code, name, 10, "d *d"));
                    break;
                case 'r':
                    {
                        WriteLine("Enter equation using 'd'");
                        var s = ReadLine();
                        if (!string.IsNullOrEmpty(s))
                        {
                            name = $"ATest{loadCount++}";
                            engine.Build(1, name, string.Format(code, name, 1000, s));
                        }
                    }
                    break;
                case 'p':
                    engine.DoOnAll(t =>
                    {
                        WriteLine($">>>> Chart for {t.Name}");
                        var values = Enumerable.Range(0, 50).Select(o => t.Value(o));
                        WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options { Height = 10 }));
                    });
                    break;
                case 'g':
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    break;
                default:
                    break;
            }
        }
    }

    // can't be in main, since that will prevent the context from unloading
    static void dump()
    {
        WriteLine($"There are {AssemblyLoadContext.All.Count()} contexts");
        foreach (var alc in AssemblyLoadContext.All)
        {
            var assemblies = alc.Assemblies;
            WriteLine($"  * '{alc.Name}' with {assemblies.Count()} assemblies");
            if (alc.Name != "Default")
            {
                foreach (var a in assemblies)
                {
                    WriteLine($"    - {a.GetName()}");
                }
            }
        }
    }
    
    // this will go away, but allocating one in Main() will not, even if local
    static void newC()
    {
        var c = new C("in fn");
        c = null;
    }
}