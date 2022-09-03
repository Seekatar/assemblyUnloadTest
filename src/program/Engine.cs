using static System.Console;

public class Engine
{
	public Engine()
	{
	}
    
    public string? DoIt(ITest? test)
	{
        DoMore(test);
        return test?.Message("From Engine");
    }

    private void DoMore(ITest? test)
    {
        if (DoMore != null)
        {
            WriteLine($"DoMore: {test?.Message("From DoMore")}");
        }
    }
}
