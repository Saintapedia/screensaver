// Models/Quote.cs
// Represents a single quotation with optional attribution.
namespace QuoteScreensaver.Models;

/// <summary>A single quotation with text and optional author.</summary>
public sealed class Quote
{
    /// <summary>The quote body text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The author / source name. Empty string means no attribution.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Which quote set this quote belongs to (for filtering).</summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>Returns true if an author name is present.</summary>
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);

    /// <summary>Full display text including author attribution when enabled.</summary>
    public string GetDisplayText(bool showAuthor) =>
        showAuthor && HasAuthor ? $"“{Text}”\n— {Author}" : $"“{Text}”";

    public override string ToString() => HasAuthor ? $"\"{Text}\" — {Author}" : $"\"{Text}\"";
}
