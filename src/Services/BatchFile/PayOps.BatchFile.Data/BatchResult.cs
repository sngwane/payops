namespace PayOps.BatchFile.Data;

public enum BatchErrorKind { Validation, NotFound, Conflict }

public sealed record BatchError(BatchErrorKind Kind, string Message, IReadOnlyDictionary<string, string[]>? Errors = null, int? ExistingBatchId = null)
{
    public static BatchError NotFound(string message) => new(BatchErrorKind.NotFound, message);
    public static BatchError Conflict(string message, int? existingBatchId = null) => new(BatchErrorKind.Conflict, message, null, existingBatchId);
    public static BatchError Invalid(IReadOnlyDictionary<string, string[]> errors) => new(BatchErrorKind.Validation, "One or more validation errors occurred.", errors);
    public static BatchError Invalid(string field, string message) => Invalid(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Outcome of a batch command: a value or an error.</summary>
public sealed class BatchResult<T>
{
    private BatchResult(T? value, BatchError? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public BatchError? Error { get; }
    public bool IsSuccess => Error is null;

    public static implicit operator BatchResult<T>(T value) => new(value, null);
    public static implicit operator BatchResult<T>(BatchError error) => new(default, error);
}

internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = [];
        list.Add(message);
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() => _errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
}
