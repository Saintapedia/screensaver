// Animation/FadeState.cs
namespace QuoteScreensaver.Animation;

/// <summary>The lifecycle stage of a QuoteSprite on screen.</summary>
public enum FadeState
{
    /// <summary>Sprite is off-screen; alpha = 0, not updated.</summary>
    Hidden,

    /// <summary>Sprite is fading in from alpha 0 → 1.</summary>
    FadingIn,

    /// <summary>Fully visible; alpha = 1. Moving and bouncing.</summary>
    Visible,

    /// <summary>Sprite is fading out from alpha 1 → 0.</summary>
    FadingOut
}
