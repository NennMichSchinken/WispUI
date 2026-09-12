using System;
using System.Numerics;
using WispUI.Style;

namespace WispUI.Core;

/// <summary>
/// Whether the user is arranging their HUD rather than playing, and the state of the drag
/// while they are.
/// <para>
/// Deliberately not saved. Edit mode is a thing you are doing right now, not a preference: a
/// plugin that came back from a restart still showing placeholder frames would be a bug, not
/// a convenience.
/// </para>
/// <para>
/// 🔴 The drag physics are LumenUI's, which were arrived at there over a long time and are
/// worth inheriting whole (Florian, 2026-09-12, asked for the same behaviour). The rule that
/// matters most: <b>never magnet-snap</b>. A position is only ever HELD at a line or LIMITED
/// by an edge — never pulled toward one. The difference is that a held position releases the
/// moment the user's own movement carries past it, so nothing ever fights the cursor.
/// </para>
/// </summary>
internal static class EditMode
{
    /// <summary>
    /// How close to a line the drag has to come before that line holds it, in pixels.
    /// Florian's own tuned value from the Lumen mockup.
    /// </summary>
    private const float GrooveTolerance = 4f;

    /// <summary>What an arrow key moves a selected element by, and what Shift makes of it.</summary>
    private const float NudgeStep = 1f;
    private const float NudgeStepFast = 10f;

    /// <summary>Where the pointer was when the drag started, and where the element was.</summary>
    private static Vector2 s_grabbedAt;
    private static Vector2 s_grabbedFrom;

    public static bool IsActive { get; private set; }

    /// <summary>Whether something is being dragged right now, as opposed to merely arranged.</summary>
    public static bool IsDragging { get; private set; }

    /// <summary>
    /// The alignment line the drag is currently held at, or null on each axis. Read by the
    /// overlay so it can draw the guide that explains why the element has stopped moving.
    /// </summary>
    public static float? HeldX { get; private set; }

    public static float? HeldY { get; private set; }

    /// <summary>
    /// Raised when arranging ends, however it ended — the button or the escape key.
    /// <para>
    /// The settings window listens, so leaving edit mode puts the player back where they
    /// started from rather than on an empty screen. Going in closed that window; coming out
    /// owes them it back (Florian, 2026-09-12).
    /// </para>
    /// </summary>
    public static event Action? Finished;

    public static void Toggle() => Set(!IsActive);

    /// <summary>Left on its own when the window closes — arranging outlives looking at the settings.</summary>
    public static void Stop() => Set(false);

    private static void Set(bool active)
    {
        if (active == IsActive)
        {
            return;
        }

        IsActive = active;

        if (!active)
        {
            EndDrag();
            Finished?.Invoke();
        }
    }

    /// <summary>Remembers where a drag began, so the element moves with the pointer rather than to it.</summary>
    public static void BeginDrag(Vector2 pointer, Vector2 elementTopLeft)
    {
        IsDragging = true;
        s_grabbedAt = pointer;
        s_grabbedFrom = elementTopLeft;
    }

    public static void EndDrag()
    {
        IsDragging = false;
        HeldX = null;
        HeldY = null;
    }

    /// <summary>
    /// Where a dragged element should sit for the pointer being here.
    /// <para>
    /// The wanted position always follows the pointer exactly — that is what keeps the element
    /// under the hand. Only the reported position is held at a line, and only while the wanted
    /// one is within tolerance of it. Push a few pixels further and it is simply free again,
    /// with no rubber band to fight.
    /// </para>
    /// </summary>
    /// <param name="pointer">Where the pointer is now.</param>
    /// <param name="size">How large the element is, for centring it against a line.</param>
    /// <param name="screen">The screen, whose centre lines are the only grooves there are yet.</param>
    /// <param name="free">Ctrl is down: no holding at all, place it exactly where the hand is.</param>
    public static Vector2 DragTo(Vector2 pointer, Vector2 size, Vector2 screen, bool free)
    {
        Vector2 wanted = s_grabbedFrom + (pointer - s_grabbedAt);

        if (free)
        {
            HeldX = null;
            HeldY = null;
            return wanted;
        }

        float centreX = MathF.Round(screen.X * 0.5f);
        float centreY = MathF.Round(screen.Y * 0.5f);
        float tolerance = Tokens.Px(GrooveTolerance);

        // The element's own centre is what lines up with the screen's, not its corner.
        float wantedCentreX = wanted.X + (size.X * 0.5f);
        float wantedCentreY = wanted.Y + (size.Y * 0.5f);

        HeldX = MathF.Abs(wantedCentreX - centreX) <= tolerance ? centreX : null;
        HeldY = MathF.Abs(wantedCentreY - centreY) <= tolerance ? centreY : null;

        return new Vector2(
            HeldX is float x ? MathF.Round(x - (size.X * 0.5f)) : wanted.X,
            HeldY is float y ? MathF.Round(y - (size.Y * 0.5f)) : wanted.Y);
    }

    /// <summary>How far an arrow key moves things, given whether Shift is down.</summary>
    public static float Nudge(bool fast) => fast ? NudgeStepFast : NudgeStep;
}
