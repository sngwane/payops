using System.Globalization;
using System.Text;

namespace PayOps.LegacyImport;

public sealed record ImportIssue(string Step, string LegacyTable, string LegacyKey, string Action, string Reason);

public sealed record TotalLine(string Item, decimal Legacy, decimal PayOps)
{
    public bool Matches => Legacy == PayOps;
}

public sealed record BatchDifference(int HeaderId, string? BatchId, string? UniqueClientId,
    int LegacyPayments, decimal LegacyAmount, int PayOpsPayments, decimal PayOpsAmount);

public sealed record ClientTotal(string UniqueClientId, int LegacyBatches, int LegacyPayments, decimal LegacyAmount,
    int PayOpsBatches, int PayOpsPayments, decimal PayOpsAmount)
{
    public bool Matches => LegacyBatches == PayOpsBatches && LegacyPayments == PayOpsPayments && LegacyAmount == PayOpsAmount;
}

public sealed class StepCounts(string name)
{
    public string Name { get; } = name;
    public int Read { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Skipped { get; set; }
    public int Repairs { get; set; }
}

/// <summary>What the import did, plus count and money parity between legacy and PayOps. Written as Markdown and CSV files.</summary>
public sealed class ImportReport(bool dryRun)
{
    public bool DryRun { get; } = dryRun;
    public DateTime StartedUtc { get; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }
    public List<StepCounts> Steps { get; } = [];
    public List<ImportIssue> Issues { get; } = [];
    public List<string> Notes { get; } = [];
    public List<TotalLine> Totals { get; } = [];
    public List<BatchDifference> BatchDifferences { get; } = [];
    public List<ClientTotal> Clients { get; } = [];

    public bool TotalsMatch => Totals.All(t => t.Matches) && BatchDifferences.Count == 0;

    public StepCounts Step(string name)
    {
        var step = new StepCounts(name);
        Steps.Add(step);
        return step;
    }

    public void Skip(StepCounts step, string table, object key, string reason)
    {
        step.Skipped++;
        Issues.Add(new ImportIssue(step.Name, table, Convert.ToString(key, CultureInfo.InvariantCulture) ?? "", "Skipped", reason));
    }

    public void Repair(StepCounts step, string table, object key, string reason)
    {
        step.Repairs++;
        Issues.Add(new ImportIssue(step.Name, table, Convert.ToString(key, CultureInfo.InvariantCulture) ?? "", "Repaired", reason));
    }

    public async Task WriteAsync(string directory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), ToMarkdown(), ct);
        await WriteCsvAsync(Path.Combine(directory, "issues.csv"), ["Step", "LegacyTable", "LegacyKey", "Action", "Reason"],
            Issues.Select(i => new object?[] { i.Step, i.LegacyTable, i.LegacyKey, i.Action, i.Reason }), ct);
        await WriteCsvAsync(Path.Combine(directory, "batch-differences.csv"),
            ["HeaderId", "BatchId", "UniqueClientId", "LegacyPayments", "LegacyAmount", "PayOpsPayments", "PayOpsAmount"],
            BatchDifferences.Select(d => new object?[] { d.HeaderId, d.BatchId, d.UniqueClientId, d.LegacyPayments, d.LegacyAmount, d.PayOpsPayments, d.PayOpsAmount }), ct);
        await WriteCsvAsync(Path.Combine(directory, "clients.csv"),
            ["UniqueClientId", "LegacyBatches", "LegacyPayments", "LegacyAmount", "PayOpsBatches", "PayOpsPayments", "PayOpsAmount", "Matches"],
            Clients.Select(c => new object?[] { c.UniqueClientId, c.LegacyBatches, c.LegacyPayments, c.LegacyAmount, c.PayOpsBatches, c.PayOpsPayments, c.PayOpsAmount, c.Matches }), ct);
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Legacy import {(DryRun ? "dry run (rolled back, nothing kept)" : "run")}");
        sb.AppendLine();
        sb.AppendLine($"Started {StartedUtc:yyyy-MM-dd HH:mm:ss} UTC, finished {FinishedUtc:yyyy-MM-dd HH:mm:ss} UTC.");
        sb.AppendLine();
        sb.AppendLine($"Totals match: **{(TotalsMatch ? "yes" : "no")}**");
        sb.AppendLine();
        sb.AppendLine("| Step | Read | Inserted | Updated | Unchanged | Skipped | Repairs |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var s in Steps)
            sb.AppendLine($"| {s.Name} | {s.Read} | {s.Inserted} | {s.Updated} | {s.Unchanged} | {s.Skipped} | {s.Repairs} |");
        sb.AppendLine();
        sb.AppendLine("| Legacy vs PayOps | Legacy | PayOps | Match |");
        sb.AppendLine("|---|---:|---:|---|");
        foreach (var t in Totals)
            sb.AppendLine(FormattableString.Invariant($"| {t.Item} | {t.Legacy:#,0.##} | {t.PayOps:#,0.##} | {(t.Matches ? "yes" : "NO")} |"));
        sb.AppendLine();
        sb.AppendLine($"Batches whose payment count or amount differs: {BatchDifferences.Count} (see `batch-differences.csv`).");
        sb.AppendLine($"Clients whose totals differ: {Clients.Count(c => !c.Matches)} of {Clients.Count} (see `clients.csv`).");
        if (Notes.Count > 0)
        {
            sb.AppendLine();
            foreach (var n in Notes) sb.AppendLine($"- {n}");
        }
        if (Issues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| Step | Action | Reason | Rows |");
            sb.AppendLine("|---|---|---|---:|");
            foreach (var g in Issues.GroupBy(i => (i.Step, i.Action, i.Reason)).OrderBy(g => g.Key.Step).ThenByDescending(g => g.Count()))
                sb.AppendLine($"| {g.Key.Step} | {g.Key.Action} | {g.Key.Reason} | {g.Count()} |");
            sb.AppendLine();
            sb.AppendLine("Every row is listed in `issues.csv`.");
        }
        return sb.ToString();
    }

    private static async Task WriteCsvAsync(string path, string[] header, IEnumerable<object?[]> rows, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        await writer.WriteLineAsync(string.Join(',', header));
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(',', row.Select(Csv)));
        }
    }

    private static string Csv(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
