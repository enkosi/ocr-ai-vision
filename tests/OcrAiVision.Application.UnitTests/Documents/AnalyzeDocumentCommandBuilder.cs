using System.Text;
using OcrAiVision.Application.Documents.AnalyzeDocument;

namespace OcrAiVision.Application.UnitTests.Documents;

/// <summary>
/// A test data builder for <see cref="AnalyzeDocumentCommand"/>. Each test then
/// states only the one thing it cares about, so a change to the command's shape
/// touches this file instead of every test.
/// </summary>
internal sealed class AnalyzeDocumentCommandBuilder
{
    private string _fileName = "invoice.pdf";
    private string _contentType = "application/pdf";
    private long _sizeInBytes = 1_024;
    private string? _modelId;
    private Func<Stream> _openReadStream = () => new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7"));

    public static AnalyzeDocumentCommandBuilder AValidCommand() => new();

    public AnalyzeDocumentCommandBuilder Named(string fileName)
    {
        _fileName = fileName;
        return this;
    }

    public AnalyzeDocumentCommandBuilder OfType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    public AnalyzeDocumentCommandBuilder Sized(long sizeInBytes)
    {
        _sizeInBytes = sizeInBytes;
        return this;
    }

    public AnalyzeDocumentCommandBuilder UsingModel(string? modelId)
    {
        _modelId = modelId;
        return this;
    }

    public AnalyzeDocumentCommandBuilder ReadWith(Func<Stream> openReadStream)
    {
        _openReadStream = openReadStream;
        return this;
    }

    public AnalyzeDocumentCommand Build() =>
        new(_fileName, _contentType, _sizeInBytes, _openReadStream, _modelId);
}
