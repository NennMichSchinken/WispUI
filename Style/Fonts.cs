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
    private static IFontHandle? s_screenTitle;
    private static IFontHandle? s_title;
    private static IFontHandle? s_body;
    private static IFontHandle? s_small;

    /// <summary>The name of the screen you are on.</summary>
    public static IFontHandle ScreenTitle => s_screenTitle ?? Fallback;

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
        s_screenTitle = atlas.NewGameFontHandle(Style(Tokens.FontRole.ScreenTitle, Tokens.FontRole.ScreenTitlePx));
        s_title = atlas.NewGameFontHandle(Style(Tokens.FontRole.Title, Tokens.FontRole.TitlePx));
        s_body = atlas.NewGameFontHandle(Style(Tokens.FontRole.Body, Tokens.FontRole.BodyPx));
        s_small = atlas.NewGameFontHandle(Style(Tokens.FontRole.Small, Tokens.FontRole.SmallPx));
    }

    /// <summary>
    /// Builds the style for one role. At scale 1.0 the size is left exactly as the game
    /// ships it, so the bitmap is drawn one glyph pixel to one screen pixel and the text is
    /// as sharp as the game's own. Any other scale has to resample — that is unavoidable
    /// with a bitmap face, and it is the honest cost of the slider.
    /// </summary>
    private static GameFontStyle Style(GameFontFamilyAndSize familyAndSize, float targetPx)
    {
        GameFontStyle style = new(familyAndSize);

        // A role either takes the step's own size — the sharp case — or asks for a size of
        // its own, which costs a resample. Either way the interface scale applies on top.
        float wanted = targetPx > Tokens.FontRole.Native ? targetPx : style.SizePx;
        float scaled = Tokens.Scale == 1f ? wanted : MathF.Round(wanted * Tokens.Scale);

        if (scaled != style.SizePx)
        {
            style.SizePx = scaled;
        }

        return style;
    }

    public static void Dispose()
    {
        s_screenTitle?.Dispose();
        s_title?.Dispose();
        s_body?.Dispose();
        s_small?.Dispose();
        s_screenTitle = null;
        s_title = null;
        s_body = null;
        s_small = null;
    }
}
