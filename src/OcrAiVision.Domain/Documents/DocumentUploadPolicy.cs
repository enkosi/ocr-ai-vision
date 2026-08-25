using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// The rules an upload must satisfy before it is worth sending to the analysis
/// service. Keeping them here — rather than in an ASP.NET filter — means the
/// same rules apply no matter which delivery mechanism accepts the file.
/// </summary>
public sealed class DocumentUploadPolicy
{
    /// <summary>The media types Document Intelligence can read.</summary>
    public static readonly IReadOnlySet<string> SupportedMediaTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "image/bmp",
            "image/tiff",
            "image/heif",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "text/html",
        };

    /// <summary>The limit applied when no other is configured (the service ceiling for the standard tier).</summary>
    public static readonly FileSize DefaultMaximumSize = FileSize.FromMegabytes(500);

    /// <summary>
    /// Creates a policy.
    /// </summary>
    /// <param name="maximumSize">The largest upload accepted; defaults to <see cref="DefaultMaximumSize"/>.</param>
    public DocumentUploadPolicy(FileSize? maximumSize = null)
    {
        MaximumSize = maximumSize ?? DefaultMaximumSize;

        if (MaximumSize.IsEmpty)
        {
            throw new DomainException("The maximum upload size must be greater than zero.");
        }
    }

    /// <summary>The largest upload this policy accepts.</summary>
    public FileSize MaximumSize { get; }

    /// <summary>
    /// Checks an upload against every rule and returns the collected violations.
    /// </summary>
    public Notification Validate(DocumentUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var notification = new Notification();

        notification.AddErrorIf(
            upload.Size.IsEmpty,
            "The uploaded file is empty.");

        notification.AddErrorIf(
            upload.Size > MaximumSize,
            $"The uploaded file is {upload.Size} which exceeds the {MaximumSize} limit.");

        notification.AddErrorIf(
            !SupportedMediaTypes.Contains(upload.MediaType.Value),
            $"'{upload.MediaType}' is not a supported document type. Supported types are: " +
            $"{string.Join(", ", SupportedMediaTypes.Order(StringComparer.Ordinal))}.");

        return notification;
    }
}
