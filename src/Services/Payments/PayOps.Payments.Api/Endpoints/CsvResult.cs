using System.Globalization;
using System.Reflection;
using System.Text;

namespace PayOps.Payments.Api.Endpoints;

/// <summary>Streams rows as CSV (header = property names). Text that a spreadsheet would run as a formula is prefixed with '.</summary>
internal sealed class CsvResult<T>(IAsyncEnumerable<T> rows, string fileName) : IResult
{
    private static readonly PropertyInfo[] Columns = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    public static bool Wanted(HttpRequest request, string? format)
        => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
           || request.Headers.Accept.Any(a => a?.Contains("text/csv", StringComparison.OrdinalIgnoreCase) == true);

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(',', Columns.Select(c => c.Name)));
        var count = 0;
        await foreach (var row in rows.WithCancellation(httpContext.RequestAborted))
        {
            await writer.WriteLineAsync(string.Join(',', Columns.Select(c => Format(c.GetValue(row)))));
            if (++count % 500 == 0)
                await writer.FlushAsync(httpContext.RequestAborted);
        }
        await writer.FlushAsync(httpContext.RequestAborted);
    }

    private static string Format(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
