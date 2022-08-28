
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seekatar.Tools;
using Serilog;
using Serilog.Configuration;

public static class ConsoleBasics
{
    public static IConfiguration BuildConfiguration()
    {
        var ret = new ConfigurationBuilder();
        if (String.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            ret.AddSharedDevSettings()
               .AddJsonFile("appsettings.Development.json", true, true);
        }
        return ret.AddJsonFile("appsettings.json", true, true)
                  .AddEnvironmentVariables()
                  .Build();


    }

    public static ILogger<T> InitSerilogAndGetLogger<T>(IConfiguration configuration) where T : class
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.InitSerilog(configuration);
        var provider = serviceCollection.BuildServiceProvider();
        return provider.GetLogger<T>();
    }

    public static ServiceProvider BuildSerilogServiceProvider(IConfiguration configuration)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.InitSerilog(configuration);
        var provider = serviceCollection.BuildServiceProvider();
        return provider;
    }

    public static void InitSerilog(this ServiceCollection me, IConfiguration configuration)
    {
        Serilog.Log.Logger = new Serilog.LoggerConfiguration()
            .ReadFrom
            .Configuration(configuration)
            .CreateLogger();

        me.AddLogging(configure => configure.AddSerilog());
    }

    public static ILogger<T> GetLogger<T>(this ServiceProvider serviceProvider) where T : class
    {
        var logger = serviceProvider.GetService<ILogger<T>>();
        if (logger is null)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            logger = loggerFactory.CreateLogger<T>();
            if (logger is null) throw new Exception("Can't create logger");
        }

        return logger;
    }

}
