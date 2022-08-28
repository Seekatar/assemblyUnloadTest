namespace libA;

using System.Reflection;
using static System.Console;

public class ClassA : ITest
{
    private string? _value;

    public int AddTo(int i) => i + 10;

    public string Message(string message)
    {
        var thisAssembly = Assembly.GetExecutingAssembly();
        foreach( var s in thisAssembly.GetManifestResourceNames()) {
            WriteLine(s);
        }
        using var stream = thisAssembly.GetManifestResourceStream("libA.helm.exe");
        if (stream != null)
        {
            WriteLine("Reading stream");
            using var reader = new StreamReader(stream);
            _value = reader.ReadToEnd();
            WriteLine($"Read {_value.Length}");
        }
        return $">>>> TestA '{message}'";
    }

    public double Value(double d) => d + 10;

    ~ClassA () {
        WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAA!");
    }
}

