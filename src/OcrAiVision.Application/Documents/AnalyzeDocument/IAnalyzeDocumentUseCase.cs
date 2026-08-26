using OcrAiVision.Application.Abstractions;

namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// The inbound port of the analyse-document use case. Driving adapters depend
/// on this interface rather than on the concrete interactor, which keeps them
/// testable and keeps the use case free to change shape.
/// </summary>
public interface IAnalyzeDocumentUseCase
{
    /// <summary>
    /// Validates an uploaded document, sends it for analysis and returns the result.
    /// </summary>
    /// <param name="command">The document to analyse.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The analysis, or the reason it could not be produced.</returns>
    Task<Result<AnalyzeDocumentResponse>> ExecuteAsync(
        AnalyzeDocumentCommand command,
        CancellationToken cancellationToken);
}
