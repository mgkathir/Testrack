using BureauAdaptor;
using NLog.Extensions.Logging;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Test Bureau Adaptor";
    })
    .ConfigureServices((hostContext, services) =>
    {
        IConfiguration configuration = hostContext.Configuration;
        string[] args = Environment.GetCommandLineArgs();
        Settings options = configuration
                  .GetSection((args.Length > 1) ? args[1].Replace("-", "") : "TU")
                  .Get<Settings>();
        services.AddSingleton(options);
        services.AddHostedService<Worker>();
    })
    .ConfigureLogging(logBuilder =>
    {
        logBuilder.SetMinimumLevel(LogLevel.Trace);
        string[] args = Environment.GetCommandLineArgs();
        logBuilder.AddNLog("nlog" + ((args.Length > 1) ? args[1] : "") +".config");
    })
    .Build();

await host.RunAsync();