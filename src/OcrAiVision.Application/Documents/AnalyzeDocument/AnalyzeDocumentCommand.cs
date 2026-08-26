namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// The input boundary of the analyse-document use case. It is expressed in
/// plain types so that an HTTP handler, a queue consumer or a CLI can all
/// construct one without the use case learning about any of them.
/// </summary>
/// <param name="FileName">The original name of the uploaded file.</param>
/// <param name="ContentType">The declared <c>Content-Type</c> of the uploaded file.</param>
/// <param name="SizeInBytes">The length of the uploaded content.</param>
/// <param name="OpenReadStream">Opens a fresh readable stream over the content.</param>
/// <param name="ModelId">The model to analyse with; falls back to the layout model when omitted.</param>
public sealed record AnalyzeDocumentCommand(
    string FileName,
    string ContentType,
    long SizeInBytes,
    Func<Stream> OpenReadStream,
    string? ModelId = null);
