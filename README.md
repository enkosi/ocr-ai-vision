# OCR AI Vision — Document Intelligence API

A minimal ASP.NET Core API that accepts a file upload, runs it through Azure AI
Document Intelligence, and returns the text, pages, tables and labelled fields
recovered from it.

The solution is organised as Clean Architecture: four projects arranged in
concentric circles, with every source-code dependency pointing inward.

## The endpoint

```
POST /api/v1/documents/analyze?modelId=prebuilt-invoice
Content-Type: multipart/form-data
```

| Part | Required | Description |
| --- | --- | --- |
| `file` | yes | The document, sent as multipart/form-data |
| `modelId` (query) | no | A Document Intelligence model. Defaults to `prebuilt-layout` |

```bash
curl -X POST "http://localhost:5133/api/v1/documents/analyze?modelId=prebuilt-invoice" \
     -F "file=@invoice.pdf;type=application/pdf"
```

A successful call returns `200` with:

```json
{
  "fileName": "invoice.pdf",
  "modelId": "prebuilt-invoice",
  "content": "TAX INVOICE ...",
  "pageCount": 2,
  "lowestFieldConfidence": 0.87,
  "pages": [ { "number": 1, "content": "...", "lineCount": 24, "wordCount": 180 } ],
  "tables": [ { "rowCount": 4, "columnCount": 3, "page": 1, "cells": [ ... ] } ],
  "fields": [ { "name": "InvoiceTotal", "value": "ZAR 1200.5", "confidence": 0.87, "page": 1 } ]
}
```

Failures come back as RFC 9457 problem responses carrying a stable `code` and,
for a rejected upload, every rule it broke:

| Status | When |
| --- | --- |
| `400` | No file was sent |
| `413` | The body exceeded the configured multipart limit |
| `422` | The file broke an upload rule (empty, too large, unsupported type) |
| `502` | The analysis service refused the request, or no credential was available |
| `504` | The analysis service was rate limiting or unavailable |

```json
{
  "title": "The document could not be accepted.",
  "status": 422,
  "detail": "The uploaded document did not satisfy the upload rules.",
  "code": "document.upload_rejected",
  "violations": ["The uploaded file is empty."]
}
```

`GET /health` reports liveness. Swagger UI is served at `/swagger` in Development.

## Architecture

```
                      ┌──────────────────────────────────────────┐
                      │  OcrAiVision.Api                         │
                      │  Minimal API endpoint, ProblemDetails     │
                      │  mapping, composition root                │
                      └───────────────┬──────────────────────────┘
                                      │            ▲
                      ┌───────────────▼─────────┐  │ implements
                      │  OcrAiVision.Application │  │ IDocumentAnalyzer
                      │  Use cases and ports     │  │
                      └───────────────┬─────────┘  │
                                      │      ┌─────┴──────────────────────┐
                      ┌───────────────▼────┐ │ OcrAiVision.Infrastructure │
                      │  OcrAiVision.Domain│ │ Azure SDK adapter,         │
                      │  Entities, value   │ │ anti-corruption translator │
                      │  objects, policies │ └────────────────────────────┘
                      └────────────────────┘
```

**The Dependency Rule.** Nothing in an inner circle knows anything about an
outer one. `Domain` references no packages at all. `Application` declares the
`IDocumentAnalyzer` port it needs; `Infrastructure` implements it, so the arrow
from Azure to the use case points *inward*. Only `Program.cs` — the composition
root — sees every layer at once.

This is enforced, not merely intended: `ArchitectureTests` asserts by reflection
that `Domain` carries no non-framework references and that `Application` has no
reference to Azure, ASP.NET Core, or either outer project. Break the rule with a
stray `using` and the build fails.

### What lives where

| Project | Contents |
| --- | --- |
| `OcrAiVision.Domain` | `AnalyzedDocument` aggregate; `ConfidenceScore`, `PageNumber`, `ModelId`, `FileSize`, `DocumentMediaType` value objects; `DocumentUploadPolicy`; `Notification` |
| `OcrAiVision.Application` | `AnalyzeDocumentHandler` interactor, its command/response DTOs, the `IDocumentAnalyzer` port, `Result<T>` and `Error` |
| `OcrAiVision.Infrastructure` | `AzureDocumentIntelligenceAnalyzer`, `AnalyzeResultTranslator`, options binding, DI registration |
| `OcrAiVision.Api` | `AnalyzeDocumentEndpoint`, `ErrorToProblemMapper`, `Program.cs` |

### Patterns worth naming

- **Value Object** (Evans/Fowler) — `ConfidenceScore`, `FileSize` and friends
  replace bare primitives, so "is this bytes or megabytes?" stops being a
  question the reader has to answer from context.
- **Notification** (Fowler) — `DocumentUploadPolicy.Validate` collects *every*
  rule violation rather than throwing on the first, so a caller sending a
  0-byte `.zip` is told about both problems at once.
- **Result over exceptions** — expected failures travel as `Result<T>` values.
  Exceptions stay for genuine programmer errors. The use case never chooses an
  HTTP status; it reports an `ErrorKind`, and `ErrorToProblemMapper` decides
  what that means over HTTP.
- **Gateway / anti-corruption layer** — `AnalyzeResultTranslator` is the only
  file in the solution that understands the Azure response shape. Swapping the
  provider means rewriting that file and the adapter beside it.
- **Test data builders** — `AnalyzedDocumentBuilder` and friends let each test
  state only the one thing it is about.

## Configuration

`appsettings.json`:

```json
{
  "DocumentIntelligence": {
    "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
    "ApiKey": "",
    "DefaultModelId": "prebuilt-layout",
    "MaxUploadSizeInBytes": 52428800
  }
}
```

Leave `ApiKey` empty and the adapter authenticates with `DefaultAzureCredential`
(managed identity), which is what should run anywhere but a laptop. Set the key
only for local development, and set it through user secrets rather than the
committed file:

```bash
dotnet user-secrets set "DocumentIntelligence:ApiKey" "<key>" \
  --project src/OcrAiVision.Api
```

`Endpoint` and `DefaultModelId` are validated at startup, so a misconfigured
deployment fails immediately instead of on its first request.
`MaxUploadSizeInBytes` sets both the domain upload policy and Kestrel's
multipart limit, so the two cannot drift apart.

## Running it

Requires the .NET 8 SDK.

```bash
dotnet restore
dotnet build
dotnet run --project src/OcrAiVision.Api
```

Then open `http://localhost:5133/swagger`.

## Tests

```bash
dotnet test
```

149 unit tests across four projects, using **xUnit** and **Moq**. No test
touches a network, a file system or a live Azure resource.

| Project | Focus |
| --- | --- |
| `Domain.UnitTests` | Invariants of the value objects, the upload policy's rules, aggregate behaviour |
| `Application.UnitTests` | The interactor against a mocked `IDocumentAnalyzer` — happy path, each validation failure, dependency and transient failures, cancellation, logging; plus the architecture tests |
| `Infrastructure.UnitTests` | The Azure adapter against a mocked `DocumentIntelligenceClient`, translating real service JSON and classifying every failure status |
| `Api.UnitTests` | The endpoint against a mocked use case — command construction, form reading, and the status code each outcome maps to |

Two details worth knowing about the mocking:

- `Mock<IDocumentAnalyzer>` uses `MockBehavior.Strict`, so any call the
  interactor makes that a test did not set up is a failure. That is what proves
  the validation paths never reach the analyzer.
- The Azure SDK's `DocumentIntelligenceClient` exposes a protected parameterless
  constructor and virtual methods specifically so it can be substituted. The
  adapter's tests take advantage of that and feed it recorded service JSON
  through the SDK's own deserialiser, so a fixture that stops matching the real
  wire contract stops parsing rather than quietly testing a shape that never
  occurs.
