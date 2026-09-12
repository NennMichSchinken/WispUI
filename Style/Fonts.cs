using System;
using System.IO;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using WispUI.Core;

namespace WispUI.Style;

/// <summary>
/// Every font handle the suite holds, in two sets that answer different questions.
/// <para>
/// The window writes in Axis, the game's own UI face, read straight from the game files. No
/// foreign face there on purpose: a settings window meant to blend into FFXIV must not give
/// itself away by its lettering.
/// </para>
/// <para>
/// The HUD is the other case. Its text sits over the world at whatever size the player set,
/// so it gets handles built for exactly those sizes rather than four fixed steps with
/// everything in between stretched. That distinction is the whole reason two of the faces it
/// can be set to are vector faces shipped with the plugin — see <see cref="HudFontFace"/>.
/// </para>
/// </summary>
internal static class Fonts
{
    /// <summary>
    /// How many different text sizes one HUD can ask for at once. Three are in use — the
    /// name, the figure on the bar and the party number — and the fourth is slack so that
    /// adding a text does not immediately mean touching this.
    /// <para>
    /// It is a small number deliberately. Every size held here is a font lock taken once per
    /// frame, and a lock allocates (API notes §3.6), so this is a budget and not a cache.
    /// </para>
    /// </summary>
    private const int MaxHudSizes = 4;

    private static IFontHandle? s_screenTitle;
    private static IFontHandle? s_title;
    private static IFontHandle? s_body;
    private static IFontHandle? s_small;

    /// <summary>The HUD's handles and the exact pixel size each one was built for.</summary>
    private static readonly IFontHandle?[] HudHandle = new IFontHandle?[MaxHudSizes];
    private static readonly float[] HudSizePx = new float[MaxHudSizes];
    private static int s_hudCount;

    private static string s_hudFaceName = FontLibrary.DefaultName;

    /// <summary>The shipped face currently in memory, and its bytes.</summary>
    private static string? s_loadedFontFile;
    private static byte[]? s_loadedFontBytes;

    /// <summary>
    /// Why the chosen face is not the one being drawn, or null when all is well. Shown in the
    /// setting itself rather than only logged: a face that quietly falls back to another one
    /// looks like a face that loaded and is simply disappointing, which cost a whole test
    /// round to tell apart (Florian, 2026-09-12).
    /// </summary>
    private static string? s_faceProblem;

    /// <summary>Why the chosen face could not be used, or null when it could.</summary>
    public static string? FaceProblem => s_faceProblem;

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

    /// <summary>How many HUD sizes are currently held.</summary>
    public static int HudCount => s_hudCount;

    /// <summary>The handle for one held HUD size.</summary>
    public static IFontHandle? HudHandleAt(int index) =>
        index >= 0 && index < s_hudCount ? HudHandle[index] : null;

    /// <summary>The exact pixel size one held HUD handle was built for.</summary>
    public static float HudSizeAt(int index) =>
        index >= 0 && index < s_hudCount ? HudSizePx[index] : 0f;

    /// <summary>
    /// Brings the HUD's handles in line with the face and the sizes that are actually in use.
    /// <para>
    /// <paramref name="settled"/> is what keeps this off the slider. Building a handle throws
    /// the font atlas away and makes a new one, which is the brief stutter you see when the
    /// face changes; doing that on every pixel of a drag would make the drag unusable. So the
    /// caller passes false while a change is still pending and true once it has gone quiet,
    /// which is the same pause the configuration is written on.
    /// </para>
    /// </summary>
    /// <param name="settled">Whether the configuration has stopped changing.</param>
    /// <param name="face">The face the HUD is set to.</param>
    /// <param name="sizes">The pixel sizes in use. Duplicates and sizes past the budget are dropped.</param>
    public static void SyncHud(bool settled, string faceName, ReadOnlySpan<float> sizes)
    {
        if (!settled || !Ready)
        {
            return;
        }

        Span<float> wanted = stackalloc float[MaxHudSizes];
        int count = Gather(sizes, wanted);

        if (Matches(faceName, wanted[..count]))
        {
            return;
        }

        s_hudFaceName = faceName;
        RebuildHud(wanted[..count]);
    }

    /// <summary>
    /// Collects the distinct sizes actually worth a handle, rounded to whole pixels. Rounded
    /// because a glyph rasterised for 16.4 pixels and drawn at 16 is the soft case all over
    /// again, and because it is what makes two texts at the same size share one handle.
    /// </summary>
    private static int Gather(ReadOnlySpan<float> sizes, Span<float> into)
    {
        int count = 0;

        for (int i = 0; i < sizes.Length && count < MaxHudSizes; i++)
        {
            float size = MathF.Round(sizes[i]);

            if (size < 1f)
            {
                continue;
            }

            bool seen = false;
            for (int j = 0; j < count; j++)
            {
                if (into[j] == size)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                into[count++] = size;
            }
        }

        return count;
    }

    private static bool Matches(string faceName, ReadOnlySpan<float> sizes)
    {
        if (!string.Equals(faceName, s_hudFaceName, StringComparison.Ordinal) || sizes.Length != s_hudCount)
        {
            return false;
        }

        for (int i = 0; i < sizes.Length; i++)
        {
            if (HudSizePx[i] != sizes[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds one handle per size in the HUD's current face.</summary>
    private static void RebuildHud(ReadOnlySpan<float> sizes)
    {
        DisposeHud();
        s_faceProblem = null;

        IFontAtlas atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        FontChoice face = FontLibrary.Find(s_hudFaceName);

        for (int i = 0; i < sizes.Length; i++)
        {
            float size = sizes[i];
            HudSizePx[i] = size;

            if (face.FilePath is null)
            {
                // A face out of the game files. Dalamud picks the nearest size the game ships
                // and resamples; there is no way around that, which is the point of offering
                // the vector faces beside these.
                HudHandle[i] = atlas.NewGameFontHandle(new GameFontStyle(face.GameFamily, size));
                continue;
            }

            HudHandle[i] = BuildFromFile(atlas, face.FilePath, size);
        }

        s_hudCount = sizes.Length;
    }

    /// <summary>
    /// A shipped vector face, rasterised for exactly this size. Returns null if it could not
    /// be loaded, which leaves the caller drawing in the window's own face rather than not
    /// drawing at all — a missing font is a reason for plain text, never for no text.
    /// <para>
    /// 🔴 From bytes we read ourselves, not from a path handed to the toolkit. The first build
    /// passed the path and the result was indistinguishable from Axis in game, with no way to
    /// tell a face that failed to load from one that loaded and looked wrong (Florian,
    /// 2026-09-12). Reading the file here means the failure has a name and a line in the log,
    /// and the setting can say so out loud instead of silently showing the wrong face.
    /// </para>
    /// </summary>
    private static IFontHandle? BuildFromFile(IFontAtlas atlas, string path, float sizePx)
    {
        byte[]? bytes = FontBytes(path);

        if (bytes is null)
        {
            return null;
        }

        string name = Path.GetFileName(path);

        try
        {
            return atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(
                    tk => tk.AddFontFromMemory(bytes, new SafeFontConfig { SizePx = sizePx }, name)));
        }
        catch (Exception ex)
        {
            s_faceProblem = $"{name} could not be rasterised. It may not be a usable font file.";
            Services.Log.Error(ex, "A font could not be rasterised: {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// The bytes of a shipped face, read once and kept. Read once because a rebuild happens
    /// per size, and reading the same 60 KB three times to build three sizes of one face is
    /// three times the file work for one file's worth of data.
    /// </summary>
    private static byte[]? FontBytes(string path)
    {
        if (string.Equals(s_loadedFontFile, path, StringComparison.OrdinalIgnoreCase)
            && s_loadedFontBytes is not null)
        {
            return s_loadedFontBytes;
        }

        if (!File.Exists(path))
        {
            s_faceProblem = $"{Path.GetFileName(path)} is no longer there.";
            Services.Log.Error("Font file missing, falling back to the interface face: {Path}", path);
            return null;
        }

        try
        {
            s_loadedFontBytes = File.ReadAllBytes(path);
            s_loadedFontFile = path;
            Services.Log.Information(
                "Loaded font {File} ({Bytes} bytes).", Path.GetFileName(path), s_loadedFontBytes.Length);
            return s_loadedFontBytes;
        }
        catch (Exception ex)
        {
            s_faceProblem = $"{Path.GetFileName(path)} could not be read.";
            Services.Log.Error(ex, "A font could not be read: {Path}", path);
            return null;
        }
    }

    /// <summary>Builds the window handles for the scale currently set in <see cref="Tokens"/>.</summary>
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

        DisposeHud();
    }

    private static void DisposeHud()
    {
        for (int i = 0; i < HudHandle.Length; i++)
        {
            HudHandle[i]?.Dispose();
            HudHandle[i] = null;
            HudSizePx[i] = 0f;
        }

        s_hudCount = 0;
    }
}
