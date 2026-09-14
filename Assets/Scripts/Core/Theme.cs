using UnityEngine;

/// <summary>
/// ONE-FILE RE-SKIN: every menu, panel, and effect pulls its colors from here.
///
/// Current direction (FIRED. — see Docs/FIRED_GDD.md): CARTOON CYBERPUNK,
/// maximum color. Neon pink / cyan / yellow / purple / acid green over deep
/// violet. Color-meaning rules:
///   cyan   = interactive / UI selection
///   pink+yellow = celebration (titles, bones, score)
///   red-pink    = damage / death
///   green       = combos (reserved — use nowhere else)
///
/// Scenes bake these colors in at build time — change values here, then re-run
/// Tools > Jam Scaffold > Build All Scenes to regenerate the shell.
/// </summary>
public static class Theme
{
    // ---- Core palette -------------------------------------------------------
    public static readonly Color Background   = FromHex("#0D0221"); // deep violet-black
    public static readonly Color BackgroundHi = FromHex("#2A0E4F"); // violet glow (gradient top)
    public static readonly Color Panel        = FromHex("#1A0B38F0");
    public static readonly Color PanelDim     = FromHex("#05010DC8");

    public static readonly Color Text         = FromHex("#F4F7FF"); // near-white, cool
    public static readonly Color TextDim      = FromHex("#8B7FB8"); // muted violet

    /// <summary>Primary interactive accent — NEON CYAN (menu selection, HUD).</summary>
    public static readonly Color Accent       = FromHex("#00F0FF");
    public static readonly Color AccentSoft   = FromHex("#00F0FF33");

    // ---- The neon set (game + effects) --------------------------------------
    public static readonly Color NeonPink   = FromHex("#FF2A6D");
    public static readonly Color NeonCyan   = FromHex("#00F0FF");
    public static readonly Color NeonYellow = FromHex("#FFE600");
    public static readonly Color NeonPurple = FromHex("#7122FA");
    public static readonly Color NeonGreen  = FromHex("#39FF14"); // combos ONLY

    // ---- Button/selectable tint states --------------------------------------
    public static readonly Color ButtonNormal      = FromHex("#251345");
    public static readonly Color ButtonHighlighted = FromHex("#3B1E6E");
    public static readonly Color ButtonPressed     = FromHex("#160A2E");
    public static readonly Color ButtonDisabled    = FromHex("#25134566");

    public static readonly Color Positive = FromHex("#39FF14"); // "YOU WIN"
    public static readonly Color Negative = FromHex("#FF3864"); // "GAME OVER"

    // ---- Effects / decoration ------------------------------------------------
    public static readonly Color TitleGradTop    = FromHex("#FFE600"); // yellow → pink wordmark
    public static readonly Color TitleGradBottom = FromHex("#FF2A6D");
    public static readonly Color GlowAmber       = FromHex("#FF2A6D40"); // soft glow behind titles (pink now)
    public static readonly Color ButtonGlow      = FromHex("#00F0FFCC"); // selection ring/marker
    public static readonly Color NebulaPurple    = FromHex("#7122FA30"); // sky blobs — louder than before
    public static readonly Color NebulaMagenta   = FromHex("#FF2A6D22");
    public static readonly Color NebulaTeal      = FromHex("#00F0FF1E");
    public static readonly Color PanelShadow     = FromHex("#00000080");
    public static readonly Color Divider         = FromHex("#00F0FF55"); // thin rules — cyan

    /// <summary>Parses "#RRGGBB" or "#RRGGBBAA". Falls back to magenta so mistakes are obvious.</summary>
    public static Color FromHex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
