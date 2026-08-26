using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OcrAiVision.Application.Documents.Ports;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using OcrAiVision.Infrastructure.DocumentIntelligence;
using Xunit;

namespace OcrAiVision.Infrastructure.UnitTests.DocumentIntelligence;

/// <summary>
/// Exercises the Azure adapter against a mocked <see cref="DocumentIntelligenceClient"/>.
/// The SDK client exposes a protected parameterless constructor and virtual
/// methods precisely so it can be substituted this way, which lets the
/// translation and the failure classification be tested without a live resource.
/// </summary>
public sealed class AzureDocumentIntelligenceAnalyzerTests
{
    private readonly Mock<DocumentIntelligenceClient> _client = new();

    private AzureDocumentIntelligenceAnalyzer CreateAnalyzer() =>
        new(_client.Object, NullLogger<AzureDocumentIntelligenceAnalyzer>.Instance);

    [Fact]
    public async Task A_layout_response_is_translated_into_the_domain_aggregate()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.LayoutResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(RequestFor("order.pdf"), CancellationToken.None);

        Assert.Equal("order.pdf", analysis.SourceFileName);
        Assert.Equal("prebuilt-layout", analysis.ModelId.Value);
        Assert.Equal("PURCHASE ORDER\nItem Qty\nWidget 3", analysis.Content);
        Assert.Equal(2, analysis.PageCount);
    }

    [Fact]
    public async Task Page_text_is_rebuilt_from_the_lines_the_service_reported()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.LayoutResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(RequestFor("order.pdf"), CancellationToken.None);

        var first = analysis.Pages[0];
        Assert.Equal(1, first.Number.Value);
        Assert.Equal($"PURCHASE ORDER{Environment.NewLine}Item Qty", first.Content);
        Assert.Equal(2, first.LineCount);
        Assert.Equal(4, first.WordCount);
    }

    [Fact]
    public async Task Tables_keep_their_shape_and_their_header_cells()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.LayoutResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(RequestFor("order.pdf"), CancellationToken.None);

        var table = Assert.Single(analysis.Tables);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(2, table.Page!.Value);
        Assert.Equal(4, table.Cells.Count);
        Assert.Equal(2, table.Cells.Count(cell => cell.IsHeader));
        Assert.Equal("Widget", table.Cells.Single(cell => cell is { RowIndex: 1, ColumnIndex: 0 }).Content);
    }

    [Fact]
    public async Task Key_value_pairs_become_extracted_fields()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.LayoutResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(RequestFor("order.pdf"), CancellationToken.None);

        var field = Assert.Single(analysis.Fields);
        Assert.Equal("Order Number", field.Name);
        Assert.Equal("PO-4471", field.Value);
        Assert.Equal(91, field.Confidence.Percentage);
        Assert.Equal(1, field.Page!.Value);
    }

    [Fact]
    public async Task Typed_invoice_fields_are_rendered_as_text()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.InvoiceResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(
            RequestFor("invoice.pdf", "prebuilt-invoice"),
            CancellationToken.None);

        var fields = analysis.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

        Assert.Equal("Acme Trading", fields["VendorName"].Value);
        Assert.Equal("ZAR 1200.5", fields["InvoiceTotal"].Value);
        Assert.StartsWith("2026-03-14", fields["InvoiceDate"].Value, StringComparison.Ordinal);
        Assert.Equal("True", fields["IsPaid"].Value);
        Assert.Equal("3", fields["PageCount"].Value);
    }

    [Fact]
    public async Task A_field_the_service_gave_no_confidence_for_is_scored_at_the_minimum()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.InvoiceResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(
            RequestFor("invoice.pdf", "prebuilt-invoice"),
            CancellationToken.None);

        var field = analysis.Fields.Single(candidate => candidate.Name == "PageCount");

        Assert.Equal(ConfidenceScore.Minimum, field.Confidence);
    }

    [Fact]
    public async Task A_field_bounding_region_becomes_the_page_it_was_found_on()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.InvoiceResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(
            RequestFor("invoice.pdf", "prebuilt-invoice"),
            CancellationToken.None);

        Assert.Equal(1, analysis.Fields.Single(field => field.Name == "VendorName").Page!.Value);
        Assert.Null(analysis.Fields.Single(field => field.Name == "InvoiceTotal").Page);
    }

    [Fact]
    public async Task An_empty_response_produces_an_empty_but_valid_aggregate()
    {
        GivenTheServiceReturns(AnalyzeResultFixtures.EmptyResponse);

        var analysis = await CreateAnalyzer().AnalyzeAsync(
            RequestFor("blank.pdf", "prebuilt-read"),
            CancellationToken.None);

        Assert.False(analysis.HasContent);
        Assert.Empty(analysis.Pages);
        Assert.Empty(analysis.Tables);
        Assert.Empty(analysis.Fields);
        Assert.Null(analysis.LowestFieldConfidence());
    }

    [Fact]
    public async Task The_requested_model_and_the_uploaded_bytes_reach_the_service()
    {
        AnalyzeDocumentOptions? captured = null;

        _client
            .Setup(client => client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                It.IsAny<AnalyzeDocumentOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<WaitUntil, AnalyzeDocumentOptions, CancellationToken>((_, options, _) => captured = options)
            .ReturnsAsync(new FakeAnalyzeOperation(AnalyzeResultFixtures.FromJson(AnalyzeResultFixtures.EmptyResponse)));

        await CreateAnalyzer().AnalyzeAsync(
            RequestFor("scan.pdf", "prebuilt-receipt", "the file bytes"),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("prebuilt-receipt", captured.ModelId);
        Assert.Equal("the file bytes", captured.BytesSource.ToString());
    }

    [Fact]
    public async Task The_caller_cancellation_token_is_handed_to_the_service()
    {
        using var source = new CancellationTokenSource();

        _client
            .Setup(client => client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                It.IsAny<AnalyzeDocumentOptions>(),
                source.Token))
            .ReturnsAsync(new FakeAnalyzeOperation(AnalyzeResultFixtures.FromJson(AnalyzeResultFixtures.EmptyResponse)));

        await CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), source.Token);

        _client.Verify(
            client => client.AnalyzeDocumentAsync(WaitUntil.Completed, It.IsAny<AnalyzeDocumentOptions>(), source.Token),
            Times.Once);
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    public async Task A_service_failure_is_classified_by_whether_a_retry_could_help(int status, bool expectedTransient)
    {
        GivenTheServiceThrows(new RequestFailedException(status, "service said no"));

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), CancellationToken.None));

        Assert.Equal(expectedTransient, exception.IsTransient);
    }

    [Fact]
    public async Task A_service_failure_keeps_the_original_exception_for_diagnostics()
    {
        var failure = new RequestFailedException(429, "Too Many Requests");
        GivenTheServiceThrows(failure);

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), CancellationToken.None));

        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task An_unknown_model_is_reported_with_the_model_that_was_asked_for()
    {
        GivenTheServiceThrows(new RequestFailedException(404, "Not Found"));

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf", "prebuilt-nonsense"), CancellationToken.None));

        Assert.Contains("prebuilt-nonsense", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_credential_failure_does_not_leak_the_service_message_to_the_caller()
    {
        GivenTheServiceThrows(new RequestFailedException(401, "Access denied due to invalid subscription key sk-live-xyz"));

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), CancellationToken.None));

        Assert.DoesNotContain("sk-live-xyz", exception.Message, StringComparison.Ordinal);
        Assert.Contains("credentials", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_credential_that_cannot_be_acquired_is_reported_as_a_permanent_failure()
    {
        // Token acquisition fails before a request is ever sent, so this never
        // arrives as a RequestFailedException.
        GivenTheServiceThrows(new CredentialUnavailableException("no credential was available"));

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.Contains("credential", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_credential_failure_does_not_leak_the_underlying_diagnostics_to_the_caller()
    {
        GivenTheServiceThrows(new AuthenticationFailedException(
            "ManagedIdentityCredential failed: probe http://169.254.169.254/metadata/identity"));

        var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() =>
            CreateAnalyzer().AnalyzeAsync(RequestFor("scan.pdf"), CancellationToken.None));

        Assert.DoesNotContain("169.254.169.254", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void The_adapter_refuses_to_be_built_without_its_collaborators()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureDocumentIntelligenceAnalyzer(null!, NullLogger<AzureDocumentIntelligenceAnalyzer>.Instance));

        Assert.Throws<ArgumentNullException>(() =>
            new AzureDocumentIntelligenceAnalyzer(_client.Object, null!));
    }

    private void GivenTheServiceReturns(string json) =>
        _client
            .Setup(client => client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                It.IsAny<AnalyzeDocumentOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FakeAnalyzeOperation(AnalyzeResultFixtures.FromJson(json)));

    private void GivenTheServiceThrows(Exception exception) =>
        _client
            .Setup(client => client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                It.IsAny<AnalyzeDocumentOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

    private static DocumentAnalysisRequest RequestFor(
        string fileName,
        string modelId = "prebuilt-layout",
        string content = "%PDF-1.7")
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        var upload = DocumentUpload.Create(
            fileName,
            DocumentMediaType.Create("application/pdf"),
            FileSize.FromBytes(bytes.Length),
            () => new MemoryStream(bytes));

        return new DocumentAnalysisRequest(upload, ModelId.Create(modelId));
    }
}
