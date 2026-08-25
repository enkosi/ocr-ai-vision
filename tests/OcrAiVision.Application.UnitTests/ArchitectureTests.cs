using System.Reflection;
using OcrAiVision.Application.Documents.AnalyzeDocument;
using OcrAiVision.Domain.Documents;
using Xunit;

namespace OcrAiVision.Application.UnitTests;

/// <summary>
/// The Dependency Rule is the one thing in Clean Architecture that a code review
/// cannot reliably catch by eye, because breaking it takes only an innocent-looking
/// <c>using</c>. These tests fail the build instead.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(AnalyzedDocument).Assembly;
    private static readonly Assembly Application = typeof(AnalyzeDocumentHandler).Assembly;

    [Fact]
    public void The_domain_depends_on_nothing_but_the_framework()
    {
        var offenders = ReferencesOf(Domain)
            .Where(reference => !IsFrameworkAssembly(reference))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"The Domain layer must stay dependency-free but references: {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void The_application_layer_does_not_know_about_azure()
    {
        Assert.DoesNotContain(
            ReferencesOf(Application),
            reference => reference.StartsWith("Azure", StringComparison.Ordinal));
    }

    [Fact]
    public void The_application_layer_does_not_know_about_a_transport()
    {
        Assert.DoesNotContain(
            ReferencesOf(Application),
            reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void The_application_layer_does_not_reference_the_outer_circles()
    {
        Assert.DoesNotContain(
            ReferencesOf(Application),
            reference => reference is "OcrAiVision.Infrastructure" or "OcrAiVision.Api");
    }

    [Fact]
    public void The_domain_does_not_reference_the_outer_circles()
    {
        Assert.DoesNotContain(
            ReferencesOf(Domain),
            reference => reference.StartsWith("OcrAiVision.", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToList();

    /// <summary>
    /// Whether a reference is part of the base class library, which every layer
    /// is allowed to use.
    /// </summary>
    private static bool IsFrameworkAssembly(string name) =>
        name is "System.Runtime" or "System.Private.CoreLib" or "netstandard" ||
        name.StartsWith("System.", StringComparison.Ordinal) ||
        name.StartsWith("mscorlib", StringComparison.Ordinal);
}
