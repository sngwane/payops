namespace PayOps.Client.Data;

public enum ClientErrorKind { Validation, NotFound, Conflict }

public sealed record ClientError(ClientErrorKind Kind, string Message, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ClientError NotFound(string message) => new(ClientErrorKind.NotFound, message);
    public static ClientError Conflict(string message) => new(ClientErrorKind.Conflict, message);
    public static ClientError Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ClientErrorKind.Validation, "One or more validation errors occurred.", errors);
}

/// <summary>Outcome of a client command: a value or an error.</summary>
public sealed class ClientResult<T>
{
    private ClientResult(T? value, ClientError? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public ClientError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ClientResult<T> Ok(T value) => new(value, null);

    public static implicit operator ClientResult<T>(T value) => Ok(value);
    public static implicit operator ClientResult<T>(ClientError error) => new(default, error);
}
