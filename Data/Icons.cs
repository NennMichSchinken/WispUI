using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
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

        if (!s_loggedStatusSize)
        {
            // Once, and only so the crop above stops being a thing anybody has to guess at.
            // The insets are pixels off a texture whose size was assumed wrong the first time;
            // this says what it actually is.
            s_loggedStatusSize = true;
            Services.Log.Information(
                "Status icon {Id} texture is {Width}x{Height}; art kept from {X0},{Y0} to {X1},{Y1}.",
                iconId,
                wrap.Width,
                wrap.Height,
                uv0.X * w,
                uv0.Y * h,
                uv1.X * w,
                uv1.Y * h);
        }

        return true;
    }

    /// <summary>Whether the one-time note about the status texture size has been written.</summary>
    private static bool s_loggedStatusSize;

    /// <summary>Resolved lookups by icon id. A null value is an id the game does not have.</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture?> Sheets = new();

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
