using System.Globalization;
using Azure.AI.DocumentIntelligence;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using AzureDocumentPage = Azure.AI.DocumentIntelligence.DocumentPage;
using AzureDocumentTable = Azure.AI.DocumentIntelligence.DocumentTable;
using AzureDocumentTableCell = Azure.AI.DocumentIntelligence.DocumentTableCell;
using DomainAnalyzedDocument = OcrAiVision.Domain.Documents.AnalyzedDocument;
using DomainDocumentPage = OcrAiVision.Domain.Documents.DocumentPage;
using DomainDocumentTable = OcrAiVision.Domain.Documents.DocumentTable;
using DomainDocumentTableCell = OcrAiVision.Domain.Documents.DocumentTableCell;

namespace OcrAiVision.Infrastructure.DocumentIntelligence;

/// <summary>
/// Translates the Azure SDK's <see cref="AnalyzeResult"/> into the domain
/// aggregate. This is the anti-corruption layer: it is the only place in the
/// solution that understands the vendor's shape, so swapping the provider means
/// rewriting this file and nothing else.
/// </summary>
internal static class AnalyzeResultTranslator
{
    /// <summary>
    /// Builds the domain aggregate from a service response.
    /// </summary>
    /// <param name="result">The response from the analysis service.</param>
    /// <param name="sourceFileName">The name of the file that was analysed.</param>
    /// <param name="requestedModelId">The model the caller asked for, used when the service echoes none.</param>
    public static DomainAnalyzedDocument ToDomain(
        this AnalyzeResult result,
        string sourceFileName,
        ModelId requestedModelId)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(requestedModelId);

        return DomainAnalyzedDocument.Create(
            sourceFileName,
            string.IsNullOrWhiteSpace(result.ModelId) ? requestedModelId : ModelId.Create(result.ModelId),
            result.Content,
            result.Pages?.Select(ToDomain),
            result.Tables?.Select(ToDomain),
            ExtractFields(result));
    }

    private static DomainDocumentPage ToDomain(AzureDocumentPage page) =>
        DomainDocumentPage.Create(
            PageNumber.Create(page.PageNumber),
            JoinLines(page),
            page.Lines?.Count ?? 0,
            page.Words?.Count ?? 0);

    private static DomainDocumentTable ToDomain(AzureDocumentTable table) =>
        DomainDocumentTable.Create(
            table.RowCount,
            table.ColumnCount,
            (table.Cells ?? []).Select(ToDomain),
            FirstPageOf(table.BoundingRegions));

    private static DomainDocumentTableCell ToDomain(AzureDocumentTableCell cell) =>
        DomainDocumentTableCell.Create(
            cell.RowIndex,
            cell.ColumnIndex,
            cell.Content,
            cell.Kind == DocumentTableCellKind.ColumnHeader || cell.Kind == DocumentTableCellKind.RowHeader);

    /// <summary>
    /// Collects labelled values from both shapes the service uses: the typed
    /// fields a prebuilt or custom model returns, and the key/value pairs the
    /// layout model returns.
    /// </summary>
    private static IEnumerable<ExtractedField> ExtractFields(AnalyzeResult result)
    {
        foreach (var document in result.Documents ?? [])
        {
            foreach (var (name, field) in document.Fields ?? Enumerable.Empty<KeyValuePair<string, DocumentField>>())
            {
                yield return ExtractedField.Create(
                    name,
                    Describe(field),
                    ToConfidence(field.Confidence),
                    FirstPageOf(field.BoundingRegions));
            }
        }

        foreach (var pair in result.KeyValuePairs ?? [])
        {
            if (string.IsNullOrWhiteSpace(pair.Key?.Content))
            {
                continue;
            }

            yield return ExtractedField.Create(
                pair.Key.Content,
                pair.Value?.Content,
                ToConfidence(pair.Confidence),
                FirstPageOf(pair.Key.BoundingRegions));
        }
    }

    /// <summary>
    /// Renders a typed field as text. The service populates exactly one of the
    /// <c>Value*</c> properties according to <see cref="DocumentField.FieldType"/>;
    /// <see cref="DocumentField.Content"/> is the raw span it was read from and
    /// is the best answer whenever it is present.
    /// </summary>
    private static string? Describe(DocumentField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Content))
        {
            return field.Content;
        }

        var type = field.FieldType;

        if (type == DocumentFieldType.String) return field.ValueString;
        if (type == DocumentFieldType.Date) return field.ValueDate?.ToString("O", CultureInfo.InvariantCulture);
        if (type == DocumentFieldType.Time) return field.ValueTime?.ToString(null, CultureInfo.InvariantCulture);
        if (type == DocumentFieldType.PhoneNumber) return field.ValuePhoneNumber;
        if (type == DocumentFieldType.Double) return field.ValueDouble?.ToString(CultureInfo.InvariantCulture);
        if (type == DocumentFieldType.Int64) return field.ValueInt64?.ToString(CultureInfo.InvariantCulture);
        if (type == DocumentFieldType.Boolean) return field.ValueBoolean?.ToString(CultureInfo.InvariantCulture);
        if (type == DocumentFieldType.CountryRegion) return field.ValueCountryRegion;
        if (type == DocumentFieldType.SelectionMark) return field.ValueSelectionMark?.ToString();
        if (type == DocumentFieldType.Signature) return field.ValueSignature?.ToString();
        if (type == DocumentFieldType.Currency) return DescribeCurrency(field);
        if (type == DocumentFieldType.Address) return field.ValueAddress?.ToString();
        if (type == DocumentFieldType.SelectionGroup) return JoinOrNull(field.ValueSelectionGroup);
        if (type == DocumentFieldType.List) return JoinOrNull(field.ValueList?.Select(Describe));

        return null;
    }

    private static string? DescribeCurrency(DocumentField field)
    {
        var currency = field.ValueCurrency;

        if (currency is null)
        {
            return null;
        }

        var amount = currency.Amount.ToString(CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(currency.CurrencyCode)
            ? amount
            : $"{currency.CurrencyCode} {amount}";
    }

    private static string? JoinOrNull(IEnumerable<string?>? values)
    {
        var present = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        return present is null || present.Count == 0 ? null : string.Join(", ", present);
    }

    private static string JoinLines(AzureDocumentPage page) =>
        page.Lines is null or { Count: 0 }
            ? string.Empty
            : string.Join(Environment.NewLine, page.Lines.Select(line => line.Content));

    /// <summary>
    /// Reads the page a bounding region refers to. Regions are structs, so an
    /// empty collection is distinguished by count rather than by null.
    /// </summary>
    private static PageNumber? FirstPageOf(IReadOnlyList<BoundingRegion>? regions) =>
        regions is null or { Count: 0 } ? null : PageNumber.Create(regions[0].PageNumber);

    /// <summary>
    /// Converts a single-precision service confidence into the domain value
    /// object. The widening cast can push a value such as 1.0f a hair past 1,
    /// so the result is clamped rather than allowed to break an otherwise
    /// perfectly good response.
    /// </summary>
    private static ConfidenceScore ToConfidence(float? confidence) =>
        confidence is null
            ? ConfidenceScore.Minimum
            : ConfidenceScore.Create(Math.Clamp((double)confidence.Value, 0d, 1d));
}
