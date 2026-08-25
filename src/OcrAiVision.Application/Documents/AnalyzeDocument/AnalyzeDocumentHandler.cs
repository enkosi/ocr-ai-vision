using Microsoft.Extensions.Logging;
using OcrAiVision.Application.Abstractions;
using OcrAiVision.Application.Documents.Ports;
using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// The interactor for the analyse-document use case. It owns the sequence —
/// build the domain objects, apply the upload policy, call the gateway, map the
/// result — and nothing else. It knows about no transport, no vendor SDK and no
/// serialisation format.
/// </summary>
public sealed class AnalyzeDocumentHandler : IAnalyzeDocumentUseCase
{
    private readonly IDocumentAnalyzer _analyzer;
    private readonly DocumentUploadPolicy _uploadPolicy;
    private readonly ILogger<AnalyzeDocumentHandler> _logger;

    /// <summary>
    /// Creates the interactor.
    /// </summary>
    /// <param name="analyzer">The gateway to the analysis service.</param>
    /// <param name="uploadPolicy">The rules an upload must satisfy.</param>
    /// <param name="logger">Where progress and failures are recorded.</param>
    public AnalyzeDocumentHandler(
        IDocumentAnalyzer analyzer,
        DocumentUploadPolicy uploadPolicy,
        ILogger<AnalyzeDocumentHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _uploadPolicy = uploadPolicy ?? throw new ArgumentNullException(nameof(uploadPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<AnalyzeDocumentResponse>> ExecuteAsync(
        AnalyzeDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var upload = TryCreateUpload(command, out var creationError);

        if (upload is null)
        {
            return Result<AnalyzeDocumentResponse>.Failure(creationError!);
        }

        var violations = _uploadPolicy.Validate(upload);

        if (violations.HasErrors)
        {
            _logger.LogInformation(
                "Rejected '{FileName}': {Violations}",
                upload.FileName,
                violations.ToString());

            return Result<AnalyzeDocumentResponse>.Failure(Error.Validation(
                ErrorCodes.UploadRejected,
                "The uploaded document did not satisfy the upload rules.",
                violations.Errors));
        }

        var modelId = ModelId.CreateOrDefault(command.ModelId);

        try
        {
            var analysis = await _analyzer
                .AnalyzeAsync(new DocumentAnalysisRequest(upload, modelId), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Analysed '{FileName}' with '{ModelId}' into {PageCount} page(s) and {FieldCount} field(s).",
                upload.FileName,
                modelId.Value,
                analysis.PageCount,
                analysis.Fields.Count);

            return Result<AnalyzeDocumentResponse>.Success(analysis.ToResponse());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Analysis of '{FileName}' was cancelled by the caller.", upload.FileName);

            return Result<AnalyzeDocumentResponse>.Failure(Error.Cancelled(
                ErrorCodes.AnalysisCancelled,
                "The analysis was cancelled before it completed."));
        }
        catch (DocumentAnalysisException exception)
        {
            _logger.LogError(
                exception,
                "The analysis service could not process '{FileName}'.",
                upload.FileName);

            return Result<AnalyzeDocumentResponse>.Failure(exception.IsTransient
                ? Error.Timeout(ErrorCodes.AnalysisUnavailable, exception.Message)
                : Error.Dependency(ErrorCodes.AnalysisFailed, exception.Message));
        }
    }

    /// <summary>
    /// Builds the domain upload, converting the structural invariants the
    /// domain enforces by throwing into the Result the use case reports with.
    /// </summary>
    private static DocumentUpload? TryCreateUpload(AnalyzeDocumentCommand command, out Error? error)
    {
        try
        {
            var upload = DocumentUpload.Create(
                command.FileName,
                DocumentMediaType.Create(command.ContentType),
                FileSize.FromBytes(command.SizeInBytes),
                command.OpenReadStream);

            error = null;
            return upload;
        }
        catch (DomainException exception)
        {
            error = Error.Validation(ErrorCodes.UploadMalformed, exception.Message);
            return null;
        }
    }
}
