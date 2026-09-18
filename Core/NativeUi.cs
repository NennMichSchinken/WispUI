using System;
using System.Numerics;
using Dalamud.Game.NativeWrapper;
using WispUI.Style;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;

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
    /// One member of the game's own party list, as far as the frames need to care.
    /// <para>
    /// Two numbers, and they are not the same number. <see cref="Row"/> is where the game
    /// draws this member — the order the player set up in their own settings, sorted by role
    /// or not. <see cref="HudIndex"/> is where they sit in the agent's own array, which always
    /// begins with the local player whatever the list looks like on screen.
    /// </para>
    /// </summary>
    internal struct HudPartyMemberInfo
    {
        public uint EntityId;

        /// <summary>Survives the member not being loaded, which an entity id does not.</summary>
        public ulong ContentId;

        /// <summary>Place in the agent's array. The number the context menu call asks for.</summary>
        public int HudIndex;

        /// <summary>Which row of the game's own party list this member is drawn on, from zero.</summary>
        public int Row;
    }

    /// <summary>As many members as the agent keeps room for. A full party plus trust slots.</summary>
    public const int HudPartyCapacity = 10;

    /// <summary>
    /// Reads the game's own party list order — who it shows, and in which row.
    /// <para>
    /// 🔴 This is why WispUI has no sorting option of its own. FFXIV already lets the player
    /// sort their party list by role and order the jobs inside each role
    /// (<c>PartyListSortTypeTank</c> and friends, plus the role sort window). Reading the
    /// finished order instead of the settings behind it means the frames follow every one of
    /// those choices without a single switch of ours, and can never disagree with the list
    /// they replace (Florian, 2026-09-13: "dann sparen wir uns die Option").
    /// </para>
    /// <para>
    /// 🔴 The agent's array is not the display order. Its own remark says the local player is
    /// always first in it and their real place is in <c>Index</c> — so the row is read from
    /// there, and the array position is kept only because that is what the context menu call
    /// wants.
    /// </para>
    /// </summary>
    /// <returns>How many entries of <paramref name="into"/> were filled. Zero means the game
    /// has nothing to say right now, and the caller should fall back to the party list's own
    /// order rather than draw nothing.</returns>
    public static unsafe int ReadPartyOrder(Span<HudPartyMemberInfo> into)
    {
        AgentHUD* hud = AgentHUD.Instance();
        if (hud is null)
        {
            return 0;
        }

        int count = hud->PartyMemberCount;
        Span<HudPartyMember> members = hud->PartyMembers;

        if (count > members.Length)
        {
            count = members.Length;
        }

        if (count > into.Length)
        {
            count = into.Length;
        }

        for (int i = 0; i < count; i++)
        {
            ref HudPartyMember member = ref members[i];

            into[i].EntityId = member.EntityId;
            into[i].ContentId = member.ContentId;
            into[i].HudIndex = i;
            into[i].Row = member.Index;
        }

        return count < 0 ? 0 : count;
    }

    /// <summary>One status effect on somebody, straight out of the game's own array.</summary>
    internal struct StatusEntry
    {
        public uint StatusId;

        /// <summary>Stacks for an effect that has them, strength for one that does not.</summary>
        public ushort Param;

        /// <summary>Seconds left, or zero for an effect that does not run out.</summary>
        public float Remaining;

        /// <summary>Who put it there. Zero when the game is not saying.</summary>
        public ulong SourceId;
    }

    /// <summary>As many effects as the game keeps room for on one person.</summary>
    public const int StatusCapacity = 60;

    /// <summary>
    /// Reads the effects on a party member, without allocating.
    /// <para>
    /// 🔴 This is why it is here rather than through Dalamud's own wrapper.
    /// <c>IPartyMember.Statuses</c> builds a new list object on every access, and its indexer
    /// hands back an interface, which boxes the struct behind it. Eight members with a dozen
    /// effects each, sixty times a second, is a few hundred objects a frame for data the game
    /// already has lying in a flat array — exactly the churn CLAUDE.md §7.1 exists to stop.
    /// </para>
    /// </summary>
    /// <param name="member">A party member's address, as Dalamud reports it.</param>
    public static unsafe int ReadMemberStatuses(nint member, Span<StatusEntry> into)
    {
        if (member == 0)
        {
            return 0;
        }

        var party = (FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember*)member;
        return ReadStatuses(&party->StatusManager, into);
    }

    /// <summary>
    /// The same, for a character in the world — which is where the effects on the player come
    /// from while they are alone and there is no party list to read.
    /// </summary>
    public static unsafe int ReadCharacterStatuses(nint character, Span<StatusEntry> into)
    {
        if (character == 0)
        {
            return 0;
        }

        // 🔴 Through the game's own accessor, NOT the StatusManager field on the struct. The
        // two do not point at the same thing: reading the field gave three effects where the
        // game had thirty (Florian, 2026-09-13). Dalamud asks the same way, which is what made
        // the disagreement visible at all.
        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)character;
        return ReadStatuses(chara->GetStatusManager(), into);
    }

    /// <summary>
    /// How much of a shield sits on a party member, as a percentage of their maximum health.
    /// <para>
    /// A single byte the game keeps beside their job and level, and the same number its own
    /// party list draws its shield segment from — so no arithmetic of ours can disagree with
    /// what the native list shows. Zero means no shield.
    /// </para>
    /// <para>
    /// ⚠️ It is a PERCENTAGE, not an amount of health. Multiplying it by maximum health to get
    /// a figure in hit points would be inventing precision the byte does not carry.
    /// </para>
    /// </summary>
    /// <param name="member">A party member's address, as Dalamud reports it.</param>
    public static unsafe byte MemberShield(nint member)
    {
        if (member == 0)
        {
            return 0;
        }

        var party = (FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember*)member;
        return party->DamageShield;
    }

    /// <summary>
    /// The same, for a character in the world — the solo case, where there is no party list to
    /// read and the player is still the one person a frame is about.
    /// <para>
    /// The game stores it in the same shape and the same place relative to job and level, so
    /// the two read alike and the renderer never has to know which one it got.
    /// </para>
    /// </summary>
    public static unsafe byte CharacterShield(nint character)
    {
        if (character == 0)
        {
            return 0;
        }

        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)character;
        return chara->ShieldValue;
    }

    /// <summary>How many raid marker slots the game keeps.</summary>
    public const int MarkerSlots = 17;

    /// <summary>
    /// Reads who currently carries each raid marker, into a reused span.
    /// <para>
    /// The game stores it the other way round from the way a frame asks: the array is indexed
    /// by MARKER, and each entry holds the object carrying it. So it is read once per frame and
    /// each frame then searches it, rather than every frame asking the game separately —
    /// seventeen comparisons per member is nothing, seventeen reads of game memory is not.
    /// </para>
    /// <para>
    /// Entries are object ids, which for a player is their entity id. Anybody not loaded has
    /// no entity id worth matching on, but they also cannot be carrying a marker we could see,
    /// so the two gaps line up and neither needs handling.
    /// </para>
    /// </summary>
    /// <returns>How many slots were filled in. Zero when the game has nothing to say.</returns>
    public static unsafe int ReadMarkers(Span<uint> into)
    {
        var controller = FFXIVClientStructs.FFXIV.Client.Game.UI.MarkingController.Instance();

        if (controller == null)
        {
            return 0;
        }

        int count = Math.Min(into.Length, MarkerSlots);
        Span<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId> markers = controller->Markers;

        for (int i = 0; i < count; i++)
        {
            uint id = markers[i].ObjectId;

            // 🔴 An empty slot is not zero. The game writes 0xE0000000 there — its own word for
            // "no object", used the same way all over the client — and a check for zero alone
            // reports all seventeen slots as taken. Worse than a wrong number: the diagnostic
            // then looks the id up in the object table, where it matches the first unloaded
            // thing it finds, and prints a confident name for a marker nobody placed. That is
            // exactly the shape of the bad diagnostic from session 9 (Florian, 2026-09-18,
            // whose seventeen empty slots all came back as the same minion).
            into[i] = id == NoObject ? 0u : id;
        }

        return count;
    }

    /// <summary>
    /// The client's own value for "no object", which is not zero. VERIFIED in the game
    /// structures, where it is the default for every optional object reference.
    /// </summary>
    private const uint NoObject = 0xE0000000u;

    private static unsafe int ReadStatuses(
        FFXIVClientStructs.FFXIV.Client.Game.StatusManager* manager,
        Span<StatusEntry> into)
    {
        if (manager is null)
        {
            return 0;
        }

        Span<FFXIVClientStructs.FFXIV.Client.Game.Status> statuses = manager->Status;

        // 🔴 The whole array, not the first NumValidStatuses of it. That count is not a dense
        // bound — the array holds gaps, so stopping at the count drops whatever sits past the
        // last gap. Sixty comparisons of a number is nothing; a silently short list is not.
        int count = 0;

        for (int i = 0; i < statuses.Length && count < into.Length; i++)
        {
            ref FFXIVClientStructs.FFXIV.Client.Game.Status status = ref statuses[i];

            // The array holds empty slots among the full ones — a cleared effect leaves its
            // place behind rather than shuffling the rest along.
            if (status.StatusId == 0)
            {
                continue;
            }

            into[count].StatusId = status.StatusId;
            into[count].Param = status.Param;

            // Negative on an effect that does not run out. Zero reads better everywhere else.
            into[count].Remaining = status.RemainingTime < 0f ? 0f : status.RemainingTime;
            into[count].SourceId = status.SourceObject;

            count++;
        }

        return count;
    }

    /// <summary>
    /// What job the game's own party list is drawing on one of its rows, or zero.
    /// <para>
    /// 🔴 The place a party member's job survives them not being loaded. Dalamud's party list
    /// reports job zero for anybody in another zone — but the game's own list still shows
    /// their icon, so the fact is there, just not where the obvious field is. It is on the
    /// addon, as the icon id it is about to draw (Florian, 2026-09-13: two members elsewhere
    /// drew grey while the native list beside them showed a White Mage and a Dark Knight).
    /// </para>
    /// <para>
    /// Job icons run in sets of a hundred from 62000, so the job is the icon's last two
    /// digits whichever set the list happens to be using.
    /// </para>
    /// </summary>
    /// <param name="row">The row the member is drawn on, counting from zero.</param>
    public static unsafe uint PartyListJob(int row)
    {
        if (row < 0)
        {
            return 0;
        }

        var addon = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Services.GameGui.GetAddonByName("_PartyList", 1).Address;

        if (addon is null)
        {
            return 0;
        }

        Span<uint> icons = addon->PartyClassJobIconId;

        if (row >= icons.Length)
        {
            return 0;
        }

        uint icon = icons[row];

        // Nothing drawn on that row, or an icon from somewhere else entirely.
        return icon < 62000u ? 0u : icon % 100u;
    }

    /// <summary>Whether the game's party list is hidden because we hid it.</summary>
    private static bool s_partyListHidden;

    /// <summary>
    /// Hides or restores the game's own party list, once per frame.
    /// <para>
    /// 🔴 Asked every frame rather than on a change of ours, because the game puts its list
    /// back up by itself — on a zone change, on joining a duty, whenever the interface is
    /// rebuilt. Only a difference is written, so the common case costs one read.
    /// </para>
    /// <para>
    /// While the player has never asked for this, the list is not touched at all. A plugin
    /// that sets a piece of the game's interface visible "just to be sure" is a plugin that
    /// undoes whatever the player did with it somewhere else.
    /// </para>
    /// </summary>
    public static unsafe void SettleNativePartyList(bool hide)
    {
        if (!hide && !s_partyListHidden)
        {
            return;
        }

        var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName("_PartyList", 1).Address;
        if (addon is null)
        {
            // Not built yet, or gone with the interface. Nothing to hide and nothing to put
            // back; the flag stays as it is so the next frame tries again.
            return;
        }

        if (addon->IsVisible != !hide)
        {
            addon->IsVisible = !hide;
        }

        s_partyListHidden = hide;
    }

    /// <summary>
    /// Puts the game's list back for good. Called when the plugin goes away: a piece of the
    /// player's interface must never be left hidden by something that is no longer running.
    /// </summary>
    public static void RestoreNativePartyList() => SettleNativePartyList(false);

    /// <summary>
    /// One of the game's own windows, as it stands on the screen right now.
    /// </summary>
    internal readonly struct NativeWindow
    {
        public readonly string Name;

        /// <summary>Which of the game's thirteen depth layers it was found in, counting from one.</summary>
        public readonly int Layer;

        /// <summary>
        /// Where it sits in that layer's own list, counting from zero — including the entries
        /// that were skipped for being invisible, so the number means a position in the game's
        /// list and not a position in ours.
        /// <para>
        /// This is the part that matters. MEASURED 2026-09-18: the layer does not separate a
        /// window from a HUD element at all — the character sheet, the chat log, every hotbar
        /// and <c>_PartyList</c> were all in layer five together. Whatever decides that the
        /// character sheet covers the party list is inside one layer, which leaves the order
        /// of the list itself.
        /// </para>
        /// </summary>
        public readonly int Slot;

        public readonly Vector2 Min;

        public readonly Vector2 Max;

        public NativeWindow(string name, int layer, int slot, Vector2 min, Vector2 max)
        {
            this.Name = name;
            this.Layer = layer;
            this.Slot = slot;
            this.Min = min;
            this.Max = max;
        }
    }

    /// <summary>
    /// Walks the game's depth layers and writes down every window that is on screen, with the
    /// layer it lives in and the box it covers.
    /// <para>
    /// The game keeps its interface in thirteen numbered lists and draws them in order, which
    /// is the only statement anywhere about what sits over what. Reading it is the difference
    /// between "our frames go under the inventory" and a hand-written list of addon names that
    /// is wrong the first time somebody opens a window we did not think of.
    /// </para>
    /// <para>
    /// ⚠️ NOT a draw-path call. It walks up to thirteen lists of up to 256 entries and builds
    /// strings; it exists for <c>/wisp status</c>, so the layer question can be settled with
    /// numbers off a real screen rather than by argument.
    /// </para>
    /// </summary>
    /// <returns>How many were written, which may be fewer than found if the buffer runs out.</returns>
    public static unsafe int ReadNativeWindows(Span<NativeWindow> into)
    {
        var manager = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance();

        if (manager == null)
        {
            return 0;
        }

        int written = 0;

        // The thirteen lists are separate fields rather than an array, so they are visited by
        // name. Tedious and honest: an array cast over them would be one wrong offset away
        // from reading whatever follows.
        for (int layer = 1; layer <= 13 && written < into.Length; layer++)
        {
            AtkUnitList* list = layer switch
            {
                1 => &manager->DepthLayerOneList,
                2 => &manager->DepthLayerTwoList,
                3 => &manager->DepthLayerThreeList,
                4 => &manager->DepthLayerFourList,
                5 => &manager->DepthLayerFiveList,
                6 => &manager->DepthLayerSixList,
                7 => &manager->DepthLayerSevenList,
                8 => &manager->DepthLayerEightList,
                9 => &manager->DepthLayerNineList,
                10 => &manager->DepthLayerTenList,
                11 => &manager->DepthLayerElevenList,
                12 => &manager->DepthLayerTwelveList,
                _ => &manager->DepthLayerThirteenList,
            };

            Span<Pointer<AtkUnitBase>> entries = list->Entries;
            int count = Math.Min((int)list->Count, entries.Length);

            for (int i = 0; i < count && written < into.Length; i++)
            {
                AtkUnitBase* unit = entries[i].Value;

                if (unit is null || !unit->IsVisible || unit->RootNode is null)
                {
                    continue;
                }

                // The window's own box, which is not the root node's: a window with a collision
                // node reports that instead, and that is the part the player can actually hit.
                FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
                unit->GetWindowBounds(&bounds);

                float width = bounds.Width;
                float height = bounds.Height;

                // Built but empty, or collapsed to nothing. It covers no pixels either way.
                if (width <= 1f || height <= 1f)
                {
                    continue;
                }

                into[written++] = new NativeWindow(
                    unit->NameString,
                    layer,
                    i,
                    new Vector2(bounds.Pos1.X, bounds.Pos1.Y),
                    new Vector2(bounds.Pos2.X, bounds.Pos2.Y));
            }
        }

        return written;
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
    /// <param name="hudIndex">
    /// The member's place in the HUD agent's own party array, counting from zero — the
    /// <see cref="HudPartyMemberInfo.HudIndex"/> carried through the snapshot, not the row the
    /// member is drawn on and not their place in the party list Dalamud hands us. Those three
    /// agree only while nothing is sorted, which is exactly the case that hid this apart until
    /// now.
    /// </param>
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
