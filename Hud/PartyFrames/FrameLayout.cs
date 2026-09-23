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

/// <summary>How mana is shown on a frame that shows it at all.</summary>
internal enum ManaStyle
{
    /// <summary>
    /// A thin strip along the bottom edge — the default (Florian, 2026-09-10). Mana is a
    /// second-order reading: present at a glance, never competing with the health bar.
    /// </summary>
    Strip = 0,

    /// <summary>A bar of its own, with its own dark track behind it, under the health bar.</summary>
    Bar = 1,
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
        float spacingX,
        float spacingY)
    {
        int perLine = PerLine(count, lines);
        int line = index / perLine;
        int slot = index - (line * perLine);

        float alongX = width + spacingX;
        float alongY = height + spacingY;

        return direction == FrameDirection.Horizontal
            ? new Vector2(slot * alongX, line * alongY)
            : new Vector2(line * alongX, slot * alongY);
    }

    /// <summary>
    /// How much room the whole block takes, corner to corner.
    /// <para>
    /// Worked out from the same two numbers <see cref="Offset"/> uses rather than by walking
    /// the frames, so the size and the positions cannot drift apart. The gaps are between the
    /// frames only: a block does not carry a margin of its own.
    /// </para>
    /// </summary>
    public static Vector2 BlockSize(
        int count,
        FrameDirection direction,
        int lines,
        float width,
        float height,
        float spacingX,
        float spacingY)
    {
        count = Math.Max(1, count);
        int perLine = PerLine(count, lines);

        // The lines actually used, which is not the same as the lines asked for: four lines
        // hold two frames when there are only two of them.
        int used = (count + perLine - 1) / perLine;

        // Frames across the screen and frames down it, whichever way the block runs. The
        // gaps belong to the screen's axes, not to the direction (see the config).
        int acrossScreen = direction == FrameDirection.Horizontal ? perLine : used;
        int downScreen = direction == FrameDirection.Horizontal ? used : perLine;

        return new Vector2(
            (acrossScreen * width) + ((acrossScreen - 1) * spacingX),
            (downScreen * height) + ((downScreen - 1) * spacingY));
    }
}
