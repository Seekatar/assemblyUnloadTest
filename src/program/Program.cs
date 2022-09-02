using System.Reflection;
using System.Runtime.Loader;
using static System.Console;
using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using AssemblyContextTest;
using program;

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
        WriteLine(""^^^^ {0} destroyed!"");
    }}
}}
";

var manager = new AssemblyManager(sp.GetLogger<AssemblyManager>());
var testCollection = new TestCollection(manager);

var path = Assembly.GetExecutingAssembly().Location;
while (true)
{
    WriteLine("\nPress a key: load(a), load(b), (u)nload, (c)all, (g)arbage (q)uit");

    cmd = ReadKey().KeyChar;
    WriteLine($" pressed");
    switch (cmd)
    {
        case 'z':
            // pretty much sample, works since self contained
            AssemblyManager.ExecuteAndUnload(true, path.Replace("program", "libB"), out var hostAlcWeakRef);
            // Poll and run GC until the AssemblyLoadContext is unloaded.
            // You don't need to do that unless you want to know when the context
            // got unloaded. You can just leave it to the regular GC.
            for (int i = 0; hostAlcWeakRef.IsAlive && (i < 10); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Console.WriteLine($"Unload success: {!hostAlcWeakRef.IsAlive}");
            break;
        case 'y':
            // doesn't work since t returned
            {
                WeakReference hostAlcWeakRef2;
                {
                    var t = AssemblyManager.Get(path.Replace("program", "libB"), out hostAlcWeakRef2);
                    WriteLine(t.Message("HI!!!!!"));
                    t = null;
                }
                var alc = hostAlcWeakRef2.Target as AssemblyLoadContext;
                alc!.Unload();

                // Poll and run GC until the AssemblyLoadContext is unloaded.
                // You don't need to do that unless you want to know when the context
                // got unloaded. You can just leave it to the regular GC.
                for (int i = 0; hostAlcWeakRef2.IsAlive && (i < 10); i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Console.WriteLine($"Unload success: {!hostAlcWeakRef2.IsAlive}");
            }
            break;
        case 'x':
            // works
            {
                AssemblyManager.ExecuteAndUnload(false, path.Replace("program", "libB"), out var hostAlcWeakRef3);
                var alc = hostAlcWeakRef3.Target as AssemblyLoadContext;
                alc!.Unload();

                // Poll and run GC until the AssemblyLoadContext is unloaded.
                // You don't need to do that unless you want to know when the context
                // got unloaded. You can just leave it to the regular GC.
                for (int i = 0; hostAlcWeakRef3.IsAlive && (i < 100); i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(2);
                }

                Console.WriteLine($"Unload success: {!hostAlcWeakRef3.IsAlive}");
            }
            break;
        case 'w':
            // works
            {
                AssemblyManager.GetAndCall( path.Replace("program", "libB"), out var hostAlcWeakRef3);
                var alc = hostAlcWeakRef3.Target as AssemblyLoadContext;
                alc!.Unload();

                // Poll and run GC until the AssemblyLoadContext is unloaded.
                // You don't need to do that unless you want to know when the context
                // got unloaded. You can just leave it to the regular GC.
                for (int i = 0; hostAlcWeakRef3.IsAlive && (i < 100); i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(2);
                }

                Console.WriteLine($"Unload success: {!hostAlcWeakRef3.IsAlive}");
            }
            break;
        case 'v':
            // works
            {
                testCollection.Load(path.Replace("program", "libB"));
                Thread.Sleep(3000);
                testCollection.Call();
                testCollection.Unload();
            }
            break;
        case '9':
            // ???
            {
                testCollection.Load(path.Replace("program", "libB"));
                testCollection.LoadAnother(path.Replace("program", "libA"));
                Thread.Sleep(3000);
                testCollection.Call();
                testCollection.Unload();
            }
            break;
        case 'q':
            return;
        case 'a':
            {
                var t = manager!.GetImplementationsOf<ITest>(manager!.LoadAssembly(path.Replace("program", "libA")));
                if (t != null)
                    tests.AddRange(t);
            }
            break;
        case 'b':
            {
                manager!.LoadAssembly(path.Replace("program", "libB"));
                // if (t != null)
                //     tests.AddRange(t);
            }
            break;
        case 'c':
            foreach (var t in tests)
            {
                WriteLine(t.Message(DateTime.Now.ToString()));
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
            tests.Clear();
            WriteLine($"Is unloaded is {manager?.IsUnloaded()}");
            break;
        case 't':
            WriteLine($"Is unloaded is {manager?.IsUnloaded()}");
            break;
        case '1':
            for (int i = 0; i < 1; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 10, "d + 1"));
                if (t != null)
                    tests.AddRange(t);
            }
            break;
        case '2':
            for (int i = 0; i < 100; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 100, "d + 10"));
                if (t != null)
                    tests.AddRange(t);
            }
            break;
        case '3':
            for (int i = 0; i < 1000; i++)
            {
                var name = $"Test{i}";
                var t = manager!.BuildAndGet<ITest>(name, string.Format(code, name, 100, "d + 10"));
                if (t != null)
                    tests.AddRange(t);
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
                    tests.AddRange(t);
            }
            break;
        case 'p':
            foreach (var t in tests)
            {
                WriteLine($">>>> Chart for {t.Name}");
                var values = Enumerable.Range(0, 50).Select(o => t.Value(o));
                WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options { Height = 10 }));
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
