using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application;
using ReportConversion.Application.Settings;
using ReportConversion.Infrastructure;
using Serilog;

// ─── Bootstrap ──────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/console-runner-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration(config =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory())
                  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddEnvironmentVariables();
        })
        .ConfigureServices((ctx, services) =>
        {
            services.Configure<SapBoSettings>(ctx.Configuration.GetSection("SapBo"));
            services.Configure<AzureOpenAiSettings>(ctx.Configuration.GetSection("AzureOpenAi"));
            services.Configure<AzureBlobSettings>(ctx.Configuration.GetSection("AzureBlob"));
            services.Configure<PowerBiSettings>(ctx.Configuration.GetSection("PowerBi"));
            services.Configure<StaleReportSettings>(ctx.Configuration.GetSection("StaleReport"));
            services.Configure<DuplicateDetectionSettings>(ctx.Configuration.GetSection("DuplicateDetection"));
            services.Configure<MigrationSettings>(ctx.Configuration.GetSection("Migration"));

            services.AddApplicationServices();
            services.AddInfrastructureServices();
        })
        .Build();

    await ReportConversion.ConsoleRunner.MenuRunner.RunAsync(host.Services);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Console runner terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
