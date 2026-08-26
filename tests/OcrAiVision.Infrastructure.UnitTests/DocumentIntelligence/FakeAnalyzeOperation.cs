using Azure;
using Azure.AI.DocumentIntelligence;

namespace OcrAiVision.Infrastructure.UnitTests.DocumentIntelligence;

/// <summary>
/// A stand-in for the long-running operation the SDK returns. Moq can subclass
/// <see cref="Operation{T}"/>, but a hand-written double reads better than five
/// property setups repeated in every test.
/// </summary>
internal sealed class FakeAnalyzeOperation(AnalyzeResult value) : Operation<AnalyzeResult>
{
    public override AnalyzeResult Value { get; } = value;

    public override bool HasValue => true;

    public override bool HasCompleted => true;

    public override string Id => "fake-operation";

    public override Response GetRawResponse() => throw new NotSupportedException();

    public override Response UpdateStatus(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override ValueTask<Response> UpdateStatusAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
