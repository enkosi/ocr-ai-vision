namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// The stable error codes the analyse-document use case reports. Callers may
/// branch on these; the accompanying messages are for humans and may change.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The upload was not described well enough to build a document from.</summary>
    public const string UploadMalformed = "document.upload_malformed";

    /// <summary>The upload broke one or more rules in the upload policy.</summary>
    public const string UploadRejected = "document.upload_rejected";

    /// <summary>The analysis service refused or failed the request.</summary>
    public const string AnalysisFailed = "document.analysis_failed";

    /// <summary>The analysis service was temporarily unable to answer.</summary>
    public const string AnalysisUnavailable = "document.analysis_unavailable";

    /// <summary>The caller withdrew the request before it completed.</summary>
    public const string AnalysisCancelled = "document.analysis_cancelled";
}
