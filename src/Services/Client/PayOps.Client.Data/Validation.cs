namespace PayOps.Client.Data;

/// <summary>Collects field errors for one request; field names match the request's JSON property names.</summary>
internal sealed class Validation
{
    public const int UniqueClientIdMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int LogoFileNameMaxLength = 255;
    public const int BranchCodeMaxLength = 10;
    public const int AccountNumberMaxLength = 20;

    private readonly Dictionary<string, List<string>> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public ClientError ToError()
        => ClientError.Invalid(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = [];
        list.Add(message);
    }

    public string Required(string field, string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            Add(field, "Required.");
        else if (trimmed.Length > maxLength)
            Add(field, $"Must be at most {maxLength} characters.");
        return trimmed;
    }

    public string? Optional(string field, string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            Add(field, $"Must be at most {maxLength} characters.");
        return trimmed;
    }

    public string UniqueClientId(string field, string? value)
    {
        var id = Required(field, value, UniqueClientIdMaxLength);
        if (id.Any(char.IsWhiteSpace))
            Add(field, "Must not contain spaces.");
        return id;
    }

    public string Digits(string field, string? value, int maxLength)
    {
        var digits = Required(field, value, maxLength);
        if (digits.Length > 0 && !digits.All(char.IsAsciiDigit))
            Add(field, "Must contain digits only.");
        return digits;
    }

    public byte[] RowVersion(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, "Required.");
            return [];
        }
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            Add(field, "Is not a valid row version.");
            return [];
        }
    }

    public static ClientError? Paging(int page, int pageSize)
    {
        var v = new Validation();
        if (page < 1)
            v.Add("page", "Must be 1 or more.");
        if (pageSize is < 1 or > ClientListQuery.MaxPageSize)
            v.Add("pageSize", $"Must be between 1 and {ClientListQuery.MaxPageSize}.");
        return v.IsValid ? null : v.ToError();
    }
}
