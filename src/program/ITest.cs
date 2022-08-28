public interface ITest
{
    string Name => GetType().Name;
    string Message(string message);
    int AddTo(int i);
    double Value(double d);
}
