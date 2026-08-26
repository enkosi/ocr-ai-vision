namespace OcrAiVision.Application.Documents.Ports;

using OcrAiVision.Domain.Documents;

/// <summary>
/// The outbound port (a Gateway in Fowler's terms) through which the
/// application reaches a document analysis service. The Application layer owns
/// this interface; Infrastructure implements it, so the dependency points
/// inward and the use case can be exercised against a test double.
/// </summary>
public interface IDocumentAnalyzer
{
    /// <summary>
    /// Runs a document through the requested model.
    /// </summary>
    /// <param name="request">The document and the model to use.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>What the model recovered from the document.</returns>
    /// <exception cref="DocumentAnalysisException">The service could not complete the request.</exception>
    Task<AnalyzedDocument> AnalyzeAsync(DocumentAnalysisRequest request, CancellationToken cancellationToken);
}
