using System.Reflection;
using System.Runtime.Loader;
using static System.Console;
using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;


namespace AssemblyUnload;

/// <summary>
/// Class to log when created and when destroyed
/// </summary>
class DestructorTest
{
    string _s;
    public DestructorTest(string s)
    {
        _s = s;
        WriteLine($">>>> CCCCCCCCCCCCCCC {s}");
    }
    ~DestructorTest() { WriteLine($"~~~~ CCCCCCCCCCCCCCCCCCCCC {_s}");  }
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

        var cmd = ' ';

        var myContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        logger.LogInformation($"Default Context is '{myContext?.Name}'");

        int counter = 1;

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
            WriteLine("\nPress a key: (enter for help)");

            cmd = ReadKey().KeyChar;
            var testContext = Char.IsUpper(cmd);
            cmd = Char.ToLower(cmd);

            WriteLine($" pressed");
            switch (cmd)
            {
                case 'f': // test to see that var c never gets collected, but c in newC() does
                    {
                        var c = new DestructorTest("in switch");
                        c = null;
                        newC();
                    }
                    break;
                case 'q': // quit
                    return;
                case 'a': // load libA
                    engine.Load(path.Replace("program", "libA").Replace("bin","obj"), "A", testContext);
                    break;
                case 'b': // load libB
                    engine.Load(path.Replace("program", "libB").Replace("bin","obj"), "B", testContext);
                    break;
                case 'c': // call
                    engine.DoOnAll(t => WriteLine(t.Message($"From Program {DateTime.Now.ToString()}")), testContext);
                    break;
                case 'd': // dump contexts
                    // inline holds the assys
                    dump();
                    break;
                case 'u': // unload
                    engine.Unload(testContext);
                    break;
                case 't': // test
                    WriteLine($"Last unloaded is {engine.IsUnloaded()}");
                    break;
                case 'x':
                    var name = $"ATest1";
                    engine.Build(1, name, string.Format(code, name, 10, "d + 1"), testContext);
                    break;
                case 'y':
                    name = $"ATest100";
                    engine.Build(100, name, string.Format(code, name, 10, "1/(d + 1)"), testContext);
                    break;
                case 'z':
                    name = $"ATest1000";
                    engine.Build(1000, name, string.Format(code, name, 10, "d *d"), testContext);
                    break;
                case 'e': // build from equations
                    {
                        WriteLine("Enter equation using 'd'");
                        var s = ReadLine();
                        if (!string.IsNullOrEmpty(s))
                        {
                            name = $"ATest{counter++}";
                            engine.Build(1, name, string.Format(code, name, 1000, s), testContext);
                        }
                    }
                    break;
                case 'p': // plot
                    engine.DoOnAll(t =>
                    {
                        WriteLine($">>>> Chart for {t.Name}");
                        var values = Enumerable.Range(0, 50).Select(o => t.Value(o));
                        WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options { Height = 10 }));
                    }, testContext);
                    break;
                case 'g':
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    break;
                default:
                    WriteLine("Commands: ");
                    WriteLine("  load static assembly (a), (b)");
                    WriteLine("  load 1, 100, 1000 dynamic assemblies: (x),(y),(z)");
                    WriteLine("  (e)nter expression for new assembly");
                    WriteLine("  (c)all Message, call (p)lot on all");
                    WriteLine("  (d)ump contexts, (t)est unload");
                    WriteLine("  (q)uit");
                    WriteLine("  ");
                    WriteLine("  Use capital letters to load into 'Test' context instead");
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
        var c = new DestructorTest("in fn");
        c = null;
    }
}