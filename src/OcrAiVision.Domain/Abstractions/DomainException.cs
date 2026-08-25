namespace OcrAiVision.Domain.Abstractions;

/// <summary>
/// Raised when an attempt is made to construct a domain object that would
/// violate an invariant. Invariant breaches are programmer errors at this
/// level: callers are expected to have validated input through a policy first.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
