namespace WispUI.Core;

/// <summary>
/// Whether the user is arranging their HUD rather than playing.
/// <para>
/// Deliberately not saved. Edit mode is a thing you are doing right now, not a preference: a
/// plugin that came back from a restart still showing placeholder frames would be a bug, not
/// a convenience.
/// </para>
/// <para>
/// It lives in one place because everything it affects has to agree on it — today that is the
/// placeholder party the frames draw without a group, tomorrow the dragging and the edges
/// that go with it (spec §6).
/// </para>
/// </summary>
internal static class EditMode
{
    public static bool IsActive { get; private set; }

    public static void Toggle() => IsActive = !IsActive;

    /// <summary>Left on its own when the window closes — arranging outlives looking at the settings.</summary>
    public static void Stop() => IsActive = false;
}
