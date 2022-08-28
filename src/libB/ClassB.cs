namespace libB;
using static System.Console;

public class ClassB : ITest
{
    public int AddTo(int i) => i + 100;

    public string Message(string message) => $">>>> TestB '{message}";
    ~ClassB () {
        WriteLine("BBBBBBBBBBBBBBBBBBBBBBBBBBB!");
    }
}
