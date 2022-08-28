namespace libB;
using static System.Console;

public class ClassB : ITest
{
    public int AddTo(int i) => i + 100;

    public string Message(string message) => $">>>> TestB '{message}";

    public double Value(double d) => d + 100;

    ~ClassB () {
        WriteLine("BBBBBBBBBBBBBBBBBBBBBBBBBBB!");
    }
}
