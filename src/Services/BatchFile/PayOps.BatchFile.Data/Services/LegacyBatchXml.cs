using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace PayOps.BatchFile.Data.Services;

/// <summary>Reads the legacy WCF <c>BulkPaymentXml</c> format: validated against the legacy XSD, then mapped to <see cref="BatchSubmission"/>.</summary>
public static class LegacyBatchXml
{
    private static readonly XmlSchemaSet Schemas = LoadSchemas();
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy/MM/dd HH:mm:ss"];

    public static (BatchSubmission? Batch, IReadOnlyDictionary<string, string[]>? Errors) Parse(string xml)
    {
        var errors = new ValidationErrors();
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                ValidationType = ValidationType.Schema,
                Schemas = Schemas
            };
            settings.ValidationEventHandler += (_, e) => errors.Add("xml", $"Line {e.Exception.LineNumber}: {e.Message}");
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            errors.Add("xml", ex.Message);
            return (null, errors.ToDictionary());
        }
        if (errors.Any)
            return (null, errors.ToDictionary());

        var root = document.Root!;
        var header = root.Element("HeaderInfo");
        var footer = root.Element("FooterInfo");
        var payments = root.Element("Payments")?.Elements("Payment").ToList() ?? [];
        if (header is null)
            errors.Add("header", "HeaderInfo is required.");
        if (footer is null)
            errors.Add("footer", "FooterInfo is required.");
        if (payments.Count == 0)
            errors.Add("payments", "At least one Payment is required.");
        if (errors.Any)
            return (null, errors.ToDictionary());

        var batchHeader = new BatchHeader(
            Value(header!, "UniqueClientId")!,
            Date(errors, "header.paymentDate", Value(header!, "PaymentDate")),
            Value(header!, "RejectionAccountName")!,
            Value(header!, "RejectionBranchCode")!,
            Value(header!, "RejectionAccountNumber")!,
            Value(header!, "UniquePaymentReference")!,
            Value(header!, "ServiceType")!,
            Long(errors, "header.sequenceNumber", Value(header!, "SequenceNumber")));

        var lines = payments.Select((p, i) => new BatchPaymentLine(
            Value(p, "EmployeeCode")!, Value(p, "EmployeeSurname")!, Value(p, "EmployeeFirstname"), Value(p, "EmployeeInitials"),
            Value(p, "EmployeeIdOrPassport"), Value(p, "EmployeeCell"), Value(p, "EmployeeEmail"), Value(p, "BranchCode")!,
            Value(p, "AccountNumber")!, Value(p, "AccountType")!, Amount(errors, $"payments[{i}].amount", Value(p, "Amount")),
            Value(p, "BankReference")!)).ToList();

        var batchFooter = new BatchFooter(
            Date(errors, "footer.currentDate", Value(footer!, "CurrentDate")),
            Int(errors, "footer.records", Value(footer!, "Records")),
            Amount(errors, "footer.totalValue", Value(footer!, "TotalValue")));

        var notifications = root.Element("NotificationInfo") is { } info
            ? new BatchNotifications(Contacts(info, "UploadConfirmations"), Contacts(info, "RejectionConfirmations"), Contacts(info, "FundingConfirmations"))
            : null;

        return errors.Any ? (null, errors.ToDictionary()) : (new BatchSubmission(batchHeader, lines, batchFooter, notifications), null);
    }

    private static List<BatchContactInfo> Contacts(XElement info, string section)
        => info.Elements(section).SelectMany(s => s.Elements("Contact"))
            .Select(c => new BatchContactInfo(Empty((string?)c.Attribute("Name"))!, Empty((string?)c.Attribute("Email")), Empty((string?)c.Attribute("CellNumber"))))
            .ToList();

    private static string? Value(XElement parent, string name) => Empty(parent.Element(name)?.Value);

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly Date(ValidationErrors errors, string field, string? value)
    {
        if (value is not null && DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return DateOnly.FromDateTime(date);
        errors.Add(field, "Must be a date in the format yyyy-MM-dd.");
        return default;
    }

    private static decimal Amount(ValidationErrors errors, string field, string? value)
    {
        if (value is not null && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return amount;
        errors.Add(field, "Must be a number such as 1234.56.");
        return 0;
    }

    private static int Int(ValidationErrors errors, string field, string? value)
    {
        if (value is not null && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return number;
        errors.Add(field, "Must be a whole number.");
        return 0;
    }

    private static long Long(ValidationErrors errors, string field, string? value)
    {
        if (value is not null && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return number;
        errors.Add(field, "Must be a whole number.");
        return 0;
    }

    private static XmlSchemaSet LoadSchemas()
    {
        using var stream = typeof(LegacyBatchXml).Assembly.GetManifestResourceStream("BulkPaymentXml.xsd")
            ?? throw new InvalidOperationException("Embedded BulkPaymentXml.xsd not found.");
        var set = new XmlSchemaSet { XmlResolver = null };
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        set.Add(null, reader);
        set.Compile();
        return set;
    }
}
