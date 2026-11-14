using Microsoft.EntityFrameworkCore;
using PayOps.Database;
using PayOps.LegacyImport;
using PayOps.LegacyImport.Legacy;

const string Usage = """
    Copies legacy BulkPayments data into PayOps. Safe to re-run: rows already copied are updated, not duplicated.

    Usage: PayOps.LegacyImport [--dry-run] [--chunk-size 5000] [--report-dir <folder>]

    Connection strings are read from the environment so they never appear in the command line:
      LEGACY_CONNECTION_STRING   legacy BulkPayments database (only read)
      PAYOPS_CONNECTION_STRING   PayOps database (all migrations must be applied first)

      --dry-run      run the whole import, write the report, then roll everything back
      --chunk-size   payments saved per transaction (default 5000)
      --report-dir   where report.md and the CSV files go (default ./legacy-import-<timestamp>)
    """;

var dryRun = false;
var chunkSize = 5000;
string? reportDir = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--dry-run": dryRun = true; break;
        case "--chunk-size" when i + 1 < args.Length && int.TryParse(args[i + 1], out var size) && size > 0: chunkSize = size; i++; break;
        case "--report-dir" when i + 1 < args.Length: reportDir = args[++i]; break;
        default: Console.Error.WriteLine(Usage); return 2;
    }
}

var legacyConnection = Environment.GetEnvironmentVariable("LEGACY_CONNECTION_STRING");
var payOpsConnection = Environment.GetEnvironmentVariable("PAYOPS_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(legacyConnection) || string.IsNullOrWhiteSpace(payOpsConnection))
{
    Console.Error.WriteLine("LEGACY_CONNECTION_STRING and PAYOPS_CONNECTION_STRING must both be set.");
    Console.Error.WriteLine(Usage);
    return 2;
}
reportDir ??= Path.Combine(Environment.CurrentDirectory, $"legacy-import-{DateTime.Now:yyyyMMdd-HHmmss}{(dryRun ? "-dryrun" : "")}");

await using var legacy = new LegacyDbContext(new DbContextOptionsBuilder<LegacyDbContext>()
    .UseSqlServer(legacyConnection, o => o.CommandTimeout(600)).Options);
await using var payOps = new PayOpsDbContext(new DbContextOptionsBuilder<PayOpsDbContext>()
    .UseSqlServer(payOpsConnection, o => o.CommandTimeout(600)).Options);

var pending = (await payOps.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count > 0)
{
    Console.Error.WriteLine($"PayOps database is missing migrations: {string.Join(", ", pending)}. Apply them first.");
    return 3;
}

Console.WriteLine(dryRun ? "Dry run: everything is rolled back at the end." : "Importing legacy data into PayOps.");
var report = await new LegacyImporter(legacy, payOps, new ImportOptions(dryRun, chunkSize), Console.Out).RunAsync();
await report.WriteAsync(reportDir);
Console.WriteLine(report.ToMarkdown());
Console.WriteLine($"Report written to {reportDir}");
return report.TotalsMatch ? 0 : 1;
