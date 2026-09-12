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

    /// <summary>
    /// The same four steps again in the face the HUD was set to, or all null while that face
    /// is Axis — which is the default, so the common case carries no second set at all.
    /// <para>
    /// 🔴 That emptiness is the point, not an optimisation left half done. Every handle here
    /// is a font lock taken once per frame in <see cref="Ink.BeginFrame"/>, and a lock
    /// allocates (API notes §3.6). Four handles for a face nobody chose would double the
    /// frame's font allocations to serve a setting at its default.
    /// </para>
    /// </summary>
    private static readonly IFontHandle?[] Hud = new IFontHandle?[4];

    private static HudFontFace s_hudFace = HudFontFace.Axis;

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

    /// <summary>
    /// The HUD's face at one of the four steps, or null when the HUD writes in Axis and the
    /// window's own handle for that step is the answer.
    /// </summary>
    public static IFontHandle? HudStep(int step) =>
        step >= 0 && step < Hud.Length ? Hud[step] : null;

    /// <summary>
    /// Sets the face HUD elements write in, rebuilding only when it actually changes. Called
    /// from the draw path, so the common case is a comparison of two enums.
    /// </summary>
    public static void SetHudFace(HudFontFace face)
    {
        if (face == s_hudFace)
        {
            return;
        }

        s_hudFace = face;
        Rebuild();
    }

    /// <summary>Builds the handles for the scale currently set in <see cref="Tokens"/>.</summary>
    public static void Rebuild()
    {
        Dispose();

        IFontAtlas atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        s_screenTitle = atlas.NewGameFontHandle(Style(Tokens.FontRole.ScreenTitle, Tokens.FontRole.ScreenTitlePx));
        s_title = atlas.NewGameFontHandle(Style(Tokens.FontRole.Title, Tokens.FontRole.TitlePx));
        s_body = atlas.NewGameFontHandle(Style(Tokens.FontRole.Body, Tokens.FontRole.BodyPx));
        s_small = atlas.NewGameFontHandle(Style(Tokens.FontRole.Small, Tokens.FontRole.SmallPx));

        if (s_hudFace == HudFontFace.Axis)
        {
            return;
        }

        // The same four sizes, so a size in pixels lands on the same step whichever face is
        // set and nothing that measures text has to know which one it got.
        GameFontFamily family = HudText.Family(s_hudFace);
        Hud[0] = atlas.NewGameFontHandle(HudStyle(family, Sized(Tokens.FontRole.ScreenTitle, Tokens.FontRole.ScreenTitlePx)));
        Hud[1] = atlas.NewGameFontHandle(HudStyle(family, Sized(Tokens.FontRole.Title, Tokens.FontRole.TitlePx)));
        Hud[2] = atlas.NewGameFontHandle(HudStyle(family, Sized(Tokens.FontRole.Body, Tokens.FontRole.BodyPx)));
        Hud[3] = atlas.NewGameFontHandle(HudStyle(family, Sized(Tokens.FontRole.Small, Tokens.FontRole.SmallPx)));
    }

    /// <summary>
    /// A face at an exact pixel size. Dalamud picks the nearest size the game ships of that
    /// family and resamples to what was asked for, which is why every face can answer to the
    /// same four steps even though none of them ships in those sizes.
    /// </summary>
    private static GameFontStyle HudStyle(GameFontFamily family, float sizePx) => new(family, sizePx);

    /// <summary>The pixel size a window role ends up at, so the HUD can ask for the same one.</summary>
    private static float Sized(GameFontFamilyAndSize familyAndSize, float targetPx) =>
        Style(familyAndSize, targetPx).SizePx;

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

        for (int i = 0; i < Hud.Length; i++)
        {
            Hud[i]?.Dispose();
            Hud[i] = null;
        }
    }
}
