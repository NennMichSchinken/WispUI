namespace WispUI.Hud
{
    // 🔴 What is left of WispUI's own party frames (removed 2026-10-29, Florian: the game's
    // list plays better and stays closer to the game). The configuration still carries the
    // frames' fields, and its migration chain and Sanitise read these enums by number. Kept
    // so an old configuration still loads; nothing draws with them. Never renumber.

    /// <summary>The figure a frame's health bar carried.</summary>
    internal enum HealthTextMode
    {
        Current = 0,
        Percent = 1,
        Deficit = 2,
    }

    /// <summary>How a frame shortened a name.</summary>
    internal enum NameShortening
    {
        Full = 0,
        Surname = 1,
        Forename = 2,
    }

    /// <summary>The shapes a frame mark could take on a frame of ours.</summary>
    internal enum FrameMarkStyle
    {
        None = 0,
        Border = 1,
        Bar = 2,
        Full = 3,
    }
}

namespace WispUI.Appearance
{
    /// <summary>What decided the colour of a frame's health bar.</summary>
    internal enum BarColourMode
    {
        Role = 0,
        Job = 1,
        Fixed = 2,
    }
}
