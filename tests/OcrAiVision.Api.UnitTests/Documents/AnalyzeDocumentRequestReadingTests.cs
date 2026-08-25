using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using OcrAiVision.Api.Documents;
using OcrAiVision.Application.Abstractions;
using OcrAiVision.Application.Documents.AnalyzeDocument;
using Xunit;

namespace OcrAiVision.Api.UnitTests.Documents;

/// <summary>
/// Covers how the endpoint gets the file off the request. Model binding an
/// <see cref="IFormFile"/> throws outright when the caller posts no multipart
/// body, so the endpoint reads the form itself — and these tests hold it to
/// answering with a 400 rather than an unhandled 500.
/// </summary>
public sealed class AnalyzeDocumentRequestReadingTests
{
    private readonly Mock<IAnalyzeDocumentUseCase> _useCase = new(MockBehavior.Strict);

    [Fact]
    public async Task A_request_with_no_body_at_all_is_a_400_rather_than_an_unhandled_error()
    {
        var result = await AnalyzeDocumentEndpoint.HandleAsync(
            RequestWithNoForm(),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        _useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_json_request_is_a_400_rather_than_an_unhandled_error()
    {
        var result = await AnalyzeDocumentEndpoint.HandleAsync(
            RequestWithNoForm("application/json"),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task A_multipart_request_carrying_no_file_is_a_400()
    {
        var result = await AnalyzeDocumentEndpoint.HandleAsync(
            RequestWithForm(new FormFileCollection()),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains(AnalyzeDocumentEndpoint.FormFieldName, problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_file_field_is_preferred_when_the_form_carries_several_files()
    {
        AnalyzeDocumentCommand? captured = null;

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnalyzeDocumentCommand, CancellationToken>((command, _) => captured = command)
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(
                new AnalyzeDocumentResponse("wanted.pdf", "prebuilt-layout", "", 0, null, [], [], [])));

        var files = new FormFileCollection
        {
            FileNamed("ignored.pdf", field: "attachment"),
            FileNamed("wanted.pdf", field: AnalyzeDocumentEndpoint.FormFieldName),
        };

        await AnalyzeDocumentEndpoint.HandleAsync(RequestWithForm(files), _useCase.Object, CancellationToken.None);

        Assert.Equal("wanted.pdf", captured!.FileName);
    }

    [Fact]
    public async Task A_single_file_under_another_field_name_is_still_accepted()
    {
        AnalyzeDocumentCommand? captured = null;

        _useCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<AnalyzeDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnalyzeDocumentCommand, CancellationToken>((command, _) => captured = command)
            .ReturnsAsync(Result<AnalyzeDocumentResponse>.Success(
                new AnalyzeDocumentResponse("scan.pdf", "prebuilt-layout", "", 0, null, [], [], [])));

        var files = new FormFileCollection { FileNamed("scan.pdf", field: "document") };

        await AnalyzeDocumentEndpoint.HandleAsync(RequestWithForm(files), _useCase.Object, CancellationToken.None);

        Assert.Equal("scan.pdf", captured!.FileName);
    }

    [Fact]
    public async Task A_body_beyond_the_multipart_limit_is_reported_as_413()
    {
        var result = await AnalyzeDocumentEndpoint.HandleAsync(
            RequestThatFailsToReadItsForm(new InvalidDataException("Multipart body length limit 100 exceeded.")),
            _useCase.Object,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.StatusCode);
        _useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_null_request_is_a_programmer_error()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            AnalyzeDocumentEndpoint.HandleAsync(null!, _useCase.Object, CancellationToken.None));
    }

    private static HttpRequest RequestWithNoForm(string? contentType = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;

        if (contentType is not null)
        {
            context.Request.ContentType = contentType;
        }

        return context.Request;
    }

    private static HttpRequest RequestWithForm(IFormFileCollection files)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "multipart/form-data; boundary=----test";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);

        return context.Request;
    }

    /// <summary>
    /// Builds a request whose form cannot be read, which is how Kestrel reports
    /// a body that exceeded the configured multipart limit.
    /// </summary>
    private static HttpRequest RequestThatFailsToReadItsForm(Exception failure)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "multipart/form-data; boundary=----test";
        context.Features.Set<IFormFeature>(new ThrowingFormFeature(failure));

        return context.Request;
    }

    private static IFormFile FileNamed(string fileName, string field)
    {
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7");

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, field, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }

    private sealed class ThrowingFormFeature(Exception failure) : IFormFeature
    {
        public bool HasFormContentType => true;

        public IFormCollection? Form { get => throw failure; set => throw failure; }

        public IFormCollection ReadForm() => throw failure;

        public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken) => throw failure;
    }
}
