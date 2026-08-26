using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcrAiVision.Application.Documents.Ports;
using OcrAiVision.Infrastructure.DocumentIntelligence;

namespace OcrAiVision.Infrastructure;

/// <summary>
/// Registers the Infrastructure layer with a container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Binds <see cref="AzureDocumentIntelligenceOptions"/>, registers the
    /// Document Intelligence client and wires it to the
    /// <see cref="IDocumentAnalyzer"/> port.
    /// </summary>
    /// <param name="services">The container being built.</param>
    /// <param name="configuration">Configuration containing the
    /// <see cref="AzureDocumentIntelligenceOptions.SectionName"/> section.</param>
    public static IServiceCollection AddAzureDocumentIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureDocumentIntelligenceOptions>()
            .Bind(configuration.GetSection(AzureDocumentIntelligenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureDocumentIntelligenceOptions>>().Value;

            // A key is convenient locally; managed identity is what should run
            // anywhere else, so it is the behaviour you get by leaving the key unset.
            return options.UsesManagedIdentity
                ? new DocumentIntelligenceClient(options.EndpointUri, new DefaultAzureCredential())
                : new DocumentIntelligenceClient(options.EndpointUri, new AzureKeyCredential(options.ApiKey!));
        });

        services.AddScoped<IDocumentAnalyzer, AzureDocumentIntelligenceAnalyzer>();

        return services;
    }
}
