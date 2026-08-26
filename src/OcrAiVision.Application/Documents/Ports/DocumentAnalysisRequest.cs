using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Application.Documents.Ports;

/// <summary>
/// What the analysis gateway needs in order to run a document through a model.
/// </summary>
/// <param name="Upload">The document to analyse.</param>
/// <param name="ModelId">The model to analyse it with.</param>
public sealed record DocumentAnalysisRequest(DocumentUpload Upload, ModelId ModelId);
