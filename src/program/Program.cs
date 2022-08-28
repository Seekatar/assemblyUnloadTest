using System;
using System.Reflection;
using System.Runtime.Loader;
using static System.Console;

List<Assembly> assemblies = new List<Assembly>();
List<ITest> tests = new();
var cmd = ' ';

var myContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
Console.WriteLine($"Default Context is {myContext?.Name}");

AssemblyLoadContext? newContext = null;

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
            loadAssembly(path.Replace("program","libA"));
            break;
        case 'b':
            loadAssembly(path.Replace("program","libB"));
            break;
        case 'c':
            foreach (var i in tests)
            {
                WriteLine(i.Message(DateTime.Now.ToString()));
            }
            break;
        case 'u':
            newContext?.Unload();
            tests.Clear();
            newContext = null;
            break;
        case 'g':
            GC.Collect();
            GC.WaitForPendingFinalizers();
            break;
        default:
            break;
    }
}

void unloading(System.Runtime.Loader.AssemblyLoadContext context)
{
    WriteLine($"Context {context.Name} unloading!");
}

void loadAssembly(string fname)
{
    if (newContext is null) {
        newContext = new AssemblyLoadContext("NewContext", isCollectible: true);
        newContext.Unloading += unloading;
        WriteLine("Created new context");
    }

    WriteLine($"Loading {fname} into context.");
    WriteLine($"Context currently has {newContext.Assemblies.Count()} assemblies");

    // var assembly = Assembly.LoadFrom(fname); load into default context
    // var assembly = newContext?.LoadFromAssemblyPath(fname); // locks assembly, even unload doesn't unlock it
    using var fs = new FileStream(fname, FileMode.Open, FileAccess.Read); // can delete the file if use stream
    var assembly = newContext?.LoadFromStream(fs);
    fs.Close();

    if (assembly is not null)
    {
        assemblies.Add(assembly);

        var types = assembly.GetTypes().Where(o => o.IsAssignableTo(typeof(ITest)));
        WriteLine($"Found {types.Count()} ITests");
        tests.AddRange(types.Select(o => Activator.CreateInstance(o) as ITest ?? throw new Exception("ow!")).ToList());

        var context = AssemblyLoadContext.GetLoadContext(assembly);
        if (context is not null)
        {
            Console.WriteLine($"Loaded into context '{context.Name}'");
        }
    }

}
