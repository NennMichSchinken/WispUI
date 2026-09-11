using System;
using System.Numerics;

namespace WispUI.Hud.PartyFrames;

/// <summary>Which way a block of frames runs.</summary>
internal enum FrameDirection
{
    /// <summary>Frames stack downwards; more lines means more columns. The game's own shape.</summary>
    Vertical = 0,

    /// <summary>Frames run to the right; more lines means more rows.</summary>
    Horizontal = 1,
}

/// <summary>
/// Where each frame goes. Nothing but arithmetic: no ImGui containers, no state, no drawing —
/// which is what lets a layout change take effect on the next frame without an id anywhere
/// being renumbered (spec §4).
/// <para>
/// Two controls decide everything: a direction and how many lines to break into. The
/// rows-and-columns matrix it replaces was the part of other plugins Florian singled out as
/// too complicated, and it says the same thing twice.
/// </para>
/// <para>
/// Written for any number of members, not for eight. That is the whole reason an alliance can
/// be switched on later without this file changing.
/// </para>
/// </summary>
internal static class FrameLayout
{
    /// <summary>The line counts on offer. A number that is not one of these reads as one line.</summary>
    public static readonly int[] LineChoices = { 1, 2, 4 };

    /// <summary>
    /// How many frames each line holds. The last line takes the remainder, so a party of five
    /// in two lines is three and two rather than four and one — the block stays square.
    /// </summary>
    public static int PerLine(int count, int lines)
    {
        lines = Math.Max(1, lines);
        return Math.Max(1, (count + lines - 1) / lines);
    }

    /// <summary>
    /// The offset of one frame from the top left of the block.
    /// </summary>
    /// <param name="index">Which frame, counted in the order they will be drawn.</param>
    /// <param name="count">How many frames there are in total — it decides how lines fill.</param>
    public static Vector2 Offset(
        int index,
        int count,
        FrameDirection direction,
        int lines,
        float width,
        float height,
        float spacing)
    {
        int perLine = PerLine(count, lines);
        int line = index / perLine;
        int slot = index - (line * perLine);

        float alongX = width + spacing;
        float alongY = height + spacing;

        return direction == FrameDirection.Horizontal
            ? new Vector2(slot * alongX, line * alongY)
            : new Vector2(line * alongX, slot * alongY);
    }
}
