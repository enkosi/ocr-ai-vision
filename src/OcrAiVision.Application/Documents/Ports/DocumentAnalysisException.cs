namespace OcrAiVision.Application.Documents.Ports;

/// <summary>
/// Thrown by an <see cref="IDocumentAnalyzer"/> when the analysis service
/// cannot complete a request. Adapters translate their vendor-specific
/// exceptions into this type so the use case never has to reference a vendor SDK.
/// </summary>
public sealed class DocumentAnalysisException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">A summary safe to surface to the caller.</param>
    /// <param name="isTransient">Whether retrying the same request could succeed.</param>
    /// <param name="innerException">The underlying vendor exception, when there is one.</param>
    public DocumentAnalysisException(string message, bool isTransient = false, Exception? innerException = null)
        : base(message, innerException) => IsTransient = isTransient;

    /// <summary>Whether retrying the same request could succeed.</summary>
    public bool IsTransient { get; }
}
