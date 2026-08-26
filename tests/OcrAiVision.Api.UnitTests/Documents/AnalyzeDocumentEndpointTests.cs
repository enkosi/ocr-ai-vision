using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using OcrAiVision.Api.Documents;
using OcrAiVision.Application.Abstractions;
using OcrAiVision.Application.Documents.AnalyzeDocument;
using Xunit;

namespace OcrAiVision.Api.UnitTests.Documents;

/// <summary>
/// Exercises the HTTP adapter against a mocked use case. These tests are about
/// translation only — what the endpoint puts into the command, and what status
/// code each outcome becomes — because that is all the adapter is allowed to do.
/// </summary>
public sealed class AnalyzeDocumentEndpointTests
{
    private readonly Mock<IAnalyzeDocumentUseCase> _useCase = new(MockBehavior.Strict);

    [Fact]
    public async Task A_successful_analysis_is_returned_as_200_with_the_response_body()
    {
        var response = ResponseFor("invoice.pdf");

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(response));

        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("invoice.pdf"),
            _useCase.Object,
            CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyzeDocumentResponse>>(result.Result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task The_uploaded_file_is_described_faithfully_in_the_command()
    {
        AnalyzeDocumentCommand? captured = null;

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnalyzeDocumentCommand, CancellationToken>((command, _) => captured = command)
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(ResponseFor("scan.png")));

        await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("scan.png", "image/png", "the bytes"),
            _useCase.Object,
            CancellationToken.None,
            modelId: "prebuilt-receipt");

        Assert.NotNull(captured);
        Assert.Equal("scan.png", captured.FileName);
        Assert.Equal("image/png", captured.ContentType);
        Assert.Equal(Encoding.UTF8.GetByteCount("the bytes"), captured.SizeInBytes);
        Assert.Equal("prebuilt-receipt", captured.ModelId);

        using var reader = new StreamReader(captured.OpenReadStream());
        Assert.Equal("the bytes", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task No_model_in_the_query_string_leaves_the_choice_to_the_use_case()
    {
        AnalyzeDocumentCommand? captured = null;

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnalyzeDocumentCommand, CancellationToken>((command, _) => captured = command)
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(ResponseFor("scan.pdf")));

        await AnalyzeDocumentEndpoint.AnalyzeAsync(FileNamed("scan.pdf"), _useCase.Object, CancellationToken.None);

        Assert.Null(captured!.ModelId);
    }

    [Fact]
    public async Task A_missing_file_is_rejected_as_400_without_reaching_the_use_case()
    {
        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(file: null, _useCase.Object, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains("'file'", problem.ProblemDetails.Detail, StringComparison.Ordinal);

        _useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_request_cancellation_token_is_passed_to_the_use_case()
    {
        using var source = new CancellationTokenSource();

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), source.Token))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(ResponseFor("scan.pdf")));

        await AnalyzeDocumentEndpoint.AnalyzeAsync(FileNamed("scan.pdf"), _useCase.Object, source.Token);

        _useCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), source.Token),
            Times.Once);
    }

    [Theory]
    [InlineData(ErrorKind.Validation, ErrorCodes.UploadRejected, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorKind.Validation, ErrorCodes.UploadMalformed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.Dependency, ErrorCodes.AnalysisFailed, StatusCodes.Status502BadGateway)]
    [InlineData(ErrorKind.Timeout, ErrorCodes.AnalysisUnavailable, StatusCodes.Status504GatewayTimeout)]
    [InlineData(ErrorKind.Cancelled, ErrorCodes.AnalysisCancelled, 499)]
    public async Task Each_use_case_failure_maps_onto_the_status_code_that_describes_it(
        ErrorKind kind,
        string code,
        int expectedStatus)
    {
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Failure(new Error(kind, code, "it did not work")));

        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("scan.pdf"),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("it did not work", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task The_error_code_is_carried_in_the_problem_response_so_clients_can_branch_on_it()
    {
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Failure(
                Error.Dependency(ErrorCodes.AnalysisFailed, "the service refused")));

        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("scan.pdf"),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(ErrorCodes.AnalysisFailed, problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task Policy_violations_are_listed_in_the_problem_response()
    {
        var violations = new[] { "The uploaded file is empty.", "'application/zip' is not a supported document type." };

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Failure(
                Error.Validation(ErrorCodes.UploadRejected, "rejected", violations)));

        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("archive.zip", "application/zip"),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(violations, Assert.IsAssignableFrom<IReadOnlyList<string>>(
            problem.ProblemDetails.Extensions["violations"]));
    }

    [Fact]
    public async Task A_failure_with_no_violations_omits_the_list_entirely()
    {
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Failure(
                Error.Dependency(ErrorCodes.AnalysisFailed, "the service refused")));

        var result = await AnalyzeDocumentEndpoint.AnalyzeAsync(
            FileNamed("scan.pdf"),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.False(problem.ProblemDetails.Extensions.ContainsKey("violations"));
    }

    private static AnalyzeDocumentResponse ResponseFor(string fileName) =>
        new(fileName, "prebuilt-layout", "content", 1, null, [], [], []);

    private static IFormFile FileNamed(
        string fileName,
        string contentType = "application/pdf",
        string content = "%PDF-1.7")
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }
}
