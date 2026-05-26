// Models/QuoteSet.cs
// A named, sourced collection of quotes (one file = one QuoteSet).
namespace QuoteScreensaver.Models;

/// <summary>Where this QuoteSet originated.</summary>
public enum QuoteSetOrigin { Local, GitHub, BuiltIn }

/// <summary>
/// A named collection of quotes loaded from a single source
/// (one .txt file, one .csv file, or one GitHub URL).
/// </summary>
public sealed class QuoteSet
{
    /// <summary>Human-readable name for this set (file name, repo name, etc.).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Full path or URL from which this set was loaded.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Origin (local disk, GitHub, or built-in defaults).</summary>
    public QuoteSetOrigin Origin { get; init; }

    /// <summary>All quotes in this set.</summary>
    public IReadOnlyList<Quote> Quotes { get; init; } = Array.Empty<Quote>();

    /// <summary>UTC timestamp of when this set was last loaded/refreshed.</summary>
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;

    /// <summary>True if this set has at least one quote.</summary>
    public bool IsEmpty => Quotes.Count == 0;

    public override string ToString() => $"{Name} ({Quotes.Count} quotes, {Origin})";
}
