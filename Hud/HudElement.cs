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

    /// <summary>
    /// Work that belongs on the game's tick rather than in a frame: something that has to keep
    /// running while nothing is being drawn, or that is too expensive to do sixty times a
    /// second. Most elements have none, and the default does nothing.
    /// <para>
    /// Whatever goes here stays short and throttles itself (CLAUDE.md §7.5). The tick is not
    /// a second draw path with a longer budget.
    /// </para>
    /// </summary>
    public virtual void Tick()
    {
    }

    // --- arranging -----------------------------------------------------------
    // Edit mode drags an element by its outside, and an element is the only thing that knows
    // where that is. It is stated here rather than worked out by the mode, because every
    // element has a different shape and only one of them is a block of eight.

    /// <summary>
    /// Whether this element can be dragged. False for anything positioned by something other
    /// than a point on the screen.
    /// </summary>
    public virtual bool Movable => false;

    /// <summary>
    /// The rectangle edit mode drags, in screen pixels. Only meaningful after
    /// <see cref="Draw"/> has run at least once, since it is the drawn extent.
    /// </summary>
    public virtual void Bounds(out System.Numerics.Vector2 min, out System.Numerics.Vector2 max)
    {
        min = default;
        max = default;
    }

    /// <summary>
    /// Puts the element's top-left corner here, in screen pixels. The element converts that
    /// into whatever it actually stores — which for the frames is an unscaled offset, so the
    /// same layout survives a change of interface scale.
    /// </summary>
    public virtual void MoveTo(System.Numerics.Vector2 topLeft)
    {
    }
}
