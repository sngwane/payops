using System.Text.RegularExpressions;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data.Services;

/// <summary>Field and business rules for a batch, ported from legacy BulkPaymentModule.UploadBatch and the XmlObjects field validators.</summary>
internal static partial class BatchRules
{
    [GeneratedRegex(@"^[a-zA-Z0-9 \-_().]*$")]
    private static partial Regex PlainText();

    [GeneratedRegex(@"^[0-9]+$")]
    private static partial Regex Digits();

    public static ServiceTypeCode? ParseServiceType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "SDV" or "SAMEDAY" or "SAME DAY" => ServiceTypeCode.SDV,
        "RTL" => ServiceTypeCode.RTL,
        "1 DAY" or "1DAY" or "ONEDAY" or "ONE DAY" => ServiceTypeCode.OneDay,
        "2 DAY" or "2DAY" or "TWODAY" or "TWO DAY" => ServiceTypeCode.TwoDay,
        "DW" => ServiceTypeCode.DW,
        _ => null
    };

    /// <summary>Required fields, lengths, characters, number formats and allowed service types.</summary>
    public static void CheckFields(BatchSubmission batch, BatchFileOptions options, ValidationErrors errors)
    {
        if (batch.Header is null)
            errors.Add("header", "Header is required.");
        else
            CheckHeader(batch.Header, options, errors);

        if (batch.Payments is null || batch.Payments.Count == 0)
            errors.Add("payments", "At least one payment is required.");
        else if (batch.Payments.Count > options.MaxRecords)
            errors.Add("payments", $"A batch may contain at most {options.MaxRecords} payments.");
        else
            for (var i = 0; i < batch.Payments.Count; i++)
                CheckPayment($"payments[{i}]", batch.Payments[i], errors);

        if (batch.Footer is null)
            errors.Add("footer", "Footer is required.");
        else
        {
            if (batch.Footer.Records < 0)
                errors.Add("footer.records", "Records may not be negative.");
            CheckAmount("footer.totalValue", batch.Footer.TotalValue, errors, allowZero: true);
        }

        if (batch.Notifications is { } notifications)
        {
            CheckContacts("notifications.uploadConfirmations", notifications.UploadConfirmations, errors);
            CheckContacts("notifications.rejectionConfirmations", notifications.RejectionConfirmations, errors);
            CheckContacts("notifications.fundingConfirmations", notifications.FundingConfirmations, errors);
        }
    }

    private static void CheckHeader(BatchHeader h, BatchFileOptions options, ValidationErrors e)
    {
        Text(e, "header.uniqueClientId", h.UniqueClientId, 50, required: true, plain: false);
        Text(e, "header.rejectionAccountName", h.RejectionAccountName, 30, required: true);
        Number(e, "header.rejectionBranchCode", h.RejectionBranchCode, 6, fixedLength: true);
        Number(e, "header.rejectionAccountNumber", h.RejectionAccountNumber, 16);
        Text(e, "header.uniquePaymentReference", h.UniquePaymentReference, 20, required: true);
        if (h.SequenceNumber <= 0)
            e.Add("header.sequenceNumber", "Sequence number must be a positive number.");
        if (string.IsNullOrWhiteSpace(h.ServiceType))
            e.Add("header.serviceType", "Service type is required.");
        else if (ParseServiceType(h.ServiceType) is not { } type || !options.EffectiveServiceTypes.Contains(type))
            e.Add("header.serviceType", $"Invalid service type. Allowed: {string.Join(", ", options.EffectiveServiceTypes)}.");
    }

    private static void CheckPayment(string path, BatchPaymentLine? p, ValidationErrors e)
    {
        if (p is null)
        {
            e.Add(path, "Payment is required.");
            return;
        }
        Text(e, $"{path}.employeeCode", p.EmployeeCode, 50, required: true, plain: false);
        Text(e, $"{path}.surname", p.Surname, 50, required: true);
        Text(e, $"{path}.firstName", p.FirstName, 50);
        Text(e, $"{path}.initials", p.Initials, 3);
        Text(e, $"{path}.idOrPassport", p.IdOrPassport, 50);
        Text(e, $"{path}.cell", p.Cell, 13);
        Text(e, $"{path}.email", p.Email, 50, plain: false);
        Number(e, $"{path}.branchCode", p.BranchCode, 6, fixedLength: true);
        Number(e, $"{path}.accountNumber", p.AccountNumber, 16);
        Text(e, $"{path}.accountType", p.AccountType, 10, required: true);
        CheckAmount($"{path}.amount", p.Amount, e);
        Text(e, $"{path}.bankReference", p.BankReference, 20, required: true);
    }

    private static void CheckContacts(string path, IReadOnlyList<BatchContactInfo>? contacts, ValidationErrors e)
    {
        if (contacts is null)
            return;
        for (var i = 0; i < contacts.Count; i++)
        {
            var c = contacts[i];
            if (c is null)
            {
                e.Add($"{path}[{i}]", "Contact is required.");
                continue;
            }
            Text(e, $"{path}[{i}].name", c.Name, 50, required: true, plain: false);
            Text(e, $"{path}[{i}].email", c.Email, 100, plain: false);
            if (!string.IsNullOrEmpty(c.Email) && !c.Email.Contains('@'))
                e.Add($"{path}[{i}].email", "Email is not valid.");
            if (!string.IsNullOrEmpty(c.CellNumber))
                Number(e, $"{path}[{i}].cellNumber", c.CellNumber, 15);
            if (string.IsNullOrWhiteSpace(c.Email) && string.IsNullOrWhiteSpace(c.CellNumber))
                e.Add($"{path}[{i}]", "An email or a cell number is required.");
        }
    }

    private static void Text(ValidationErrors e, string field, string? value, int maxLength, bool required = false, bool plain = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
                e.Add(field, "Value is required.");
            return;
        }
        if (value.Length > maxLength)
            e.Add(field, $"Must be {maxLength} characters or fewer.");
        else if (plain && !PlainText().IsMatch(value))
            e.Add(field, "Only letters, digits, spaces and - _ ( ) . are allowed.");
    }

    private static void Number(ValidationErrors e, string field, string? value, int maxLength, bool fixedLength = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            e.Add(field, "Value is required.");
        else if (!Digits().IsMatch(value))
            e.Add(field, "Only digits are allowed.");
        else if (fixedLength && value.Length != maxLength)
            e.Add(field, $"Must be exactly {maxLength} digits.");
        else if (value.Length > maxLength)
            e.Add(field, $"Must be {maxLength} digits or fewer.");
    }

    private static void CheckAmount(string field, decimal value, ValidationErrors e, bool allowZero = false)
    {
        if (value < 0 || (!allowZero && value == 0))
            e.Add(field, allowZero ? "Amount may not be negative." : "Amount must be greater than zero.");
        else if (decimal.Round(value, 2) != value)
            e.Add(field, "Amount may have at most 2 decimal places.");
        else if (value >= 10_000_000_000_000_000m)
            e.Add(field, "Amount is too large.");
    }

    /// <summary>
    /// Payment-date rules: SDV/RTL/DW not in the past, OneDay after today, TwoDay at least two business days ahead,
    /// and never a Sunday or public holiday.
    /// </summary>
    public static string? CheckPaymentDate(ServiceTypeCode type, DateOnly paymentDate, DateOnly today, IReadOnlySet<DateOnly> holidays)
    {
        if (paymentDate.DayOfWeek == DayOfWeek.Sunday || holidays.Contains(paymentDate))
            return "Payment date may not be a Sunday or a public holiday.";
        return type switch
        {
            ServiceTypeCode.OneDay when paymentDate <= today => "OneDay payments must be dated after today.",
            ServiceTypeCode.TwoDay when paymentDate < BusinessCalendar.AddBusinessDays(today, 2, holidays)
                => $"TwoDay payments must be dated on or after {BusinessCalendar.AddBusinessDays(today, 2, holidays):yyyy-MM-dd} (two business days).",
            _ when paymentDate < today => "Payment date may not be in the past.",
            _ => null
        };
    }
}

/// <summary>Business days are Monday to Friday, excluding public holidays.</summary>
public static class BusinessCalendar
{
    public static bool IsBusinessDay(DateOnly date, IReadOnlySet<DateOnly> holidays)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date);

    public static DateOnly AddBusinessDays(DateOnly from, int days, IReadOnlySet<DateOnly> holidays)
    {
        var date = from;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date, holidays))
                days--;
        }
        return date;
    }
}
