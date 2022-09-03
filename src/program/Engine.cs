using System;

public class Engine
{
	public Engine()
	{
	}
    
    public string? DoIt(ITest? test)
	{
        return test?.Message("From Engine");
    }
}
