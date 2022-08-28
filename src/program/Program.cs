using System;
using System.Reflection;
using System.Runtime.Loader;
using static System.Console;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

List<Assembly> assemblies = new();
List<PortableExecutableReference> _references = new();
List<ITest> tests = new();
var cmd = ' ';

var myContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
Console.WriteLine($"Default Context is {myContext?.Name}");

AssemblyLoadContext? newContext = null;
int loadCount = 1;

var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
var trustedAssemblyPaths = trustedAssemblies.Split(Path.PathSeparator);
_references = trustedAssemblyPaths.Select(path => MetadataReference.CreateFromFile(path)).ToList();

var code = @"
using System;
namespace {0};
using static System.Console;

public class {0} : ITest
{{
    public int AddTo(int i) => i + {1};

    public string Message(string message) => $""@@@@ {0} '{message}"";

    public double Value(double d) => {2};

    ~{0} () {{
        WriteLine(""^^^^ {0} destroyed!"");
    }}
}}
";


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
            loadAssembly(path.Replace("program", "libA"));
            break;
        case 'b':
            loadAssembly(path.Replace("program", "libB"));
            break;
        case 'c':
            foreach (var i in tests)
            {
                WriteLine(i.Message(DateTime.Now.ToString()));
            }
            break;
        case 'd':
            WriteLine ($"There are {AssemblyLoadContext.All.Count()} contexts");
            foreach(var alc in AssemblyLoadContext.All) {
                WriteLine ($"   {alc.Name}");
            }
            break;
        case 'u':
            newContext?.Unload();
            tests.Clear();
            assemblies.Clear();
            newContext = null;
            break;
        case '1':
            for (int i = 0; i < 1; i++)
            {
                var name = $"Test{i}";
                var (p, d) = BuildAssembly(string.Format(code, name, 10, "d + 10" ), name);
                if (p is not null && d is not null)
                {
                    loadAssembly(name, p, d);
                    p.Dispose();
                    d.Dispose();
                }
            }
            break;
        case '2':
            for (int i = 0; i < 1000; i++)
            {
                var name = $"Test{i}";
                var (p, d) = BuildAssembly(string.Format(code, name, 11, "d + 11"), name);
                if (p is not null && d is not null)
                {
                    loadAssembly(name, p);
                    p.Dispose();
                    d.Dispose();
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

void unloading(System.Runtime.Loader.AssemblyLoadContext context)
{
    WriteLine($"Context {context.Name} unloading!");
}

void loadAssembly(string fname, Stream? s = null, Stream? pdbStream = null)
{
    if (newContext is null)
    {
        newContext = new AssemblyLoadContext($"NewContext{loadCount++}", isCollectible: true);
        newContext.Unloading += unloading;
        WriteLine("Created new context");
    }

    WriteLine($"Loading {fname} into context.");
    WriteLine($"Context currently has {newContext.Assemblies.Count()} assemblies");

    // var assembly = Assembly.LoadFrom(fname); load into default context
    // var assembly = newContext?.LoadFromAssemblyPath(fname); // locks assembly, even unload doesn't unlock it
    FileStream? fs = null;
    if (s == null)
    {
        fs = new FileStream(fname, FileMode.Open, FileAccess.Read); // can delete the file if use stream
        s = fs;
    }

    var assembly = newContext?.LoadFromStream(s, pdbStream);
    fs?.Close();
    fs?.Dispose();

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

bool CheckForErrors(List<Diagnostic> diag)
{
    if (!diag.Any(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) return true;

    WriteLine($"Compiler errors");
    foreach (var e in diag.Where(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
    {
        WriteLine($"   {e.ToString()}");
    }
    return false;
}

(Stream? pe, Stream? pdb) BuildAssembly(string code, string name)
{
    var fileName = name + ".dll";

    // largely from http://www.albahari.com/nutshell/cs10ian-supplement.pdf p54
    SyntaxTree tree;
    tree = CSharpSyntaxTree.ParseText(code);

    CSharpCompilation compilation;

    compilation = CSharpCompilation
        .Create(name)
        .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
        .AddSyntaxTrees(tree)
        .AddReferences(_references);

    var diag = compilation.GetDiagnostics();
    if (!CheckForErrors(diag.ToList())) return (null, null);


    // diag = compilation.Emit(fileName,name+".pdb").Diagnostics;
    var peStream = new MemoryStream();
    var pdbStream = new MemoryStream();

    compilation.Emit(peStream, pdbStream);
    if (!CheckForErrors(diag.ToList())) return (null, null);

    peStream.Seek(0, SeekOrigin.Begin);
    pdbStream.Seek(0, SeekOrigin.Begin);
    return (peStream, pdbStream);
}