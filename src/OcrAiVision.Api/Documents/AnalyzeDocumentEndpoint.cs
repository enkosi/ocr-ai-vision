using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OcrAiVision.Application.Documents.AnalyzeDocument;

namespace OcrAiVision.Api.Documents;

/// <summary>
/// The HTTP driving adapter for the analyse-document use case. Its whole job is
/// translation: multipart request in, use case command out, use case result in,
/// HTTP response out. No business rule lives here.
/// </summary>
public static class AnalyzeDocumentEndpoint
{
    /// <summary>The route the endpoint is served from.</summary>
    public const string RoutePattern = "/api/v1/documents/analyze";

    /// <summary>The multipart form field the document is expected under.</summary>
    public const string FormFieldName = "file";

    /// <summary>
    /// Maps the endpoint onto a route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAnalyzeDocumentEndpoint(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost(RoutePattern, HandleAsync)
            .WithName("AnalyzeDocument")
            .WithSummary("Analyses an uploaded document with Azure AI Document Intelligence.")
            .WithDescription(
                "Accepts a single file as multipart/form-data under the field name 'file' and returns " +
                "the text, pages, tables and labelled fields recovered from it.")
            .WithTags("Documents")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<AnalyzeDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return routes;
    }

    /// <summary>
    /// Reads the uploaded file off the request and hands it to the use case.
    /// The form is read by hand rather than through <see cref="IFormFile"/>
    /// model binding, because binding throws when a caller posts no multipart
    /// body at all — and a missing file deserves a 400 that says so, not a 500.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="useCase">The use case that does the work.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <param name="modelId">
    /// An optional Document Intelligence model, for example <c>prebuilt-invoice</c>.
    /// Defaults to <c>prebuilt-layout</c>.
    /// </param>
    internal static async Task<Results<Ok<AnalyzeDocumentResponse>, ProblemHttpResult>> HandleAsync(
        HttpRequest request,
        IAnalyzeDocumentUseCase useCase,
        CancellationToken cancellationToken,
        [FromQuery] string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasFormContentType)
        {
            return MissingFile();
        }

        IFormFile? file;

        try
        {
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);

            file = form.Files[FormFieldName] ?? form.Files.FirstOrDefault();
        }
        catch (InvalidDataException exception)
        {
            // Kestrel enforces the multipart body limit before the upload policy
            // ever sees the file, so the ceiling has to be reported from here.
            return TypedResults.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The uploaded document is too large.");
        }

        return await AnalyzeAsync(file, useCase, cancellationToken, modelId).ConfigureAwait(false);
    }

    /// <summary>
    /// Translates one uploaded file into a use case call and its result back
    /// into a response.
    /// </summary>
    internal static async Task<Results<Ok<AnalyzeDocumentResponse>, ProblemHttpResult>> AnalyzeAsync(
        IFormFile? file,
        IAnalyzeDocumentUseCase useCase,
        CancellationToken cancellationToken,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);

        if (file is null)
        {
            return MissingFile();
        }

        var command = new AnalyzeDocumentCommand(
            file.FileName,
            file.ContentType ?? string.Empty,
            file.Length,
            file.OpenReadStream,
            modelId);

        var result = await useCase.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);

        return result.Match<Results<Ok<AnalyzeDocumentResponse>, ProblemHttpResult>>(
            response => TypedResults.Ok(response),
            error => error.ToProblem());
    }

    private static ProblemHttpResult MissingFile() =>
        TypedResults.Problem(
            detail: $"Send the document as multipart/form-data under the field name '{FormFieldName}'.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "No document was uploaded.");
}
