// Services/LocalQuoteLoader.cs
// Scans a folder for .txt and .csv quote files and parses them into QuoteSets.
using QuoteScreensaver.Models;

namespace QuoteScreensaver.Services;

/// <summary>
/// Loads quotes from local .txt and .csv files.
///
/// Format rules:
///   .txt  — One quote per non-empty line.
///           Lines starting with # are treated as comments.
///   .csv  — Rows with columns: Quote,Author  (header row optional, detected automatically).
///           Author column is optional; rows with only one column have no author.
/// </summary>
public sealed class LocalQuoteLoader
{
    /// <summary>
    /// Loads all .txt and .csv files from the given folder as separate QuoteSets.
    /// Returns an empty list if the folder doesn't exist or has no matching files.
    /// </summary>
    public IReadOnlyList<QuoteSet> LoadFromFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return Array.Empty<QuoteSet>();

        var sets = new List<QuoteSet>();

        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.*")
                                          .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                                                   || f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                          .OrderBy(f => f))
        {
            try
            {
                var set = LoadFile(filePath);
                if (!set.IsEmpty) sets.Add(set);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalQuoteLoader] Skipping {filePath}: {ex.Message}");
            }
        }

        return sets;
    }

    /// <summary>Loads a single .txt or .csv file into a QuoteSet.</summary>
    public QuoteSet LoadFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(filePath);
        var lines = File.ReadAllLines(filePath);

        var quotes = ext == ".csv" ? ParseCsv(lines, name) : ParseTxt(lines, name);

        return new QuoteSet
        {
            Name = name,
            SourcePath = filePath,
            Origin = QuoteSetOrigin.Local,
            Quotes = quotes,
            LoadedAt = DateTime.UtcNow
        };
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private static List<Quote> ParseTxt(string[] lines, string sourceName)
    {
        var quotes = new List<Quote>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            // Support inline "text — Author" format
            var parts = line.Split(new[] { " — ", " - ", " – " }, 2, StringSplitOptions.None);
            quotes.Add(parts.Length == 2
                ? new Quote { Text = parts[0].Trim().Trim('"', '“', '”'), Author = parts[1].Trim(), SourceName = sourceName }
                : new Quote { Text = line.Trim('"', '“', '”'), SourceName = sourceName });
        }
        return quotes;
    }

    private static List<Quote> ParseCsv(string[] lines, string sourceName)
    {
        var quotes = new List<Quote>();
        bool skipFirst = false;

        if (lines.Length > 0)
        {
            var header = lines[0].ToLowerInvariant();
            // Detect header row
            skipFirst = header.Contains("quote") || header.Contains("text") || header.Contains("author");
        }

        foreach (var raw in lines.Skip(skipFirst ? 1 : 0))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Count == 0) continue;

            var text = cols[0].Trim().Trim('"');
            var author = cols.Count >= 2 ? cols[1].Trim().Trim('"') : string.Empty;

            if (!string.IsNullOrWhiteSpace(text))
                quotes.Add(new Quote { Text = text, Author = author, SourceName = sourceName });
        }
        return quotes;
    }

    /// <summary>Splits a CSV line respecting double-quoted fields.</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"'); // escaped quote
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
