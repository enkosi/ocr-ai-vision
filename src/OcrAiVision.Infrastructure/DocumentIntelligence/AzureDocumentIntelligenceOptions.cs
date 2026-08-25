using System.ComponentModel.DataAnnotations;

namespace OcrAiVision.Infrastructure.DocumentIntelligence;

/// <summary>
/// Configuration for the Azure AI Document Intelligence adapter.
/// </summary>
public sealed class AzureDocumentIntelligenceOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "DocumentIntelligence";

    /// <summary>
    /// The resource endpoint, for example
    /// <c>https://my-resource.cognitiveservices.azure.com/</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The resource key. Leave this empty to authenticate with
    /// <c>DefaultAzureCredential</c>, which is the preferred option outside
    /// local development.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The model used when a request does not name one.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DefaultModelId { get; set; } = "prebuilt-layout";

    /// <summary>
    /// The largest upload the API will accept, in bytes. Defaults to 50 MB,
    /// comfortably under the service ceiling.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxUploadSizeInBytes { get; set; } = 50L * 1024L * 1024L;

    /// <summary>
    /// Whether to authenticate with <c>DefaultAzureCredential</c> rather than a key.
    /// </summary>
    public bool UsesManagedIdentity => string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Returns the endpoint as a <see cref="Uri"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configured endpoint is not an absolute URI.</exception>
    public Uri EndpointUri =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException(
                $"'{Endpoint}' is not a valid Document Intelligence endpoint. " +
                $"Set {SectionName}:{nameof(Endpoint)} to an absolute URI.");
}
