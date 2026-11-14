using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using PayOps.BatchFile.Api.Authentication;
using PayOps.BatchFile.Data;
using PayOps.BatchFile.Data.Services;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Api.Endpoints;

/// <summary>Endpoints for clients' systems, signed in with an API key.</summary>
public static class BatchEndpoints
{
    public static IEndpointRouteBuilder MapBatchEndpoints(this IEndpointRouteBuilder app)
    {
        var batches = app.MapGroup("/api/batches").WithTags("Batches").RequireAuthorization(BatchFileAuth.ClientPolicy);

        batches.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, BatchSubmissionService service, CancellationToken ct) =>
        {
            var body = await ReadBodyAsync(request, ct);
            if (body.Error is not null)
                return body.Error;
            var hash = SHA256.HashData(body.Bytes!);
            if (Deserialize(request, body.Bytes!, out var batch) is { } invalid)
            {
                await service.RecordUnreadableAsync(user.GetBatchCaller(), BatchUploadFormat.Json, hash, new Dictionary<string, string[]>(invalid.ProblemDetails.Errors), ct);
                return invalid;
            }
            return (await service.SubmitAsync(user.GetBatchCaller(), batch!, BatchUploadFormat.Json, hash, ct))
                .ToCreated(b => $"/api/batches/{b.BatchId}");
        }).Accepts<BatchSubmission>("application/json");

        batches.MapPost("/xml", async (HttpRequest request, ClaimsPrincipal user, BatchSubmissionService service, CancellationToken ct) =>
        {
            var body = await ReadBodyAsync(request, ct);
            if (body.Error is not null)
                return body.Error;
            return (await service.SubmitXmlAsync(user.GetBatchCaller(), body.Bytes!, SHA256.HashData(body.Bytes!), ct))
                .ToCreated(b => $"/api/batches/{b.BatchId}");
        }).Accepts<string>("application/xml", "text/xml");

        batches.MapPost("/validate", async (HttpRequest request, ClaimsPrincipal user, BatchSubmissionService service, CancellationToken ct) =>
        {
            var body = await ReadBodyAsync(request, ct);
            if (body.Error is not null)
                return body.Error;
            if (IsXml(request))
                return (await service.ValidateXmlAsync(user.GetBatchCaller(), body.Bytes!, ct)).ToHttp();
            if (Deserialize(request, body.Bytes!, out var batch) is { } invalid)
                return invalid;
            return (await service.ValidateAsync(user.GetBatchCaller(), batch!, ct)).ToHttp();
        }).Accepts<BatchSubmission>("application/json", "application/xml", "text/xml");

        batches.MapGet("/", async (ClaimsPrincipal user, BatchQueries queries, DateOnly? from, DateOnly? to, string? uniqueReference, int? page, int? pageSize,
                CancellationToken ct)
            => (await queries.ListBatchesAsync(user.GetBatchCaller(),
                new BatchListQuery(from, to, uniqueReference, page ?? 1, pageSize ?? BatchListQuery.DefaultPageSize), ct)).ToHttp());

        batches.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, BatchQueries queries, int? page, int? pageSize, CancellationToken ct)
            => (await queries.GetBatchAsync(user.GetBatchCaller(), id, page ?? 1, pageSize ?? 500, ct)).ToHttp());

        batches.MapGet("/{id:int}/rejections", async (int id, ClaimsPrincipal user, BatchQueries queries, CancellationToken ct)
            => (await queries.GetRejectionsAsync(user.GetBatchCaller(), id, ct)).ToHttp());

        return app;
    }

    private static bool IsXml(HttpRequest request)
        => request.ContentType is { } type && (type.Contains("application/xml", StringComparison.OrdinalIgnoreCase)
                                               || type.Contains("text/xml", StringComparison.OrdinalIgnoreCase));

    private static async Task<(byte[]? Bytes, IResult? Error)> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        var limit = request.HttpContext.RequestServices.GetRequiredService<IOptions<BatchFileOptions>>().Value.MaxRequestBytes;
        if (request.ContentLength > limit)
            return (null, TooLarge(limit));
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > limit)
                return (null, TooLarge(limit));
            buffer.Write(chunk, 0, read);
        }
        return buffer.Length == 0
            ? (null, TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["The request body is empty."] }))
            : (buffer.ToArray(), null);
    }

    private static IResult TooLarge(long limit)
        => TypedResults.Problem($"The request body may be at most {limit} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

    private static ValidationProblem? Deserialize(HttpRequest request, byte[] body, out BatchSubmission? batch)
    {
        var options = request.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        try
        {
            batch = JsonSerializer.Deserialize<BatchSubmission>(body, options);
        }
        catch (JsonException ex)
        {
            batch = null;
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [ex.Path ?? "body"] = [ex.Message.Split(" Path:")[0]] });
        }
        return batch is null ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["The request body is empty."] }) : null;
    }
}
