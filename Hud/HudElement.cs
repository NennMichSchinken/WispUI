using Dalamud.Bindings.ImGui;

namespace WispUI.Hud;

/// <summary>
/// One thing WispUI draws in the world. Everything a HUD element has in common lives here:
/// whether it is on, what it is called, and the two calls the manager makes on it.
/// <para>
/// Deliberately thin. An element gets its configuration handed to it rather than reaching for
/// it, and it paints into a draw list at absolute coordinates — no ImGui window, no nesting,
/// so a layout change can never reshuffle an ImGui id (spec §4).
/// </para>
/// </summary>
internal abstract class HudElement
{
    /// <summary>The name this element goes by in the log and in the navigation tree.</summary>
    public abstract string Name { get; }

    /// <summary>Whether the user has this element switched on.</summary>
    public abstract bool Enabled { get; }

    /// <summary>
    /// Gathers what this element needs for the frame about to be drawn. Runs once, before
    /// <see cref="Draw"/>, and is the only place allowed to read from the game.
    /// </summary>
    public abstract void Collect();

    /// <summary>
    /// Paints the element. Reads nothing but what <see cref="Collect"/> put aside, allocates
    /// nothing, and throws nothing — this runs a hundred times a second (CLAUDE.md §7).
    /// </summary>
    public abstract void Draw(ImDrawListPtr dl);

    /// <summary>
    /// Whether there is anything to draw at all. Checked before <see cref="Collect"/>, so an
    /// element that has nothing to show this frame costs one branch and not a party scan.
    /// </summary>
    public virtual bool HasAnythingToDraw => true;
}
