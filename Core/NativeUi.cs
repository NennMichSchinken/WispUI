using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    /// Takes the mouse position away from the world for this tick, so nothing behind a WispUI
    /// window lights up under a pointer that is not really on it.
    /// <para>
    /// The game does this to itself wherever its own interface covers the world — there is a
    /// call for exactly this case — so a plugin window is only asking to be treated like any
    /// other panel. Clicks were already being held back by Dalamud; this is the highlight the
    /// pointer left behind on the way past (Florian, 2026-09-12).
    /// </para>
    /// <para>
    /// Runs on the game's tick rather than while drawing: by the time anything is painted the
    /// world has long since decided what it thinks the mouse is on.
    /// </para>
    /// </summary>
    public static unsafe void TakeMouseFromWorld()
    {
        UIInputData* input = UIInputData.Instance();
        if (input is null)
        {
            return;
        }

        // No buttons named: the position and the wheel are what the world must not see. What
        // the mouse buttons do is Dalamud's to decide, and it already decided.
        input->FilterUICursorInputs(MouseButtonFlags.None);
    }

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
