using PayOps.Payments.Data;

namespace PayOps.Payments.Api.Endpoints;

internal static class ResultMapping
{
    public static IResult ToHttp<T>(this PaymentsResult<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToHttp();

    public static IResult ToCsv<T>(this PaymentsResult<IAsyncEnumerable<T>> result, string fileName)
        => result.IsSuccess ? new CsvResult<T>(result.Value!, fileName) : result.Error!.ToHttp();

    public static IResult ToHttp(this PaymentsError error) => error.Kind switch
    {
        PaymentsErrorKind.Validation => TypedResults.ValidationProblem(
            error.Errors?.ToDictionary(e => e.Key, e => e.Value) ?? [], detail: error.Message),
        PaymentsErrorKind.NotFound => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status404NotFound),
        _ => TypedResults.Problem(error.Message, statusCode: StatusCodes.Status409Conflict)
    };

    public static IResult NoStaffIdentity()
        => TypedResults.Problem("The sign-in token has no user object id (oid), so the change can't be recorded against a user.", statusCode: StatusCodes.Status403Forbidden);
}
