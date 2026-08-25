using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using OcrAiVision.Application.Documents.Ports;
using OcrAiVision.Domain.Documents;
using DomainAnalyzedDocument = OcrAiVision.Domain.Documents.AnalyzedDocument;

namespace OcrAiVision.Infrastructure.DocumentIntelligence;

/// <summary>
/// The driven adapter that fulfils <see cref="IDocumentAnalyzer"/> using Azure
/// AI Document Intelligence. It owns the SDK call, the retry classification and
/// the translation back into the domain; the use case above it stays unaware
/// that Azure exists at all.
/// </summary>
public sealed class AzureDocumentIntelligenceAnalyzer : IDocumentAnalyzer
{
    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<AzureDocumentIntelligenceAnalyzer> _logger;

    /// <summary>
    /// Creates the adapter.
    /// </summary>
    /// <param name="client">The configured Document Intelligence client.</param>
    /// <param name="logger">Where calls and failures are recorded.</param>
    public AzureDocumentIntelligenceAnalyzer(
        DocumentIntelligenceClient client,
        ILogger<AzureDocumentIntelligenceAnalyzer> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DomainAnalyzedDocument> AnalyzeAsync(
        DocumentAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = await ReadContentAsync(request.Upload, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Sending '{FileName}' ({Size}) to model '{ModelId}'.",
            request.Upload.FileName,
            request.Upload.Size,
            request.ModelId.Value);

        try
        {
            var operation = await _client
                .AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    new AnalyzeDocumentOptions(request.ModelId.Value, content),
                    cancellationToken)
                .ConfigureAwait(false);

            return operation.Value.ToDomain(request.Upload.FileName, request.ModelId);
        }
        catch (RequestFailedException exception)
        {
            throw new DocumentAnalysisException(
                DescribeFailure(exception, request.ModelId.Value),
                IsTransient(exception.Status),
                exception);
        }
        catch (AuthenticationFailedException exception)
        {
            // Token acquisition fails before any request is sent, so it never
            // surfaces as a RequestFailedException. Left unhandled it would
            // reach the caller as an opaque 500.
            throw new DocumentAnalysisException(
                "The application could not obtain a credential for the analysis service.",
                isTransient: false,
                exception);
        }
    }

    /// <summary>
    /// Buffers the upload, because the service call needs a length-known body
    /// and may replay it on retry.
    /// </summary>
    private static async Task<BinaryData> ReadContentAsync(
        DocumentUpload upload,
        CancellationToken cancellationToken)
    {
        await using var stream = upload.OpenReadStream();

        return await BinaryData.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns a service failure into a message that is useful to the caller
    /// without echoing anything the service may have included about the request.
    /// </summary>
    private static string DescribeFailure(RequestFailedException exception, string modelId) => exception.Status switch
    {
        400 => $"The analysis service rejected the document for model '{modelId}'. " +
               "It may be corrupt, password protected, or the wrong format for this model.",
        401 or 403 => "The analysis service rejected the configured credentials.",
        404 => $"The analysis service has no model named '{modelId}'.",
        408 => "The analysis service timed out while reading the document.",
        429 => "The analysis service is rate limiting requests. Try again shortly.",
        >= 500 => "The analysis service is temporarily unavailable.",
        _ => $"The analysis service returned an unexpected status ({exception.Status}).",
    };

    /// <summary>
    /// Whether retrying an identical request has a chance of succeeding.
    /// </summary>
    private static bool IsTransient(int status) => status is 408 or 429 or >= 500;
}
