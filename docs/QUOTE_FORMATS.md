# Quote File Formats

This screensaver can load quotes from **local files on your PC** and from **GitHub URLs**.
Two file formats are supported: plain text (`.txt`) and comma-separated values (`.csv`).

---

## Where to put your files

Place your quote files in any folder you like, then point the screensaver to that folder:

1. Open **Screen Saver Settings** → click **Settings…**
2. Go to the **Sources** tab
3. Set **Local quotes folder** to the folder containing your files
4. Make sure **Source mode** includes *Local* or *Both*

The screensaver scans that folder for every `.txt` and `.csv` file it finds.
Each file becomes its own named quote set (named after the filename).

---

## Plain-text format (`.txt`)

The simplest format — one quote per line, author optional.

### Rules

| Rule | Detail |
|------|--------|
| One quote per line | Each non-blank line is treated as one quote |
| Blank lines are ignored | Use them freely to group quotes visually |
| Lines starting with `#` are comments | The rest of that line is ignored |
| Author separator | Add ` -- ` (space, two dashes, space) at the end of a line to split quote from author |
| No author? That's fine | The quote is shown without attribution |
| Encoding | UTF-8 recommended; ASCII also works |

### Examples

**Quotes without authors:**
```
The journey of a thousand miles begins with one step.
Be the change you wish to see in the world.
Not all those who wander are lost.
```

**Quotes with authors (using ` -- ` separator):**
```
# Bible
I can do all things through Christ who strengthens me. -- Philippians 4:13
Fear not, for I am with you. -- Isaiah 41:10

# Saints
Not all of us can do great things. But we can do small things with great love. -- St. Teresa of Calcutta
Pray as though everything depended on God. Work as though everything depended on you. -- St. Augustine
```

**Mixed (some with authors, some without):**
```
The best is yet to come.
Every day is a gift. -- Unknown
Keep going — you're closer than you think.
```

### Complete example file → [`quotes/my_quotes_example.txt`](../quotes/my_quotes_example.txt)

---

## CSV format (`.csv`)

More structured — useful when every quote has an author or when importing from a spreadsheet.

### Rules

| Rule | Detail |
|------|--------|
| First row (header) | Must be `Quote,Author` (case-insensitive; `Author` column is optional) |
| One quote per row | Each data row after the header is one quote |
| Quoting fields | Wrap a field in `"double quotes"` if it contains a comma or a line break |
| Escaping quotes inside a field | Double the quote character: `""` becomes `"` in the output |
| Empty author cell | Leave it blank — the quote is shown without attribution |
| Blank rows | Ignored |
| Lines starting with `#` | Ignored (comment rows) |
| Encoding | UTF-8 recommended |

### Column order

The screensaver always expects **Quote first, Author second**:

```
Quote,Author
```

A header with only `Quote` (no Author column) is also valid — all quotes will be shown without attribution.

### Examples

**Standard two-column CSV:**
```csv
Quote,Author
"I can do all things through Christ who strengthens me.",Philippians 4:13
"For God so loved the world that he gave his only Son.",John 3:16
"Not all of us can do great things. But we can do small things with great love.",St. Teresa of Calcutta
```

**Quote containing a comma (must be quoted):**
```csv
Quote,Author
"When I was young, I admired clever people. Now that I am old, I admire kind people.",Abraham Joshua Heschel
```

**Quote containing a double-quote character (double it up):**
```csv
Quote,Author
"He said ""go forth"" and they went.",Traditional
```

**Quote-only CSV (no Author column):**
```csv
Quote
Be the change you wish to see in the world.
The journey of a thousand miles begins with one step.
```

**With comment rows:**
```csv
Quote,Author
# ----- Scripture -----
"Trust in the Lord with all your heart.",Proverbs 3:5
# ----- Saints -----
"Joy is a net of love by which you can catch souls.",St. Teresa of Calcutta
```

### Complete example file → [`quotes/my_quotes_example.csv`](../quotes/my_quotes_example.csv)

---

## GitHub URL source

In addition to local files, the screensaver can fetch quote files directly from GitHub.
Supported URL forms:

| URL type | Example |
|----------|---------|
| Raw file URL | `https://raw.githubusercontent.com/user/repo/main/quotes.csv` |
| GitHub blob URL | `https://github.com/user/repo/blob/main/quotes.txt` |
| GitHub folder URL | `https://github.com/user/repo/tree/main/quotes/` |
| GitHub Contents API | `https://api.github.com/repos/user/repo/contents/quotes` |

When you point to a **folder**, every `.txt` and `.csv` file in that folder is loaded as a separate set.

The screensaver caches downloaded files locally in `%AppData%\QuoteScreensaver\cache\`
and refreshes them according to your **Cache refresh** setting (Daily / Weekly / Monthly / Manual).

### Bundled default set

Out of the box, the screensaver loads:

> **Faith & Inspiration** — 49 quotes from Scripture, Catholic Saints, and compatible Christian voices  
> `https://raw.githubusercontent.com/Saintapedia/screensaver/main/quotes/faith_inspiration.csv`

You can see this in the Settings dialog under the **Sources** tab → GitHub Presets dropdown.

---

## Tips

- **File names become set names.** `my_prayers.csv` shows up in the Settings dialog as *my_prayers*.
  Name your files descriptively.
- **Multiple files = multiple sets.** You can enable or disable individual sets in Settings → Sources → Quote Sets.
- **Long quotes are fine.** The screensaver auto-sizes text to fit the screen (down to a minimum readable size).
- **Special characters work.** Em-dashes `—`, smart quotes `"`, accented letters, etc. are all fine in UTF-8 files.
- **Test before installing.** Press `A` while the screensaver is running to toggle author display, or `R` to reload quotes from disk without restarting.

---

## Quick-reference card

```
.txt format
───────────────────────────────────────
# Comment line (ignored)
Quote text here.
Quote with author -- Author Name
Another quote -- Source

.csv format
───────────────────────────────────────
Quote,Author                          ← required header row
"Quote text here.",Author Name
"Quote with a, comma inside.",Source
"Quote with a ""quoted word"".",Auth
Plain quote no author,
```
