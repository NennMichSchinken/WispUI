using System;

namespace WispUI.Appearance;

/// <summary>
/// The parts of an element's appearance, as a mask. Two things use it: an element says
/// which of them it actually has, and the paste panel says which of them to carry over.
/// <para>
/// Every part is listed here from the start, including ones no element has yet. This is the
/// vocabulary of the feature, not a list of what happens to be built — an element that
/// gains backgrounds later needs a new flag on itself, not a new name here.
/// </para>
/// </summary>
[Flags]
internal enum AppearanceFields
{
    None = 0,

    /// <summary>What decides the bar colour, and the fixed colour it falls back on.</summary>
    Colours = 1 << 0,

    /// <summary>The fill drawn inside a bar.</summary>
    Texture = 1 << 1,

    /// <summary>Bar shape and fill direction.</summary>
    Shape = 1 << 2,

    /// <summary>Font role, outline, and what the numbers say.</summary>
    Text = 1 << 3,

    /// <summary>The plate behind the bar.</summary>
    Background = 1 << 4,

    Opacity = 1 << 5,

    /// <summary>
    /// A picture on the element, and where it sits: the job icon today, role icons and raid
    /// markers later. Its own part rather than a corner of <see cref="Text"/> — an icon and a
    /// name are two different things to want carried over, and the panel's whole job is to let
    /// you say which.
    /// </summary>
    Icon = 1 << 6,

    All = Colours | Texture | Shape | Text | Background | Opacity | Icon,
}
