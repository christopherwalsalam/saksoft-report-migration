using Microsoft.Extensions.DependencyInjection;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Services;
using Serilog;

namespace ReportConversion.ConsoleRunner;

public static class MenuRunner
{
    public static async Task RunAsync(IServiceProvider services)
    {
        PrintBanner();
        bool running = true;

        while (running)
        {
            PrintMenu();
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await ExtractMetadataAsync(services);
                    break;
                case "2":
                    await IdentifyStaleReportsAsync(services);
                    break;
                case "3":
                    await GroupByKpiAsync(services);
                    break;
                case "4":
                    await FindDuplicatesAsync(services);
                    break;
                case "5":
                    await MigrateReportsAsync(services);
                    break;
                case "6":
                    await ViewMigrationSummaryAsync(services);
                    break;
                case "0":
                    running = false;
                    Log.Information("Exiting console runner");
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Invalid option. Please try again.");
                    Console.ResetColor();
                    break;
            }

            if (running)
            {
                Console.WriteLine();
                Console.WriteLine("  Press any key to continue...");
                Console.ReadKey(intercept: true);
            }
        }
    }

    // ─── Menu Actions ──────────────────────────────────────────────────────

    private static async Task ExtractMetadataAsync(IServiceProvider services)
    {
        PrintHeader("1. Extract SAP BO Metadata");
        Log.Information("Starting metadata extraction from SAP BusinessObjects...");

        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<MetadataExtractionService>();

        try
        {
            var result = await svc.ExtractAllReportsAsync();
            PrintSuccess($"Extraction complete. Extracted: {result.ExtractedCount}, Failed: {result.FailedCount}");
            if (result.Errors.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Errors: {string.Join("; ", result.Errors.Take(5))}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            PrintError($"Extraction failed: {ex.Message}");
            Log.Error(ex, "Metadata extraction failed");
        }
    }

    private static async Task IdentifyStaleReportsAsync(IServiceProvider services)
    {
        PrintHeader("2. Identify Stale Reports");
        Log.Information("Analysing report usage to identify stale and unused reports...");

        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<StaleReportService>();

        try
        {
            var result = await svc.IdentifyStaleReportsAsync();
            PrintSuccess($"Analysis complete.");
            Console.WriteLine($"  Active:     {result.ActiveCount,6}");
            Console.WriteLine($"  Stale:      {result.StaleCount,6}");
            Console.WriteLine($"  Never Used: {result.NeverUsedCount,6}");
        }
        catch (Exception ex)
        {
            PrintError($"Stale report analysis failed: {ex.Message}");
            Log.Error(ex, "Stale report analysis failed");
        }
    }

    private static async Task GroupByKpiAsync(IServiceProvider services)
    {
        PrintHeader("3. Group Reports by KPI (Azure OpenAI)");
        Log.Information("Sending report metadata to Azure OpenAI for KPI classification...");

        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<KpiGroupingService>();

        try
        {
            var result = await svc.GroupReportsByKpiAsync();
            PrintSuccess($"KPI grouping complete. Grouped: {result.GroupedCount}, Failed: {result.FailedCount}");
        }
        catch (Exception ex)
        {
            PrintError($"KPI grouping failed: {ex.Message}");
            Log.Error(ex, "KPI grouping failed");
        }
    }

    private static async Task FindDuplicatesAsync(IServiceProvider services)
    {
        PrintHeader("4. Find Duplicate Reports (Embeddings)");
        Log.Information("Generating embeddings and detecting duplicate reports...");

        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<DuplicateDetectionService>();

        try
        {
            var result = await svc.FindDuplicatesAsync();
            PrintSuccess($"Duplicate detection complete. Pairs found: {result.DuplicatePairsFound}");
        }
        catch (Exception ex)
        {
            PrintError($"Duplicate detection failed: {ex.Message}");
            Log.Error(ex, "Duplicate detection failed");
        }
    }

    private static async Task MigrateReportsAsync(IServiceProvider services)
    {
        PrintHeader("5. Migrate Reports to Power BI");

        Console.Write("  Migrate all reports or specific IDs? [A]ll / [I]Ds: ");
        var choice = Console.ReadLine()?.Trim().ToUpper();

        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<MigrationService>();

        try
        {
            if (choice == "I")
            {
                Console.Write("  Enter comma-separated report IDs: ");
                var input = Console.ReadLine() ?? string.Empty;
                var ids = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                               .Where(id => id > 0)
                               .ToList();

                if (!ids.Any())
                {
                    PrintError("No valid IDs provided.");
                    return;
                }

                Log.Information("Migrating {Count} reports: {Ids}", ids.Count, string.Join(", ", ids));
                await svc.MigrateReportsByIdsAsync(ids);
                PrintSuccess($"Migration complete for {ids.Count} reports.");
            }
            else
            {
                Log.Information("Starting full migration for all reports...");
                await svc.MigrateAllReportsAsync();
                PrintSuccess("Full migration complete.");
            }
        }
        catch (Exception ex)
        {
            PrintError($"Migration failed: {ex.Message}");
            Log.Error(ex, "Migration failed");
        }
    }

    private static async Task ViewMigrationSummaryAsync(IServiceProvider services)
    {
        PrintHeader("6. Migration Summary");

        using var scope = services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMigrationJobRepository>();

        try
        {
            var (total, success, failed, pending, inProgress) = await repo.GetSummaryAsync();
            double rate = total > 0 ? Math.Round((double)success / total * 100, 1) : 0;

            Console.WriteLine();
            Console.WriteLine("  ┌─────────────────────────────────┐");
            Console.WriteLine($"  │ Total Jobs:      {total,14} │");
            Console.WriteLine($"  │ Success:         {success,14} │");
            Console.WriteLine($"  │ Failed:          {failed,14} │");
            Console.WriteLine($"  │ Pending:         {pending,14} │");
            Console.WriteLine($"  │ In Progress:     {inProgress,14} │");
            Console.WriteLine($"  │ Success Rate:    {rate,13}% │");
            Console.WriteLine("  └─────────────────────────────────┘");
        }
        catch (Exception ex)
        {
            PrintError($"Failed to retrieve summary: {ex.Message}");
            Log.Error(ex, "Summary retrieval failed");
        }
    }

    // ─── UI Helpers ────────────────────────────────────────────────────────

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║        ReportConversion — SAP BO → Power BI          ║");
        Console.WriteLine("  ║        Migration Console Runner v1.0                 ║");
        Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintMenu()
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  ─────────────────────────────────────────────────────");
        Console.WriteLine("  Main Menu");
        Console.WriteLine("  ─────────────────────────────────────────────────────");
        Console.ResetColor();
        Console.WriteLine("  [1]  Extract SAP BO Metadata");
        Console.WriteLine("  [2]  Identify Stale / Unused Reports");
        Console.WriteLine("  [3]  Group Reports by KPI (Azure OpenAI)");
        Console.WriteLine("  [4]  Find Duplicate Reports (Embeddings)");
        Console.WriteLine("  [5]  Migrate Reports to Power BI");
        Console.WriteLine("  [6]  View Migration Summary");
        Console.WriteLine("  [0]  Exit");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  Select option: ");
        Console.ResetColor();
    }

    private static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ── {title} ──");
        Console.ResetColor();
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {message}");
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {message}");
        Console.ResetColor();
    }
}
