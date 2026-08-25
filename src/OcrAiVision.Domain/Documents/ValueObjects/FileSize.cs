using System.Globalization;
using OcrAiVision.Domain.Abstractions;

namespace OcrAiVision.Domain.Documents.ValueObjects;

/// <summary>
/// A non-negative size in bytes. Wrapping the primitive keeps "bytes versus
/// megabytes" mistakes out of the policy rules that compare sizes.
/// </summary>
public sealed class FileSize : ValueObject, IComparable<FileSize>
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    private FileSize(long bytes) => Bytes = bytes;

    /// <summary>The size in bytes.</summary>
    public long Bytes { get; }

    /// <summary>Whether the size is zero.</summary>
    public bool IsEmpty => Bytes == 0;

    /// <summary>
    /// Creates a size from a byte count.
    /// </summary>
    public static FileSize FromBytes(long bytes)
    {
        if (bytes < 0)
        {
            throw new DomainException($"File size cannot be negative but was {bytes}.");
        }

        return new FileSize(bytes);
    }

    /// <summary>
    /// Creates a size from a megabyte count.
    /// </summary>
    public static FileSize FromMegabytes(int megabytes)
    {
        if (megabytes < 0)
        {
            throw new DomainException($"File size cannot be negative but was {megabytes}MB.");
        }

        return new FileSize(megabytes * BytesPerMegabyte);
    }

    /// <inheritdoc />
    public int CompareTo(FileSize? other) => other is null ? 1 : Bytes.CompareTo(other.Bytes);

    public static bool operator >(FileSize left, FileSize right) => left.CompareTo(right) > 0;

    public static bool operator <(FileSize left, FileSize right) => left.CompareTo(right) < 0;

    public static bool operator >=(FileSize left, FileSize right) => left.CompareTo(right) >= 0;

    public static bool operator <=(FileSize left, FileSize right) => left.CompareTo(right) <= 0;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bytes;
    }

    /// <inheritdoc />
    public override string ToString() => Bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
}
