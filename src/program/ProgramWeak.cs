#if false
using System.Reflection;
using System.Runtime.Loader;
using static System.Console;
using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using AssemblyContextTest;

Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
var config = ConsoleBasics.BuildConfiguration();
var sp = ConsoleBasics.BuildSerilogServiceProvider(config);
var logger = sp.GetLogger<ITest>();

Console.OutputEncoding = Encoding.UTF8; // for ascii chart

List<WeakReference<ITest>> tests = new();
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
        WriteLine(""^^^^ {0} destroyed!"");
    }}
}}
";

var manager = new AssemblyManager(sp.GetLogger<AssemblyManager>());

var path = Assembly.GetExecutingAssembly().Location;
while (true)
{
    WriteLine("\nPress a key: load(a), load(b), (u)nload, (c)all, (g)arbage (q)uit");

    cmd = ReadKey().KeyChar;
    WriteLine($" pressed");
    switch (cmd)
    {
        case 'q':
            return;
        case 'a':
            {
                var t = manager!.GetImplementationsOf<ITest>(manager!.LoadAssembly(path.Replace("program", "libA")));
                if (t != null)
                    tests.AddRange(t.Select(o => new WeakReference<ITest>(o)));
            }
            break;
        case 'b':
            {
                var t = manager!.GetImplementationsOf<ITest>(manager!.LoadAssembly(path.Replace("program", "libB")));
                if (t != null)
                    tests.AddRange(t.Select( o => new WeakReference<ITest>(o)));
            }
            break;
        case 'c':
            foreach (var i in tests)
            {
                if (i.TryGetTarget(out var t))
                    WriteLine(t.Message(DateTime.Now.ToString()));
                else
                    logger.LogWarning("Weak reference gone");
            }
            break;
        case 'd':
            WriteLine($"There are {AssemblyLoadContext.All.Count()} contexts");
            foreach (var alc in AssemblyLoadContext.All)
            {
                var assemblies = alc.Assemblies;
                WriteLine($"   {alc.Name} with {assemblies.Count()} assemblies");
                if (alc.Name != "Default")
                {
                    foreach (var a in assemblies)
                    {
                        WriteLine($"        {a.GetName()}");
                    }
                }
            }
            break;
        case 'u':
            manager?.Unload();
            //tests.Clear();
            break;
        case '1':
            for (int i = 0; i < 1; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 10, "d + 1"));
                if (t != null)
                    tests.AddRange(t.Select(o => new WeakReference<ITest>(o)));
            }
            break;
        case '2':
            for (int i = 0; i < 100; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 100, "d + 10"));
                if (t != null)
                    tests.AddRange(t.Select(o => new WeakReference<ITest>(o)));
            }
            break;
        case '3':
            for (int i = 0; i < 1000; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 100, "d + 10"));
                if (t != null)
                    tests.AddRange(t.Select(o => new WeakReference<ITest>(o)));
            }
            break;
        case 'r':
            WriteLine("Enter equation using 'd'");
            var s = ReadLine();
            if (!string.IsNullOrEmpty(s))
            {
                var name = $"Test{loadCount++}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 1000, s));
                if (t != null)
                    tests.AddRange(t.Select(o => new WeakReference<ITest>(o)));
            }
            break;
        case 'p':
            foreach (var i in tests)
            {
                if (i.TryGetTarget(out var t))
                {
                    WriteLine($">>>> Chart for {t.Name}");
                    var values = Enumerable.Range(0, 50).Select(o => t.Value(o));
                    WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options { Height = 10 }));
                }
            }
            break;
        case 'g':
            GC.Collect();
            GC.WaitForPendingFinalizers();
            break;
        default:
            break;
    }
}
#endif