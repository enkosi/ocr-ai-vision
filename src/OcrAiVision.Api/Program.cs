using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using OcrAiVision.Api.Documents;
using OcrAiVision.Application;
using OcrAiVision.Infrastructure;
using OcrAiVision.Infrastructure.DocumentIntelligence;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root -------------------------------------------------------
// This file is the only place that knows about every layer at once. Everything
// below it depends inward: Api -> Application -> Domain, with Infrastructure
// plugging into the ports the Application layer declares.

builder.Services.AddAzureDocumentIntelligence(builder.Configuration);

var documentIntelligenceOptions = builder.Configuration
    .GetSection(AzureDocumentIntelligenceOptions.SectionName)
    .Get<AzureDocumentIntelligenceOptions>() ?? new AzureDocumentIntelligenceOptions();

builder.Services.AddApplication(documentIntelligenceOptions.MaxUploadSizeInBytes);

// Kestrel has to agree with the upload policy, otherwise a file the policy
// would reject with a readable message is cut off by the server first.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = documentIntelligenceOptions.MaxUploadSizeInBytes;
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new()
{
    Title = "OCR AI Vision — Document Intelligence API",
    Version = "v1",
    Description = "Uploads a document and returns what Azure AI Document Intelligence recovered from it.",
}));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapAnalyzeDocumentEndpoint();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so integration tests can drive the application through
/// <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
