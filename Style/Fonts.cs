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
        s_title = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, Tokens.FontSize.Title));
        s_body = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, Tokens.FontSize.Body));
        s_small = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, Tokens.FontSize.Small));
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
