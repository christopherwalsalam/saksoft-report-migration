using Microsoft.Extensions.DependencyInjection;
using ReportConversion.Application.Services;
using ReportConversion.Application.Settings;

namespace ReportConversion.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<MetadataExtractionService>();
        services.AddScoped<StaleReportService>();
        services.AddScoped<KpiGroupingService>();
        services.AddScoped<KpiRegistryService>();
        services.AddScoped<DuplicateDetectionService>();
        services.AddScoped<MigrationService>();
        return services;
    }
}
