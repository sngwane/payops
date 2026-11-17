namespace PayOps.Payments.Data;

public enum PaymentsErrorKind { Validation, NotFound, Conflict }

public sealed record PaymentsError(PaymentsErrorKind Kind, string Message, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PaymentsError NotFound(string message) => new(PaymentsErrorKind.NotFound, message);
    public static PaymentsError Conflict(string message) => new(PaymentsErrorKind.Conflict, message);
    public static PaymentsError Invalid(IReadOnlyDictionary<string, string[]> errors) => new(PaymentsErrorKind.Validation, "One or more validation errors occurred.", errors);
    public static PaymentsError Invalid(string field, string message) => Invalid(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Outcome of a Payments API query or command: a value or an error.</summary>
public sealed class PaymentsResult<T>
{
    private PaymentsResult(T? value, PaymentsError? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public PaymentsError? Error { get; }
    public bool IsSuccess => Error is null;

    public static PaymentsResult<T> Ok(T value) => new(value, null);

    public static implicit operator PaymentsResult<T>(T value) => Ok(value);
    public static implicit operator PaymentsResult<T>(PaymentsError error) => new(default, error);
}
