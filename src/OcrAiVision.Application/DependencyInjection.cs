using Microsoft.Extensions.DependencyInjection;
using OcrAiVision.Application.Documents.AnalyzeDocument;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Application;

/// <summary>
/// Registers the Application layer with a container. The layer describes what
/// it needs; the composition root decides how those needs are met.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases and the domain policies they depend on.
    /// </summary>
    /// <param name="services">The container being built.</param>
    /// <param name="maximumUploadSizeInBytes">
    /// The largest upload to accept; defaults to <see cref="DocumentUploadPolicy.DefaultMaximumSize"/>.
    /// </param>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        long? maximumUploadSizeInBytes = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => new DocumentUploadPolicy(
            maximumUploadSizeInBytes is null
                ? null
                : FileSize.FromBytes(maximumUploadSizeInBytes.Value)));

        services.AddScoped<IAnalyzeDocumentUseCase, AnalyzeDocumentHandler>();

        return services;
    }
}
