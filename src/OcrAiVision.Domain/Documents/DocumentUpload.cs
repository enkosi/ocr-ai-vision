using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// A document submitted for analysis, described independently of how it
/// arrived. The bytes themselves stay behind <see cref="OpenReadStream"/> so
/// large uploads never have to be buffered in the domain.
/// </summary>
public sealed class DocumentUpload
{
    private readonly Func<Stream> _openReadStream;

    private DocumentUpload(
        string fileName,
        DocumentMediaType mediaType,
        FileSize size,
        Func<Stream> openReadStream)
    {
        FileName = fileName;
        MediaType = mediaType;
        Size = size;
        _openReadStream = openReadStream;
    }

    /// <summary>The original file name as supplied by the caller.</summary>
    public string FileName { get; }

    /// <summary>The declared media type of the content.</summary>
    public DocumentMediaType MediaType { get; }

    /// <summary>The size of the content.</summary>
    public FileSize Size { get; }

    /// <summary>
    /// Creates an upload. Only structural requirements are enforced here;
    /// business rules such as size limits belong to
    /// <see cref="DocumentUploadPolicy"/> so that a caller can report every
    /// violation rather than trip over the first one.
    /// </summary>
    public static DocumentUpload Create(
        string fileName,
        DocumentMediaType mediaType,
        FileSize size,
        Func<Stream> openReadStream)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(openReadStream);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("File name must not be empty.");
        }

        return new DocumentUpload(fileName.Trim(), mediaType, size, openReadStream);
    }

    /// <summary>
    /// Opens a fresh readable stream over the uploaded content. The caller owns
    /// the returned stream and is responsible for disposing it.
    /// </summary>
    public Stream OpenReadStream() => _openReadStream();
}
