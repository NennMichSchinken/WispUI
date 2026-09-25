using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using WispUI.Core;

namespace WispUI.Data;

/// <summary>
/// Game icons by id, for everything WispUI paints over the world: job icons today, role
/// icons and raid markers later.
/// <para>
/// Dalamud owns the textures and caches them itself, so this is not a second cache of the
/// pixels — it is a cache of the <em>lookup</em>. Resolving an id walks a path and a
/// dictionary inside Dalamud, and the answer never changes, so it is asked once per id and
/// the shared texture is kept.
/// </para>
/// <para>
/// An id the game has no icon for is remembered as a miss, so a wrong id costs one failed
/// lookup rather than one per frame for as long as the plugin runs.
/// </para>
/// </summary>
internal static class Icons
{
    /// <summary>
    /// The party leader's mark.
    /// <para>
    /// Unlike a job icon there is no rule to derive this from — the game's own party list
    /// draws the mark from a node of its own (<c>AddonPartyList.LeaderMarkResNode</c>), so it
    /// is not an id read off a sheet. CONFIRMED IN THE GAME against the flag the party list
    /// shows (Florian, 2026-09-12).
    /// </para>
    /// </summary>
    public const uint PartyLeader = 61521u;

    /// <summary>
    /// How far the art of a status icon sits inside its texture, in the texture's own pixels:
    /// four in from each side, fourteen down from the top, twelve up from the bottom.
    /// <para>
    /// 🔴 A status icon is not edge to edge. The texture carries a transparent margin and the
    /// game's own plate around the art, and drawing the whole thing gives a small picture
    /// floating in a lot of nothing.
    /// </para>
    /// <para>
    /// 🔴 These are PIXELS, and they are divided by the size of the texture that actually came
    /// back — not by a size assumed here. A first attempt wrote them as fractions of a forty by
    /// fifty-six texture, and against the real one that cut a sliver out of the middle of every
    /// icon (Florian, 2026-09-13, with a screenshot of the wreckage). The same lesson as the
    /// font sizes in session 3: a measurement is not a ratio until you know what it is a ratio
    /// of.
    /// </para>
    /// </summary>
    private const float StatusInsetX = 4f;
    private const float StatusInsetTop = 14f;
    private const float StatusInsetBottom = 12f;

    /// <summary>
    /// One of our own pictures, shipped beside the assembly under <c>Textures\</c>.
    /// <para>
    /// Same shape as the game-icon lookup below and for the same reason: Dalamud owns the
    /// pixels and caches them, so what is kept here is the <em>lookup</em>, which never
    /// changes. A file that is missing is remembered as a miss, so a typo in a name costs one
    /// failed lookup rather than one per frame for the life of the plugin.
    /// </para>
    /// </summary>
    /// <param name="path">Relative to the plugin folder, e.g. <c>bars/gradient.png</c>.</param>
    public static bool Bundled(string path, out ImTextureID handle) =>
        Bundled(path, out handle, out _);

    /// <summary>
    /// The same, and the size of the picture in its own pixels.
    /// <para>
    /// 🔴 Asked rather than assumed. A pattern that tiles needs to know how many of its own
    /// pixels it covers, and writing that number down here as a constant is the same trap the
    /// status-icon crop fell into in session 9 — a measurement is not a ratio until you know
    /// what it is a ratio of, and the file can be re-exported at another size without anyone
    /// remembering to come back and edit a number.
    /// </para>
    /// </summary>
    public static bool Bundled(string path, out ImTextureID handle, out System.Numerics.Vector2 size)
    {
        handle = default;
        size = System.Numerics.Vector2.One;

        if (!Ours.TryGetValue(path, out ISharedImmediateTexture? texture))
        {
            string? dir = Services.PluginInterface.AssemblyLocation.DirectoryName;

            // No folder to look in is not an error worth throwing over — it is a plugin loaded
            // in a way we did not expect, and a bar without its texture still draws.
            texture = dir is null
                ? null
                : Services.Textures.GetFromFile(
                    System.IO.Path.Combine(dir, "Textures", path.Replace('/', System.IO.Path.DirectorySeparatorChar)));

            Ours[path] = texture;
            Hold(texture);
        }

        if (texture is null || !texture.TryGetWrap(out IDalamudTextureWrap? wrap, out _))
        {
            return false;
        }

        handle = wrap.Handle;

        // Guarded against zero, because the size ends up as a divisor.
        size = new System.Numerics.Vector2(
            wrap.Width > 0 ? wrap.Width : 1f,
            wrap.Height > 0 ? wrap.Height : 1f);

        return true;
    }

    /// <summary>
    /// A status effect's picture, cropped to the art. False when there is nothing to draw yet.
    /// </summary>
    public static bool StatusIcon(
        uint iconId,
        out ImTextureID handle,
        out System.Numerics.Vector2 uv0,
        out System.Numerics.Vector2 uv1)
    {
        handle = default;
        uv0 = System.Numerics.Vector2.Zero;
        uv1 = System.Numerics.Vector2.One;

        if (iconId == 0)
        {
            return false;
        }

        if (!Sheets.TryGetValue(iconId, out ISharedImmediateTexture? sheet))
        {
            sheet = Services.Textures.TryGetFromGameIcon(iconId, out ISharedImmediateTexture? found) ? found : null;
            Sheets[iconId] = sheet;
            Hold(sheet);
        }

        if (sheet is null || !sheet.TryGetWrap(out IDalamudTextureWrap? wrap, out _))
        {
            return false;
        }

        handle = wrap.Handle;

        float w = wrap.Width;
        float h = wrap.Height;

        // A texture too small to hold the margin is drawn whole rather than cut into nothing.
        if (w <= StatusInsetX * 2f || h <= StatusInsetTop + StatusInsetBottom)
        {
            return true;
        }

        uv0 = new System.Numerics.Vector2(StatusInsetX / w, StatusInsetTop / h);
        uv1 = new System.Numerics.Vector2(1f - (StatusInsetX / w), 1f - (StatusInsetBottom / h));

        return true;
    }

    /// <summary>Resolved lookups by icon id. A null value is an id the game does not have.</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture?> Sheets = new();

    /// <summary>The same, for our own files. Keyed by the path given to <see cref="Bundled"/>.</summary>
    private static readonly Dictionary<string, ISharedImmediateTexture?> Ours = new();

    /// <summary>
    /// A hold on every texture WispUI has drawn, for as long as the plugin runs.
    /// <para>
    /// 🔴 Dalamud frees a shared texture on the graphics card once nobody has asked for it for
    /// two seconds (<c>SharedImmediateTexture.SelfReferenceDurationTicks</c>), and loads it again
    /// on the next request. With the party frames switched off, the preview band is the only
    /// thing asking — so leaving the Party frames screen for a moment and coming back freed and
    /// re-created every bar texture and every stand-in's icons. Exactly that sequence crashed the
    /// game four times inside the graphics driver, always with the driver's photo mode hooked
    /// into the frame (Florian, 2026-09-25). A driver should survive a texture being freed and
    /// made again, but there is no reason to keep asking it to: the set is small (our bar
    /// textures, job icons, whatever effects have been on screen) and a held texture costs a
    /// few kilobytes.
    /// </para>
    /// <para>
    /// Capped, because effect icons are the one open-ended part — a long session could see
    /// hundreds. Past the cap a texture is simply not held, which is how everything behaved
    /// before.
    /// </para>
    /// </summary>
    private static readonly List<Task<IDalamudTextureWrap>> Held = new();

    private const int MaxHeld = 512;

    /// <summary>Takes a hold on a texture the first time it is looked up. Never per frame.</summary>
    private static void Hold(ISharedImmediateTexture? texture)
    {
        if (texture is null || Held.Count >= MaxHeld)
        {
            return;
        }

        Held.Add(texture.RentAsync());
    }

    /// <summary>Lets go of every held texture. Called once, when the plugin unloads.</summary>
    public static void Release()
    {
        for (int i = 0; i < Held.Count; i++)
        {
            // Disposes the wrap whenever the load finishes, or swallows the failure if it never
            // does — a texture still loading at unload must not throw on the way out.
            _ = Held[i].ToContentDisposedTask(true);
        }

        Held.Clear();
        Sheets.Clear();
        Ours.Clear();
    }

    /// <summary>
    /// The texture to draw for an icon id, or a null handle while it is not available — not
    /// loaded yet, or no such icon. The caller draws nothing in that case rather than drawing
    /// Dalamud's empty texture, which would be a grey square where an icon is meant to be.
    /// </summary>
    public static ImTextureID Handle(uint iconId)
    {
        if (iconId == 0)
        {
            return default;
        }

        if (!Sheets.TryGetValue(iconId, out ISharedImmediateTexture? sheet))
        {
            sheet = Services.Textures.TryGetFromGameIcon(iconId, out ISharedImmediateTexture? found) ? found : null;
            Sheets[iconId] = sheet;
            Hold(sheet);
        }

        if (sheet is null)
        {
            return default;
        }

        // Asked every time rather than kept: the wrap is only promised for the frame it was
        // asked in, and this is what tells Dalamud the texture is still in use. The instance
        // behind it is the same one every frame, so nothing is allocated here.
        return sheet.TryGetWrap(out IDalamudTextureWrap? wrap, out _) ? wrap.Handle : default;
    }
}
