using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using ReportConversion.Application.Interfaces;
using ReportConversion.Infrastructure.AzureOpenAIClient;
using ReportConversion.Infrastructure.PowerBiClient;
using ReportConversion.Infrastructure.Repositories;
using ReportConversion.Infrastructure.SapBoClient;

namespace ReportConversion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportElementRepository, ReportElementRepository>();
        services.AddScoped<IReportSqlRepository, ReportSqlRepository>();
        services.AddScoped<IKpiGroupRepository, KpiGroupRepository>();
        services.AddScoped<IKpiRegistryRepository, KpiRegistryRepository>();
        services.AddScoped<IDuplicateReportRepository, DuplicateReportRepository>();
        services.AddScoped<IMigrationJobRepository, MigrationJobRepository>();
        services.AddScoped<IMigrationLogRepository, MigrationLogRepository>();

        // HTTP clients with Polly retry
        services.AddHttpClient<ISapBoClient, SapBoHttpClient>()
            .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IPowerBiClient, PowerBiRestClient>()
            .AddPolicyHandler(GetRetryPolicy());

        // Azure services
        services.AddSingleton<IAzureOpenAiClient, AzureOpenAiHttpClient>();
        services.AddSingleton<IBlobStorageClient, AzureBlobStorageClient>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
