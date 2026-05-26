// Services/QuoteSetManager.cs
// Central coordinator: loads local + GitHub quote sets and serves random quotes.
using QuoteScreensaver.Models;

namespace QuoteScreensaver.Services;

/// <summary>
/// Manages all loaded <see cref="QuoteSet"/>s and provides random quote selection.
/// Initialization is asynchronous (GitHub loading), but built-in defaults are
/// available immediately so the screensaver never starts blank.
/// </summary>
public sealed class QuoteSetManager
{
    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly LocalQuoteLoader _localLoader = new();
    private readonly GitHubQuoteLoader _githubLoader;

    private readonly List<QuoteSet> _sets = new();
    private readonly List<Quote> _allQuotes = new();
    private readonly List<int> _shuffledIndices = new();
    private int _shufflePos = 0;
    private readonly List<int[]> _history = new();   // groups of shown quote-indices
    private int _historyPos = -1;

    // Thread-safety for background GitHub refresh
    private readonly object _lock = new();

    public QuoteSetManager(AppSettings settings, SettingsManager settingsManager)
    {
        _settings = settings;
        _settingsManager = settingsManager;
        _githubLoader = new GitHubQuoteLoader(settingsManager.CacheDirectory);
    }

    /// <summary>
    /// Returns all currently loaded <see cref="QuoteSet"/>s (thread-safe snapshot).
    /// </summary>
    public IReadOnlyList<QuoteSet> Sets
    {
        get { lock (_lock) { return _sets.ToArray(); } }
    }

    /// <summary>Total number of quotes across all enabled sets.</summary>
    public int TotalQuotes
    {
        get { lock (_lock) { return _allQuotes.Count; } }
    }

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads local quotes synchronously (instant), then starts a background
    /// task to load GitHub quotes. The screensaver can start as soon as this
    /// returns — GitHub quotes will merge in when available.
    /// </summary>
    public void InitializeSync()
    {
        // Always load built-in defaults first so there's something to show
        LoadBuiltInDefaults();

        if (_settings.SourceMode != QuoteSourceMode.GitHub)
            LoadLocalQuotes();

        // Fire-and-forget GitHub loading
        if (_settings.SourceMode != QuoteSourceMode.Local &&
            !string.IsNullOrWhiteSpace(_settings.GitHubUrl))
        {
            _ = LoadGitHubAsync();
        }
    }

    /// <summary>Forces a full reload of all sources (used by 'R' keyboard shortcut).</summary>
    public async Task ReloadAsync()
    {
        lock (_lock)
        {
            _sets.Clear();
            _allQuotes.Clear();
        }

        LoadBuiltInDefaults();

        if (_settings.SourceMode != QuoteSourceMode.GitHub)
            LoadLocalQuotes();

        if (_settings.SourceMode != QuoteSourceMode.Local &&
            !string.IsNullOrWhiteSpace(_settings.GitHubUrl))
        {
            await LoadGitHubAsync(forceRefresh: true);
        }
    }

    // ── Quote selection ───────────────────────────────────────────────────────

    /// <summary>
    /// Picks <paramref name="count"/> distinct random quotes for the next display group.
    /// Uses a shuffle-bag to avoid immediate repeats.
    /// </summary>
    public Quote[] NextGroup(int count)
    {
        lock (_lock)
        {
            if (_allQuotes.Count == 0) return Array.Empty<Quote>();

            count = Math.Min(count, _allQuotes.Count);
            var group = PickDistinct(count);

            // Push to history, trim old entries
            if (_historyPos < _history.Count - 1)
                _history.RemoveRange(_historyPos + 1, _history.Count - _historyPos - 1);
            _history.Add(group.Select(q => _allQuotes.IndexOf(q)).ToArray());
            if (_history.Count > 50) _history.RemoveAt(0);
            _historyPos = _history.Count - 1;

            return group.ToArray();
        }
    }

    /// <summary>Returns the previous group from history, or null if at beginning.</summary>
    public Quote[]? PreviousGroup()
    {
        lock (_lock)
        {
            if (_historyPos <= 0) return null;
            _historyPos--;
            return _history[_historyPos]
                   .Where(i => i >= 0 && i < _allQuotes.Count)
                   .Select(i => _allQuotes[i])
                   .ToArray();
        }
    }

    // ── Private loading ───────────────────────────────────────────────────────

    private void LoadBuiltInDefaults()
    {
        var defaults = new QuoteSet
        {
            Name       = "Built-in Defaults",
            SourcePath = "(built-in)",
            Origin     = QuoteSetOrigin.BuiltIn,
            Quotes     = BuiltInQuotes,
            LoadedAt   = DateTime.UtcNow
        };
        MergeSet(defaults);
    }

    private void LoadLocalQuotes()
    {
        var folder = string.IsNullOrWhiteSpace(_settings.LocalQuotesFolder)
                   ? AppContext.BaseDirectory
                   : _settings.LocalQuotesFolder;

        var sets = _localLoader.LoadFromFolder(folder);
        foreach (var s in sets) MergeSet(s);
    }

    private async Task LoadGitHubAsync(bool forceRefresh = false)
    {
        try
        {
            var sets = await _githubLoader.LoadAsync(
                _settings.GitHubUrl,
                _settings.CacheRefresh,
                forceRefresh);

            foreach (var s in sets) MergeSet(s);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuoteSetManager] GitHub load error: {ex.Message}");
        }
    }

    private void MergeSet(QuoteSet set)
    {
        if (set.IsEmpty) return;
        lock (_lock)
        {
            // Replace existing set with same name if reloading
            var existing = _sets.FindIndex(s => s.Name == set.Name && s.Origin == set.Origin);
            if (existing >= 0) _sets[existing] = set;
            else                _sets.Add(set);

            RebuildQuoteList();
        }
    }

    private void RebuildQuoteList()
    {
        // Must be called while holding _lock
        _allQuotes.Clear();

        var enabledSets = _settings.EnabledSets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim())
                                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var set in _sets)
        {
            // If EnabledSets is empty, all sets are enabled
            if (enabledSets.Count > 0 && !enabledSets.Contains(set.Name)) continue;
            _allQuotes.AddRange(set.Quotes);
        }

        Reshuffle();
    }

    private void Reshuffle()
    {
        _shuffledIndices.Clear();
        _shuffledIndices.AddRange(Enumerable.Range(0, _allQuotes.Count));
        var rng = Random.Shared;
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }
        _shufflePos = 0;
    }

    private List<Quote> PickDistinct(int count)
    {
        var result = new List<Quote>(count);
        var usedIndices = new HashSet<int>();

        for (int attempt = 0; attempt < _allQuotes.Count && result.Count < count; attempt++)
        {
            if (_shufflePos >= _shuffledIndices.Count) Reshuffle();
            int idx = _shuffledIndices[_shufflePos++];
            if (usedIndices.Add(idx)) result.Add(_allQuotes[idx]);
        }

        return result;
    }

    // ── Built-in default quotes ───────────────────────────────────────────────

    private static readonly IReadOnlyList<Quote> BuiltInQuotes = new[]
    {
        new Quote { Text = "The only way to do great work is to love what you do.",    Author = "Steve Jobs" },
        new Quote { Text = "In the middle of every difficulty lies opportunity.",       Author = "Albert Einstein" },
        new Quote { Text = "It does not matter how slowly you go as long as you do not stop.", Author = "Confucius" },
        new Quote { Text = "Life is what happens when you're busy making other plans.", Author = "John Lennon" },
        new Quote { Text = "The future belongs to those who believe in the beauty of their dreams.", Author = "Eleanor Roosevelt" },
        new Quote { Text = "Strive not to be a success, but rather to be of value.",   Author = "Albert Einstein" },
        new Quote { Text = "You must be the change you wish to see in the world.",     Author = "Mahatma Gandhi" },
        new Quote { Text = "Spread love everywhere you go. Let no one ever come to you without leaving happier.", Author = "Mother Teresa" },
        new Quote { Text = "When you reach the end of your rope, tie a knot in it and hang on.", Author = "Franklin D. Roosevelt" },
        new Quote { Text = "Always remember that you are absolutely unique. Just like everyone else.", Author = "Margaret Mead" },
        new Quote { Text = "Don't judge each day by the harvest you reap but by the seeds that you plant.", Author = "Robert Louis Stevenson" },
        new Quote { Text = "The best time to plant a tree was 20 years ago. The second best time is now.", Author = "Chinese Proverb" },
        new Quote { Text = "An unexamined life is not worth living.",                  Author = "Socrates" },
        new Quote { Text = "Spread your wings and let the fairy in you fly!",          Author = "Author Unknown" },
        new Quote { Text = "The journey of a thousand miles begins with one step.",    Author = "Lao Tzu" },
        new Quote { Text = "That which does not kill us, makes us stronger.",          Author = "Friedrich Nietzsche" },
        new Quote { Text = "Imagination is more important than knowledge.",            Author = "Albert Einstein" },
        new Quote { Text = "In three words I can sum up everything I've learned about life: it goes on.", Author = "Robert Frost" },
        new Quote { Text = "To be yourself in a world that is constantly trying to make you something else is the greatest accomplishment.", Author = "Ralph Waldo Emerson" },
        new Quote { Text = "Two roads diverged in a wood, and I took the one less travelled by, and that has made all the difference.", Author = "Robert Frost" },
        new Quote { Text = "You only live once, but if you do it right, once is enough.", Author = "Mae West" },
        new Quote { Text = "I've learned that people will forget what you said, people will forget what you did, but people will never forget how you made them feel.", Author = "Maya Angelou" },
        new Quote { Text = "Whether you think you can or you think you can't, you're right.", Author = "Henry Ford" },
        new Quote { Text = "The mind is everything. What you think you become.",       Author = "Buddha" },
        new Quote { Text = "Twenty years from now you will be more disappointed by the things that you didn't do than by the ones you did do.", Author = "Mark Twain" },
    };
}
