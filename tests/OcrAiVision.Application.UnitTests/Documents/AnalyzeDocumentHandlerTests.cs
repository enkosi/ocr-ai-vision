using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OcrAiVision.Application.Abstractions;
using OcrAiVision.Application.Documents.AnalyzeDocument;
using OcrAiVision.Application.Documents.Ports;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;
using static OcrAiVision.Application.UnitTests.Documents.AnalyzeDocumentCommandBuilder;
using static OcrAiVision.Application.UnitTests.Documents.AnalyzedDocumentBuilder;

namespace OcrAiVision.Application.UnitTests.Documents;

/// <summary>
/// Exercises the interactor against a mocked <see cref="IDocumentAnalyzer"/>.
/// Because the port belongs to this layer, no Azure SDK type appears anywhere
/// in these tests — which is the point of owning the interface.
/// </summary>
public sealed class AnalyzeDocumentHandlerTests
{
    private readonly Mock<IDocumentAnalyzer> _analyzer = new(MockBehavior.Strict);
    private readonly DocumentUploadPolicy _policy = new(FileSize.FromMegabytes(10));

    private AnalyzeDocumentHandler CreateHandler(ILogger<AnalyzeDocumentHandler>? logger = null) =>
        new(_analyzer.Object, _policy, logger ?? NullLogger<AnalyzeDocumentHandler>.Instance);

    [Fact]
    public async Task A_valid_document_is_sent_to_the_analyzer_and_its_result_returned()
    {
        var analysis = AnAnalysis()
            .Of("invoice.pdf")
            .ByModel("prebuilt-invoice")
            .WithContent("INVOICE 42")
            .WithPage(1, "INVOICE 42", lines: 1, words: 2)
            .WithField("InvoiceTotal", "R 1 200.00", 0.88d, page: 1)
            .Build();

        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().UsingModel("prebuilt-invoice").Build(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("invoice.pdf", result.Value.FileName);
        Assert.Equal("prebuilt-invoice", result.Value.ModelId);
        Assert.Equal("INVOICE 42", result.Value.Content);
        Assert.Equal(1, result.Value.PageCount);
        Assert.Equal(0.88d, result.Value.LowestFieldConfidence);

        var field = Assert.Single(result.Value.Fields);
        Assert.Equal("InvoiceTotal", field.Name);
        Assert.Equal("R 1 200.00", field.Value);
        Assert.Equal(1, field.Page);
    }

    [Fact]
    public async Task The_upload_handed_to_the_analyzer_describes_the_command()
    {
        DocumentAnalysisRequest? captured = null;

        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentAnalysisRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(AnAnalysis().Build());

        await CreateHandler().ExecuteAsync(
            AValidCommand()
                .Named("  statement.pdf  ")
                .OfType("application/pdf; charset=binary")
                .Sized(2_048)
                .UsingModel("prebuilt-read")
                .Build(),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("statement.pdf", captured.Upload.FileName);
        Assert.Equal("application/pdf", captured.Upload.MediaType.Value);
        Assert.Equal(2_048, captured.Upload.Size.Bytes);
        Assert.Equal("prebuilt-read", captured.ModelId.Value);
    }

    [Fact]
    public async Task The_layout_model_is_used_when_the_caller_names_none()
    {
        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(
                It.Is<DocumentAnalysisRequest>(request => request.ModelId == ModelId.PrebuiltLayout),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnAnalysis().Build());

        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().UsingModel(null).Build(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _analyzer.VerifyAll();
    }

    [Fact]
    public async Task The_caller_cancellation_token_is_passed_through_to_the_analyzer()
    {
        using var source = new CancellationTokenSource();

        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), source.Token))
            .ReturnsAsync(AnAnalysis().Build());

        await CreateHandler().ExecuteAsync(AValidCommand().Build(), source.Token);

        _analyzer.Verify(
            analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), source.Token),
            Times.Once);
    }

    [Fact]
    public async Task An_empty_upload_is_rejected_without_calling_the_analyzer()
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Sized(0).Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal(ErrorCodes.UploadRejected, result.Error.Code);
        Assert.Contains(result.Error.Details!, detail => detail.Contains("empty", StringComparison.OrdinalIgnoreCase));

        _analyzer.Verify(
            analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_oversized_upload_is_rejected_without_calling_the_analyzer()
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Sized(FileSize.FromMegabytes(11).Bytes).Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UploadRejected, result.Error.Code);
        _analyzer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_unsupported_media_type_is_rejected_without_calling_the_analyzer()
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Named("archive.zip").OfType("application/zip").Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UploadRejected, result.Error.Code);
        _analyzer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Every_policy_violation_is_reported_at_once()
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Named("archive.zip").OfType("application/zip").Sized(0).Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Error.Details!.Count);
    }

    [Theory]
    [InlineData("", "application/pdf")]
    [InlineData("   ", "application/pdf")]
    [InlineData("scan.pdf", "")]
    public async Task A_malformed_upload_is_reported_as_such(string fileName, string contentType)
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Named(fileName).OfType(contentType).Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal(ErrorCodes.UploadMalformed, result.Error.Code);
        _analyzer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_negative_content_length_is_reported_as_malformed()
    {
        var result = await CreateHandler().ExecuteAsync(
            AValidCommand().Sized(-1).Build(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UploadMalformed, result.Error.Code);
    }

    [Fact]
    public async Task A_permanent_analyzer_failure_becomes_a_dependency_error()
    {
        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentAnalysisException("The document is password protected."));

        var result = await CreateHandler().ExecuteAsync(AValidCommand().Build(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Dependency, result.Error.Kind);
        Assert.Equal(ErrorCodes.AnalysisFailed, result.Error.Code);
        Assert.Equal("The document is password protected.", result.Error.Message);
    }

    [Fact]
    public async Task A_transient_analyzer_failure_becomes_a_timeout_error()
    {
        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentAnalysisException("Rate limited.", isTransient: true));

        var result = await CreateHandler().ExecuteAsync(AValidCommand().Build(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Timeout, result.Error.Kind);
        Assert.Equal(ErrorCodes.AnalysisUnavailable, result.Error.Code);
    }

    [Fact]
    public async Task A_cancelled_analysis_becomes_a_cancellation_error()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateHandler().ExecuteAsync(AValidCommand().Build(), source.Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Cancelled, result.Error.Kind);
        Assert.Equal(ErrorCodes.AnalysisCancelled, result.Error.Code);
    }

    [Fact]
    public async Task A_cancellation_the_caller_did_not_ask_for_is_not_swallowed()
    {
        // The analyzer's own internal timeout must not be reported as if the
        // caller had walked away — that would hide a real dependency problem.
        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateHandler().ExecuteAsync(AValidCommand().Build(), CancellationToken.None));
    }

    [Fact]
    public async Task A_rejected_upload_is_logged_at_information_level()
    {
        var logger = new Mock<ILogger<AnalyzeDocumentHandler>>();

        await CreateHandler(logger.Object).ExecuteAsync(
            AValidCommand().Sized(0).Build(),
            CancellationToken.None);

        logger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Rejected", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task An_analyzer_failure_is_logged_as_an_error_with_the_exception()
    {
        var logger = new Mock<ILogger<AnalyzeDocumentHandler>>();
        var failure = new DocumentAnalysisException("Boom.");

        _analyzer
            .Setup(analyzer => analyzer.AnalyzeAsync(It.IsAny<DocumentAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        await CreateHandler(logger.Object).ExecuteAsync(AValidCommand().Build(), CancellationToken.None);

        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                failure,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void The_handler_refuses_to_be_built_without_its_collaborators()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnalyzeDocumentHandler(null!, _policy, NullLogger<AnalyzeDocumentHandler>.Instance));

        Assert.Throws<ArgumentNullException>(() =>
            new AnalyzeDocumentHandler(_analyzer.Object, null!, NullLogger<AnalyzeDocumentHandler>.Instance));

        Assert.Throws<ArgumentNullException>(() =>
            new AnalyzeDocumentHandler(_analyzer.Object, _policy, null!));
    }

    [Fact]
    public async Task A_null_command_is_a_programmer_error()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().ExecuteAsync(null!, CancellationToken.None));
    }
}
