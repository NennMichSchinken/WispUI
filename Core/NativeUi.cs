using System;
using System.Numerics;
using Dalamud.Game.NativeWrapper;
using WispUI.Style;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WispUI.Core;

/// <summary>
/// Everything WispUI says to the game's own interface, in one place.
/// <para>
/// This is the only file in the suite that reaches into the game's structures, and it stays
/// that way on purpose: when a patch moves one of them, the fix is here and nowhere else
/// (CLAUDE.md §7.7). Every call checks for nothing rather than trusting a pointer, because a
/// structure that has moved gives a null, not an exception.
/// </para>
/// </summary>
internal static class NativeUi
{
    /// <summary>
    /// Whether anything of ours wants the game to keep drawing its own pointer this frame,
    /// and what we last told Dalamud.
    /// <para>
    /// One switch, shared by everything running in the game, so it gets exactly one writer.
    /// The settings window and the party frames both sit under the mouse at times, and two
    /// of them writing it directly is how a pointer gets left the way the last one wanted it.
    /// </para>
    /// </summary>
    private static bool s_gameCursorWanted;
    private static bool s_gameCursorHeld;

    /// <summary>
    /// Says, while drawing, that the pointer over this thing should stay the game's own.
    /// Anything the mouse can be over says it; the frame settles it once at the end.
    /// </summary>
    public static void KeepGameCursor() => s_gameCursorWanted = true;

    /// <summary>
    /// Called once at the end of the frame, after everything has had its say. Writes only on
    /// a change, so the common case costs a comparison.
    /// </summary>
    public static void SettleCursor()
    {
        bool ours = s_gameCursorWanted;
        s_gameCursorWanted = false;

        if (ours != s_gameCursorHeld)
        {
            s_gameCursorHeld = ours;

            // Off while it is ours: with it on, Dalamud holds the game's pointer back and puts
            // a Windows one in its place, and the shape we set would never reach the screen.
            Services.PluginInterface.UiBuilder.OverrideGameCursor = !ours;
        }

        if (ours)
        {
            // The shape comes last, after every window and every HUD element has had its say,
            // so it is what the thing under the mouse asked for on this frame rather than on
            // the one before. The game picks its own shape earlier, which is what makes this
            // the later word.
            FollowCursor(ImGui.GetMouseCursor());
        }
    }

    /// <summary>
    /// Hands the pointer back for good. Called when the plugin goes away: the switch is shared
    /// by everything in the game and must never be left lying the way we wanted it.
    /// </summary>
    public static void ReleaseCursor()
    {
        s_gameCursorWanted = false;

        if (s_gameCursorHeld)
        {
            s_gameCursorHeld = false;
            Services.PluginInterface.UiBuilder.OverrideGameCursor = true;
        }
    }

    /// <summary>
    /// Sets the game's own pointer to match what ImGui asked for this frame.
    /// <para>
    /// The game keeps a cursor with a set of shapes — an arrow, a hand for something
    /// clickable, a bar for text — and picks one each frame from whatever is under the mouse.
    /// Rather than paint a pointer of our own beside it, we set its shape: the pointer over a
    /// WispUI button is then the same pointer the player sees everywhere else in the game,
    /// which is the whole reason to do it this way (Florian, 2026-09-12).
    /// </para>
    /// <para>
    /// Called after everything has drawn, so it carries what the control under the mouse asked
    /// for rather than what the frame before wanted. The game chooses its own shape earlier in
    /// the frame, which is what makes this the later word.
    /// </para>
    /// </summary>
    public static unsafe void FollowCursor(ImGuiMouseCursor cursor)
    {
        AtkStage* stage = AtkStage.Instance();
        if (stage is null)
        {
            return;
        }

        stage->AtkCursor.SetCursorType(Shape(cursor), true);
    }

    /// <summary>
    /// Opens the game's own right-click menu on a party member — the one with Examine, Trade,
    /// Send Tell and the rest.
    /// <para>
    /// Not a menu of ours that looks like the game's: it is the game's, opened by the same call
    /// its own party list makes, so it carries exactly the entries that member deserves right
    /// now and whatever other plugins have added to it. Nothing to keep current.
    /// </para>
    /// <para>
    /// It has to exist because the frames take every mouse button (spec §15), and without this
    /// the right button over a frame would do nothing at all — while over the game's own party
    /// list it opens this. The frames are meant to replace that list, so they owe it the menu.
    /// </para>
    /// </summary>
    /// <param name="hudIndex">The member's place in the game's own party list, counting from zero.</param>
    public static unsafe void OpenPartyContextMenu(int hudIndex)
    {
        if (hudIndex < 0)
        {
            return;
        }

        AgentHUD* hud = AgentHUD.Instance();
        if (hud is null)
        {
            return;
        }

        // 🔴 Opened in the name of the game's own party list, not from the target.
        //
        // Both calls put up the same menu, and the obvious one — from the target — puts it
        // under the cursor. The cursor is on one of our frames, and ImGui draws after the
        // entire game interface, so the menu opens underneath the frame it belongs to. That
        // order cannot be reversed: nothing of ours can go behind a game window.
        //
        // It is the party list's menu either way, which is what makes it carry the entries for
        // a party member rather than for a stranger.
        //
        // Where it comes up is not decided here and no longer needs to be. Three attempts went
        // into moving it somewhere the frames are not — asking for a position (ignored on this
        // route), placing it beside the block (works, and is not where the pointer is), hiding
        // the frames (works, and removes what is being read). The frames now leave a hole
        // where it lands instead, so it can open at the pointer like the game's own does.
        ushort addonId = Services.GameGui.GetAddonByName("_PartyList", 1).Id;

        if (addonId == 0)
        {
            return;
        }

        hud->OpenContextMenuFromPartyAddon(addonId, hudIndex);
    }

    /// <summary>
    /// How far inside the menu's own window the hole is cut, per side.
    /// <para>
    /// 🔴 Inside, never flush. A game window is larger than the panel you can see — there is
    /// border and shadow in its node tree that it does not paint over. Cutting the hole to the
    /// window's full size therefore left a gap all the way round the menu, which read as a
    /// frame drawn around it (Florian, 2026-09-12).
    /// </para>
    /// <para>
    /// Erring inwards is the safe direction: too small a hole leaves a sliver of frame along
    /// the menu's edge, too large a one leaves a visible hole in the world. The first is hard
    /// to notice, the second is what was reported.
    /// </para>
    /// </summary>
    private const float MenuInset = 6f;

    /// <summary>
    /// The rectangle the game's own right-click menu actually covers, or false when none is
    /// open. Used to leave that area unpainted rather than to move anything.
    /// </summary>
    public static bool ContextMenuBounds(out Vector2 min, out Vector2 max)
    {
        min = default;
        max = default;

        AtkUnitBasePtr addon = Services.GameGui.GetAddonByName("ContextMenu", 1);

        if (!addon.IsVisible)
        {
            return false;
        }

        Vector2 size = addon.ScaledSize;
        float inset = Tokens.Px(MenuInset);

        min = addon.Position + new Vector2(inset, inset);
        max = addon.Position + size - new Vector2(inset, inset);

        // A menu that has been put up but not laid out yet has no size worth cutting around.
        return max.X > min.X && max.Y > min.Y;
    }

    /// <summary>
    /// Whether the game's own right-click menu is on screen.
    /// <para>
    /// 🔴 Asked so the frames can let go of the mouse while it is up. The frames hold every
    /// mouse button over themselves (spec §15), and a menu opened on a frame appears partly
    /// over that same area — so without this the menu is there and cannot be clicked, and the
    /// click lands on a frame instead (Florian, 2026-09-12).
    /// </para>
    /// </summary>
    public static bool ContextMenuOpen() =>
        Services.GameGui.GetAddonByName("ContextMenu", 1).IsVisible;

    // Objects in the world still light up behind the window — a postbox under the pointer is
    // highlighted even though the pointer is really on a settings row.
    //
    // Tried and dropped: UIInputData.FilterUICursorInputs, the call the game makes on itself
    // wherever its own panels cover the view. In-game it changed nothing here (2026-09-12), so
    // it went rather than staying on as an unproven poke into the game's memory every tick.
    // Clicks are held back by Dalamud already, which leaves a highlight and nothing more.
    // Whatever paints it is not that input path, and finding out means going after the
    // targeting system — too much to reach for over a highlight Florian called liveable.

    private static AtkCursor.CursorType Shape(ImGuiMouseCursor cursor) => cursor switch
    {
        // The game's own word for the pointing hand it shows over anything you can click.
        ImGuiMouseCursor.Hand => AtkCursor.CursorType.Clickable,
        ImGuiMouseCursor.TextInput => AtkCursor.CursorType.TextInput,
        ImGuiMouseCursor.ResizeEw => AtkCursor.CursorType.ResizeWE,
        ImGuiMouseCursor.ResizeNs => AtkCursor.CursorType.ResizeNS,
        ImGuiMouseCursor.ResizeNesw => AtkCursor.CursorType.ResizeNESW,
        ImGuiMouseCursor.ResizeNwse => AtkCursor.CursorType.ResizeNWSE,
        ImGuiMouseCursor.ResizeAll => AtkCursor.CursorType.Grab,
        ImGuiMouseCursor.NotAllowed => AtkCursor.CursorType.NoAccess,
        _ => AtkCursor.CursorType.Arrow,
    };
}
