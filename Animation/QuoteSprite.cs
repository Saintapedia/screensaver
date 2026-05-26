// Animation/QuoteSprite.cs
// A single animated quote with physics, fade lifecycle, and self-contained rendering.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using QuoteScreensaver.Models;

namespace QuoteScreensaver.Animation;

/// <summary>
/// Represents one floating quote on screen. Handles its own movement,
/// bouncing, fade lifecycle, and drawing.
/// </summary>
public sealed class QuoteSprite
{
    // ── Data ──────────────────────────────────────────────────────────────────
    public Quote Quote     { get; }
    public string DisplayText { get; private set; }

    // ── Physics ───────────────────────────────────────────────────────────────
    public PointF Position { get; set; }     // top-left of text block, in screen pixels
    public PointF Velocity { get; set; }     // pixels per second

    // ── Visual ───────────────────────────────────────────────────────────────
    /// <summary>Current opacity, 0..1. Applied as alpha when drawing.</summary>
    public float Alpha     { get; set; } = 0f;
    public FadeState State { get; private set; } = FadeState.Hidden;
    /// <summary>Seconds spent in the current <see cref="FadeState"/>.</summary>
    public float StateTimer { get; private set; }

    public Font    Font    { get; }
    public SizeF   Size    { get; }   // pre-measured bounding box of the text

    // ── Derived ───────────────────────────────────────────────────────────────
    public RectangleF Bounds => new(Position, Size);
    public bool IsFullyHidden  => State == FadeState.Hidden;
    public bool IsFullyVisible => State == FadeState.Visible;

    // ── String format (shared) ────────────────────────────────────────────────
    private static readonly StringFormat _sf = new()
    {
        Alignment     = StringAlignment.Center,
        LineAlignment = StringAlignment.Near,
        FormatFlags   = StringFormatFlags.LineLimit,
        Trimming      = StringTrimming.Word
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="quote">The quote to display.</param>
    /// <param name="showAuthor">Whether to append "— Author" below the text.</param>
    /// <param name="screenBounds">Full screen rectangle (used to compute font + initial position).</param>
    /// <param name="speedPixelsPerSec">Base movement speed.</param>
    /// <param name="fontFamily">Font family name.</param>
    public QuoteSprite(Quote quote, bool showAuthor, RectangleF screenBounds,
                       float speedPixelsPerSec, string fontFamily)
    {
        Quote       = quote;
        DisplayText = quote.GetDisplayText(showAuthor);

        // ── Choose font size based on quote length ─────────────────────────
        Font = ChooseFont(DisplayText, screenBounds, fontFamily);

        // ── Measure text to determine bounding box ────────────────────────
        float maxWidth = screenBounds.Width * 0.62f;
        Size = MeasureText(DisplayText, Font, maxWidth);

        // ── Random starting position (fully on-screen) ────────────────────
        float margin = 40f;
        float x = Random.Shared.NextSingle() * Math.Max(1, screenBounds.Width  - Size.Width  - margin * 2) + margin;
        float y = Random.Shared.NextSingle() * Math.Max(1, screenBounds.Height - Size.Height - margin * 2) + margin;
        Position = new PointF(x, y);

        // ── Random velocity direction with constant speed ──────────────────
        double angle = Random.Shared.NextDouble() * Math.PI * 2;
        float speed  = speedPixelsPerSec * (0.7f + Random.Shared.NextSingle() * 0.6f);
        Velocity = new PointF((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartFadeIn()
    {
        State      = FadeState.FadingIn;
        StateTimer = 0f;
        Alpha      = 0f;
    }

    public void StartFadeOut()
    {
        if (State == FadeState.FadingOut || State == FadeState.Hidden) return;
        State      = FadeState.FadingOut;
        StateTimer = 0f;
    }

    public void RefreshDisplayText(bool showAuthor)
    {
        DisplayText = Quote.GetDisplayText(showAuthor);
    }

    /// <summary>
    /// Advances physics and fade; call once per frame with elapsed seconds.
    /// </summary>
    /// <param name="dt">Delta time in seconds.</param>
    /// <param name="screenBounds">Screen area to bounce within.</param>
    /// <param name="fadeDuration">Duration of each fade phase in seconds.</param>
    /// <param name="others">Other sprites for collision detection.</param>
    /// <param name="enableCollisions">Whether inter-sprite collisions are active.</param>
    public void Update(float dt, RectangleF screenBounds, float fadeDuration,
                       IReadOnlyList<QuoteSprite> others, bool enableCollisions)
    {
        if (State == FadeState.Hidden) return;

        UpdateFade(dt, fadeDuration);
        UpdatePhysics(dt, screenBounds);

        if (enableCollisions)
            ResolveCollisions(others);
    }

    /// <summary>Draws the sprite onto <paramref name="g"/>.</summary>
    public void Draw(Graphics g, Color baseTextColor, bool showShadow)
    {
        if (State == FadeState.Hidden || Alpha <= 0.004f) return;

        int a = (int)(Alpha * 255f);
        a = Math.Clamp(a, 0, 255);

        var textBrushColor = Color.FromArgb(a, baseTextColor.R, baseTextColor.G, baseTextColor.B);

        var rect = new RectangleF(Position.X, Position.Y, Size.Width, Size.Height);

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode     = SmoothingMode.AntiAlias;

        if (showShadow)
        {
            int sa = (int)(Alpha * 140f);
            var shadowColor = Color.FromArgb(Math.Clamp(sa, 0, 255), 0, 0, 0);
            using var shadowBrush = new SolidBrush(shadowColor);
            var shadowRect = new RectangleF(rect.X + 2.5f, rect.Y + 2.5f, rect.Width, rect.Height);
            g.DrawString(DisplayText, Font, shadowBrush, shadowRect, _sf);
        }

        using var textBrush = new SolidBrush(textBrushColor);
        g.DrawString(DisplayText, Font, textBrush, rect, _sf);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UpdateFade(float dt, float fadeDuration)
    {
        StateTimer += dt;

        switch (State)
        {
            case FadeState.FadingIn:
                Alpha = Math.Min(1f, StateTimer / fadeDuration);
                if (Alpha >= 1f) { Alpha = 1f; State = FadeState.Visible; StateTimer = 0f; }
                break;

            case FadeState.FadingOut:
                Alpha = Math.Max(0f, 1f - StateTimer / fadeDuration);
                if (Alpha <= 0f) { Alpha = 0f; State = FadeState.Hidden; StateTimer = 0f; }
                break;
        }
    }

    private void UpdatePhysics(float dt, RectangleF screen)
    {
        var newX = Position.X + Velocity.X * dt;
        var newY = Position.Y + Velocity.Y * dt;
        var vx   = Velocity.X;
        var vy   = Velocity.Y;

        // Bounce off left/right edges
        if (newX < 0f)                              { newX = 0f; vx = Math.Abs(vx); }
        if (newX + Size.Width > screen.Width)       { newX = screen.Width - Size.Width; vx = -Math.Abs(vx); }

        // Bounce off top/bottom edges
        if (newY < 0f)                              { newY = 0f; vy = Math.Abs(vy); }
        if (newY + Size.Height > screen.Height)     { newY = screen.Height - Size.Height; vy = -Math.Abs(vy); }

        Position = new PointF(newX, newY);
        Velocity = new PointF(vx, vy);
    }

    private void ResolveCollisions(IReadOnlyList<QuoteSprite> others)
    {
        foreach (var other in others)
        {
            if (other == this || other.State == FadeState.Hidden) continue;

            var myBounds    = Bounds;
            var otherBounds = other.Bounds;

            if (!myBounds.IntersectsWith(otherBounds)) continue;

            // Simple elastic collision: exchange velocity components
            // Push sprites apart along the axis of least penetration
            float overlapX = Math.Min(myBounds.Right,  otherBounds.Right)  - Math.Max(myBounds.Left, otherBounds.Left);
            float overlapY = Math.Min(myBounds.Bottom, otherBounds.Bottom) - Math.Max(myBounds.Top,  otherBounds.Top);

            if (overlapX < overlapY)
            {
                // Horizontal collision
                var vx1 = Velocity.X; var vx2 = other.Velocity.X;
                Velocity = new PointF(vx2 * 0.9f, Velocity.Y);
                // Nudge apart
                float nudge = overlapX / 2f + 1f;
                Position = new PointF(Position.X + (Velocity.X >= 0 ? -nudge : nudge), Position.Y);
            }
            else
            {
                // Vertical collision
                var vy1 = Velocity.Y; var vy2 = other.Velocity.Y;
                Velocity = new PointF(Velocity.X, vy2 * 0.9f);
                float nudge = overlapY / 2f + 1f;
                Position = new PointF(Position.X, Position.Y + (Velocity.Y >= 0 ? -nudge : nudge));
            }
        }
    }

    // ── Font / measurement ────────────────────────────────────────────────────

    private static readonly float[] FontSizes = { 52f, 44f, 36f, 30f, 26f, 22f, 18f };

    private static Font ChooseFont(string text, RectangleF screen, string fontFamily)
    {
        // Target: text should not exceed 60% screen width or 35% screen height
        float maxW = screen.Width  * 0.60f;
        float maxH = screen.Height * 0.35f;

        foreach (var size in FontSizes)
        {
            bool italic = size >= 26f;
            var style   = italic ? FontStyle.Italic : FontStyle.Regular;
            var font    = new Font(fontFamily, size, style, GraphicsUnit.Point);
            var measured = MeasureText(text, font, maxW);

            if (measured.Height <= maxH)
                return font;

            font.Dispose();
        }

        return new Font(fontFamily, 18f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static SizeF MeasureText(string text, Font font, float maxWidth)
    {
        using var bmp = new Bitmap(1, 1);
        using var g   = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        return g.MeasureString(text, font, (int)maxWidth, _sf);
    }
}
