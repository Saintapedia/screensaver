// Services/GitHubQuoteLoader.cs
// Downloads quotes from GitHub raw file URLs or folder API paths and caches them.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using QuoteScreensaver.Models;

namespace QuoteScreensaver.Services;

/// <summary>
/// Downloads quote files from GitHub and caches them locally under
/// %AppData%\QuoteScreensaver\cache\.
///
/// Supported URL formats
///   1. Raw file:   https://raw.githubusercontent.com/user/repo/branch/path/file.txt
///   2. Blob URL:   https://github.com/user/repo/blob/branch/path/file.txt  (auto-converted)
///   3. Tree URL:   https://github.com/user/repo/tree/branch/path/           (folder listing)
///   4. API URL:    https://api.github.com/repos/user/repo/contents/path/    (folder listing)
/// </summary>
public sealed class GitHubQuoteLoader : IDisposable
{
    private static readonly HttpClient _http = CreateHttpClient();
    private readonly string _cacheDir;
    private readonly LocalQuoteLoader _localLoader = new();

    public GitHubQuoteLoader(string cacheDirectory)
    {
        _cacheDir = cacheDirectory;
        Directory.CreateDirectory(_cacheDir);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads quotes from the given URL.
    /// If <paramref name="forceRefresh"/> is false and a fresh enough cache exists,
    /// returns the cached version without hitting the network.
    /// </summary>
    public async Task<IReadOnlyList<QuoteSet>> LoadAsync(
        string url,
        CacheRefreshInterval refreshInterval = CacheRefreshInterval.Daily,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return Array.Empty<QuoteSet>();

        try
        {
            var normalised = NormaliseUrl(url.Trim());
            bool isFolder = IsFolderUrl(normalised);

            if (isFolder)
                return await LoadFolderAsync(normalised, refreshInterval, forceRefresh, ct);
            else
                return await LoadSingleFileAsync(normalised, refreshInterval, forceRefresh, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GitHub] Load failed for {url}: {ex.Message}");
            // Fall back to cached data if available
            return LoadFromCacheFallback(url);
        }
    }

    /// <summary>Tests whether a URL is reachable. Returns null on success, error message on failure.</summary>
    public async Task<string?> TestConnectionAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var normalised = NormaliseUrl(url.Trim());
            using var req = new HttpRequestMessage(HttpMethod.Head, normalised);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.IsSuccessStatusCode) return null;
            return $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ── Single-file download ──────────────────────────────────────────────────

    private async Task<IReadOnlyList<QuoteSet>> LoadSingleFileAsync(
        string rawUrl, CacheRefreshInterval interval, bool forceRefresh, CancellationToken ct)
    {
        var cacheFile = GetCacheFilePath(rawUrl);
        var cacheAge = GetCacheAge(cacheFile);

        if (!forceRefresh && cacheAge.HasValue && cacheAge.Value < GetMaxAge(interval))
        {
            var cached = _localLoader.LoadFile(cacheFile);
            return cached.IsEmpty ? Array.Empty<QuoteSet>() : new[] { cached };
        }

        var content = await _http.GetStringAsync(rawUrl, ct);
        await File.WriteAllTextAsync(cacheFile, content, ct);

        var set = ParseContent(rawUrl, content);
        return set is null ? Array.Empty<QuoteSet>() : new[] { set };
    }

    // ── Folder download ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<QuoteSet>> LoadFolderAsync(
        string apiUrl, CacheRefreshInterval interval, bool forceRefresh, CancellationToken ct)
    {
        // Fetch directory listing from GitHub Contents API
        var listCacheFile = GetCacheFilePath(apiUrl + "/_index_");
        var cacheAge = GetCacheAge(listCacheFile);

        List<(string name, string downloadUrl)> files;

        if (!forceRefresh && cacheAge.HasValue && cacheAge.Value < GetMaxAge(interval)
            && File.Exists(listCacheFile))
        {
            var cachedJson = await File.ReadAllTextAsync(listCacheFile, ct);
            files = ParseDirectoryListing(cachedJson);
        }
        else
        {
            var json = await _http.GetStringAsync(apiUrl, ct);
            await File.WriteAllTextAsync(listCacheFile, json, ct);
            files = ParseDirectoryListing(json);
        }

        var sets = new List<QuoteSet>();
        foreach (var (name, downloadUrl) in files.Where(f => IsQuoteFile(f.name)))
        {
            try
            {
                var fileSets = await LoadSingleFileAsync(downloadUrl, interval, forceRefresh, ct);
                sets.AddRange(fileSets);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GitHub] Skipping {name}: {ex.Message}");
            }
        }
        return sets;
    }

    // ── URL normalisation ─────────────────────────────────────────────────────

    private static string NormaliseUrl(string url)
    {
        // https://github.com/user/repo/blob/branch/path/file.txt
        //   → https://raw.githubusercontent.com/user/repo/branch/path/file.txt
        if (url.Contains("github.com") && url.Contains("/blob/"))
        {
            return url
                .Replace("https://github.com/", "https://raw.githubusercontent.com/")
                .Replace("/blob/", "/");
        }

        // https://github.com/user/repo/tree/branch/path/
        //   → https://api.github.com/repos/user/repo/contents/path?ref=branch
        if (url.Contains("github.com") && url.Contains("/tree/"))
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            // segments: [user, repo, "tree", branch, ...path...]
            if (segments.Length >= 4)
            {
                string user   = segments[0];
                string repo   = segments[1];
                string branch = segments[3];
                string path   = segments.Length > 4 ? string.Join("/", segments[4..]) : "";
                string apiPath = string.IsNullOrEmpty(path) ? "" : $"/{path}";
                return $"https://api.github.com/repos/{user}/{repo}/contents{apiPath}?ref={branch}";
            }
        }

        return url; // already a raw or API URL
    }

    private static bool IsFolderUrl(string url) =>
        url.Contains("api.github.com/repos") && url.Contains("/contents");

    private static bool IsQuoteFile(string name) =>
        name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    // ── GitHub API directory listing parser ───────────────────────────────────

    private static List<(string name, string downloadUrl)> ParseDirectoryListing(string json)
    {
        var result = new List<(string, string)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // GitHub returns an array for directory contents
            if (root.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in root.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeEl)) continue;
                if (typeEl.GetString() != "file") continue;

                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var dl   = item.TryGetProperty("download_url", out var d) ? d.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(dl)) result.Add((name, dl));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GitHub] Directory parse error: {ex.Message}");
        }
        return result;
    }

    // ── Cache helpers ─────────────────────────────────────────────────────────

    private string GetCacheFilePath(string url)
    {
        // Build a safe filename from a hash of the URL
        var hash = Math.Abs(url.GetHashCode()).ToString("X8");
        var ext  = url.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ".csv" : ".txt";
        return Path.Combine(_cacheDir, $"{hash}{ext}");
    }

    private static TimeSpan? GetCacheAge(string cacheFile)
    {
        if (!File.Exists(cacheFile)) return null;
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile);
    }

    private static TimeSpan GetMaxAge(CacheRefreshInterval interval) => interval switch
    {
        CacheRefreshInterval.Daily   => TimeSpan.FromDays(1),
        CacheRefreshInterval.Weekly  => TimeSpan.FromDays(7),
        CacheRefreshInterval.Monthly => TimeSpan.FromDays(30),
        _                            => TimeSpan.MaxValue // Manual = never auto-refresh
    };

    private IReadOnlyList<QuoteSet> LoadFromCacheFallback(string url)
    {
        var cacheFile = GetCacheFilePath(url);
        if (!File.Exists(cacheFile)) return Array.Empty<QuoteSet>();
        try
        {
            var set = _localLoader.LoadFile(cacheFile);
            return set.IsEmpty ? Array.Empty<QuoteSet>() : new[] { set };
        }
        catch
        {
            return Array.Empty<QuoteSet>();
        }
    }

    // ── Content parser ────────────────────────────────────────────────────────

    private static QuoteSet? ParseContent(string url, string content)
    {
        // Determine name from URL
        var fileName = Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "GitHub Quotes";

        var lines = content.Split('\n');
        var ext = url.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ".csv" : ".txt";

        // Use LocalQuoteLoader parsing logic via a temporary file
        var tmp = Path.GetTempFileName() + ext;
        try
        {
            File.WriteAllText(tmp, content);
            var loader = new LocalQuoteLoader();
            var set = loader.LoadFile(tmp);
            // Rebuild with correct name and origin
            return new QuoteSet
            {
                Name       = fileName,
                SourcePath = url,
                Origin     = QuoteSetOrigin.GitHub,
                Quotes     = set.Quotes,
                LoadedAt   = DateTime.UtcNow
            };
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    // ── HttpClient factory ────────────────────────────────────────────────────

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("QuoteScreensaver", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public void Dispose() { /* HttpClient is static/shared */ }
}
