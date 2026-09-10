using System;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using WispUI.Core;

namespace WispUI.Style;

/// <summary>
/// WispUI draws in Axis, the game's own UI face, loaded straight from the game files.
/// No bundled foreign font: a config window that is meant to blend in must not give
/// itself away by its lettering.
/// <para>Handles are built once for the current scale and rebuilt when the scale changes.</para>
/// </summary>
internal static class Fonts
{
    private static IFontHandle? s_title;
    private static IFontHandle? s_body;
    private static IFontHandle? s_small;

    /// <summary>Window title and section headings.</summary>
    public static IFontHandle Title => s_title ?? Fallback;

    /// <summary>Body copy, labels, buttons.</summary>
    public static IFontHandle Body => s_body ?? Fallback;

    /// <summary>Hints, badges, version chips.</summary>
    public static IFontHandle Small => s_small ?? Fallback;

    private static IFontHandle Fallback => Services.PluginInterface.UiBuilder.DefaultFontHandle;

    /// <summary>True once the handles have been built at least once.</summary>
    public static bool Ready => s_body is not null;

    /// <summary>Builds the handles for the scale currently set in <see cref="Tokens"/>.</summary>
    public static void Rebuild()
    {
        Dispose();

        IFontAtlas atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        s_title = atlas.NewGameFontHandle(Style(Tokens.FontRole.Title));
        s_body = atlas.NewGameFontHandle(Style(Tokens.FontRole.Body));
        s_small = atlas.NewGameFontHandle(Style(Tokens.FontRole.Small));
    }

    /// <summary>
    /// Builds the style for one role. At scale 1.0 the size is left exactly as the game
    /// ships it, so the bitmap is drawn one glyph pixel to one screen pixel and the text is
    /// as sharp as the game's own. Any other scale has to resample — that is unavoidable
    /// with a bitmap face, and it is the honest cost of the slider.
    /// </summary>
    private static GameFontStyle Style(GameFontFamilyAndSize familyAndSize)
    {
        GameFontStyle style = new(familyAndSize);
        if (Tokens.Scale != 1f)
        {
            style.SizePx = MathF.Round(style.SizePx * Tokens.Scale);
        }

        return style;
    }

    public static void Dispose()
    {
        s_title?.Dispose();
        s_body?.Dispose();
        s_small?.Dispose();
        s_title = null;
        s_body = null;
        s_small = null;
    }
}
