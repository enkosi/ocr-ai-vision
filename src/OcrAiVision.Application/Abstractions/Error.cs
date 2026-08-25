namespace OcrAiVision.Application.Abstractions;

/// <summary>
/// The kinds of failure a use case can report, chosen so that a delivery
/// mechanism can map them onto its own vocabulary (HTTP status codes, exit
/// codes, queue retries) without the use case knowing about that vocabulary.
/// </summary>
public enum ErrorKind
{
    /// <summary>The request was rejected by a business rule before any work was done.</summary>
    Validation,

    /// <summary>A dependency was reachable but refused or failed the request.</summary>
    Dependency,

    /// <summary>A dependency did not answer within the allotted time.</summary>
    Timeout,

    /// <summary>The caller withdrew the request.</summary>
    Cancelled,
}

/// <summary>
/// A failure described in the language of the application, not of a transport.
/// </summary>
/// <param name="Kind">The category of failure.</param>
/// <param name="Code">A stable, machine-readable identifier such as <c>document.unsupported_type</c>.</param>
/// <param name="Message">A human-readable summary safe to return to the caller.</param>
/// <param name="Details">Any additional violations that contributed to the failure.</param>
public sealed record Error(
    ErrorKind Kind,
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null)
{
    /// <summary>Creates a validation failure.</summary>
    public static Error Validation(string code, string message, IReadOnlyList<string>? details = null) =>
        new(ErrorKind.Validation, code, message, details);

    /// <summary>Creates a dependency failure.</summary>
    public static Error Dependency(string code, string message) =>
        new(ErrorKind.Dependency, code, message);

    /// <summary>Creates a timeout failure.</summary>
    public static Error Timeout(string code, string message) =>
        new(ErrorKind.Timeout, code, message);

    /// <summary>Creates a cancellation failure.</summary>
    public static Error Cancelled(string code, string message) =>
        new(ErrorKind.Cancelled, code, message);
}
