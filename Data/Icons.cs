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
    /// The part of a status effect's picture that is actually the picture.
    /// <para>
    /// 🔴 A status icon is not edge to edge. The texture carries a transparent margin and the
    /// game's own rounded plate around the art, and drawing the whole thing gives a small
    /// picture floating in a lot of nothing — which is why ours looked like game defaults
    /// dropped on a frame rather than part of it (Florian, 2026-09-13).
    /// </para>
    /// <para>
    /// Stated as fractions rather than pixels so it holds for the high resolution textures
    /// too, which are the same layout at twice the size. Measured against the game's own
    /// numbers: four pixels in from each side, fourteen from the top and twelve from the
    /// bottom of a forty by fifty-six texture.
    /// </para>
    /// </summary>
    public static readonly System.Numerics.Vector2 StatusUv0 = new(4f / 40f, 14f / 56f);

    public static readonly System.Numerics.Vector2 StatusUv1 = new(1f - (4f / 40f), 1f - (12f / 56f));

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
