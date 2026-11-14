using PayOps.BatchFile.Data;

namespace PayOps.BatchFile.Api.Endpoints;

internal static class ResultMapping
{
    public static IResult ToHttp<T>(this BatchResult<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToHttp();

    public static IResult ToCreated<T>(this BatchResult<T> result, Func<T, string> location)
        => result.IsSuccess ? TypedResults.Created(location(result.Value!), result.Value) : result.Error!.ToHttp();

    public static IResult ToHttp(this BatchError error) => error.Kind switch
    {
        BatchErrorKind.Validation => TypedResults.ValidationProblem(
            error.Errors?.ToDictionary(e => e.Key, e => e.Value) ?? [], detail: error.Message),
        BatchErrorKind.NotFound => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status404NotFound),
        _ => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status409Conflict,
            extensions: error.ExistingBatchId is { } id ? new Dictionary<string, object?> { ["batchId"] = id } : null)
    };
}
