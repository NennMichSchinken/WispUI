using System;

namespace WispUI.Core;

/// <summary>
/// The modifier keys a binding can ask for, as a set.
/// <para>
/// Flags rather than a list of combinations: three keys make eight states, and writing those
/// out as an enum is eight names nobody wants to read.
/// </para>
/// </summary>
[Flags]
public enum BindingModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>
/// What a binding does when it fires. Two of them are not actions at all, which is why this
/// exists rather than treating action id zero as a special case.
/// </summary>
public enum BindingKind
{
    /// <summary>Select the member, the way a click on the game's party list does.</summary>
    Target = 0,

    /// <summary>Open the game's own right-click menu on them.</summary>
    ContextMenu = 1,

    /// <summary>Use an action on them.</summary>
    Action = 2,
}

/// <summary>
/// One mouse button, with modifiers, doing one thing on the frame under the pointer.
/// <para>
/// Kept per job, because what a button should do depends entirely on what you are playing —
/// a White Mage's second button is a heal and a Warrior has nothing to put there (Florian,
/// 2026-09-12). The two that are not actions are the exception and are shared, since
/// selecting somebody means the same thing on every job.
/// </para>
/// </summary>
[Serializable]
public sealed class MouseBinding
{
    /// <summary>
    /// Which button, as ImGui counts them: 0 left, 1 right, 2 middle, 3 and 4 the two side
    /// buttons. Dalamud hands the side buttons through under those numbers, verified in its
    /// own input handler.
    /// </summary>
    public int Button { get; set; }

    public BindingModifiers Modifiers { get; set; }

    public BindingKind Kind { get; set; } = BindingKind.Action;

    /// <summary>The Action row id, when <see cref="Kind"/> is an action.</summary>
    public uint ActionId { get; set; }

    /// <summary>
    /// Whether this binding answers to what is being held right now. Compared against the
    /// exact set, not a subset: Shift+Left and plain Left are different bindings, and a plain
    /// Left that also fired while Shift was down would make the second one unreachable.
    /// </summary>
    public bool Matches(int button, BindingModifiers held) =>
        this.Button == button && this.Modifiers == held;

    /// <summary>A copy, for the editing that happens before a change is kept.</summary>
    public MouseBinding Clone() => new()
    {
        Button = this.Button,
        Modifiers = this.Modifiers,
        Kind = this.Kind,
        ActionId = this.ActionId,
    };
}
