using Microsoft.AspNetCore.Http.HttpResults;
using OcrAiVision.Application.Abstractions;
using OcrAiVision.Application.Documents.AnalyzeDocument;

namespace OcrAiVision.Api.Documents;

/// <summary>
/// Translates the application's transport-agnostic <see cref="Error"/> into an
/// RFC 9457 problem response. Keeping the choice of status code here — rather
/// than in the use case — is what lets the same use case serve a queue consumer
/// or a CLI without change.
/// </summary>
internal static class ErrorToProblemMapper
{
    /// <summary>
    /// nginx's convention for "the client went away before the response was ready".
    /// ASP.NET Core does not name this one.
    /// </summary>
    internal const int ClientClosedRequest = 499;

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, title) = error.Kind switch
        {
            ErrorKind.Validation => (StatusCodes.Status422UnprocessableEntity, "The document could not be accepted."),
            ErrorKind.Dependency => (StatusCodes.Status502BadGateway, "The analysis service could not process the document."),
            ErrorKind.Timeout => (StatusCodes.Status504GatewayTimeout, "The analysis service did not respond in time."),
            ErrorKind.Cancelled => (ClientClosedRequest, "The request was cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "The request could not be completed."),
        };

        // A malformed upload is a bad request rather than a well-formed request
        // the server refuses, so it keeps the 400 the caller expects.
        if (error.Code == ErrorCodes.UploadMalformed)
        {
            status = StatusCodes.Status400BadRequest;
        }

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
        };

        if (error.Details is { Count: > 0 })
        {
            extensions["violations"] = error.Details;
        }

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            extensions: extensions);
    }
}

