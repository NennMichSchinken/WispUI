using Dalamud.Bindings.ImGui;
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
