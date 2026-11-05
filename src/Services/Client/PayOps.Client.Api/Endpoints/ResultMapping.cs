using PayOps.Client.Data;

namespace PayOps.Client.Api.Endpoints;

internal static class ResultMapping
{
    public static IResult ToHttp<T>(this ClientResult<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToHttp();

    public static IResult ToCreated<T>(this ClientResult<T> result, Func<T, string> location)
        => result.IsSuccess ? TypedResults.Created(location(result.Value!), result.Value) : result.Error!.ToHttp();

    private static IResult ToHttp(this ClientError error) => error.Kind switch
    {
        ClientErrorKind.Validation => TypedResults.ValidationProblem(
            error.Errors?.ToDictionary(e => e.Key, e => e.Value) ?? [], detail: error.Message),
        ClientErrorKind.NotFound => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status404NotFound),
        _ => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status409Conflict)
    };
}
