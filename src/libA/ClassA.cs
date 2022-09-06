namespace libA;

using System.Reflection;
using static System.Console;

public class ClassA : ITest
{
    public int AddTo(int i) => i + 10;

    public string Message(string message)
    {
        return $">>>> TestA '{message}'";
    }

    public double Value(double d) => d + 10;

    ~ClassA () {
        WriteLine("~~~~ AAAAAAAAAAAAAAAAAAAAAAAAAA!");
    }
}

