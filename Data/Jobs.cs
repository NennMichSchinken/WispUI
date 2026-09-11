using WispUI.Style;

namespace WispUI.Data;

/// <summary>What a job does in a party. Everything that is not one of the three is a job we
/// do not colour by role — a crafter, a gatherer, or a class before it takes its job.</summary>
internal enum JobRole
{
    None = 0,
    Tank,
    Healer,
    Dps,
}

/// <summary>
/// Job facts, looked up by <c>ClassJob</c> row id: what role it fills, what colour it wears,
/// and which icon belongs to it.
/// <para>
/// The colours and the icon rule come from HamMeter, our own published plugin — the same job
/// reads the same colour in both, which is the point of a suite. They are its tested values,
/// not a fresh guess, and copying them is ours to do.
/// </para>
/// <para>
/// Everything is a flat array indexed by row id, filled once at type load: a bar asks for a
/// colour every frame, and neither a dictionary lookup nor a sheet read belongs in that path.
/// </para>
/// </summary>
internal static class Jobs
{
    /// <summary>One past the highest ClassJob row id we know (PCT is 42).</summary>
    private const int Count = 43;

    /// <summary>The framed job icon of a job is its row id offset by this.</summary>
    private const int IconBase = 62100;

    private static readonly uint[] Colours = new uint[Count];
    private static readonly JobRole[] Roles = new JobRole[Count];

    static Jobs()
    {
        // Tanks
        Set(19, JobRole.Tank, 0xA8D2E6); // Paladin
        Set(21, JobRole.Tank, 0xCF2A2A); // Warrior
        Set(32, JobRole.Tank, 0xD126CC); // Dark Knight
        Set(37, JobRole.Tank, 0x796D30); // Gunbreaker

        // Healers
        Set(24, JobRole.Healer, 0xFFFDF5); // White Mage
        Set(28, JobRole.Healer, 0x8657FF); // Scholar
        Set(33, JobRole.Healer, 0xFFE74A); // Astrologian
        Set(40, JobRole.Healer, 0x80D1BA); // Sage

        // Melee
        Set(20, JobRole.Dps, 0xD69C00); // Monk
        Set(22, JobRole.Dps, 0x4164CD); // Dragoon
        Set(30, JobRole.Dps, 0xAF2F2F); // Ninja
        Set(34, JobRole.Dps, 0xE46D10); // Samurai
        Set(39, JobRole.Dps, 0x965A96); // Reaper
        Set(41, JobRole.Dps, 0x3D8C61); // Viper

        // Physical ranged
        Set(23, JobRole.Dps, 0xC1D95D); // Bard
        Set(31, JobRole.Dps, 0x6EE1D6); // Machinist
        Set(38, JobRole.Dps, 0xFE9C9C); // Dancer

        // Casters
        Set(25, JobRole.Dps, 0xA331D6); // Black Mage
        Set(27, JobRole.Dps, 0x2D9B38); // Summoner
        Set(35, JobRole.Dps, 0xE32940); // Red Mage
        Set(42, JobRole.Dps, 0xF279A6); // Pictomancer
        Set(36, JobRole.Dps, 0x005FFF); // Blue Mage

        // The classes a job grows out of. They keep the job's colour and role, so a party
        // member below level 30 is not suddenly uncoloured.
        Inherit(1, 19);  // Gladiator -> Paladin
        Inherit(3, 21);  // Marauder -> Warrior
        Inherit(6, 24);  // Conjurer -> White Mage
        Inherit(26, 28); // Arcanist -> Scholar
        Inherit(2, 20);  // Pugilist -> Monk
        Inherit(4, 22);  // Lancer -> Dragoon
        Inherit(29, 30); // Rogue -> Ninja
        Inherit(5, 23);  // Archer -> Bard
        Inherit(7, 25);  // Thaumaturge -> Black Mage
    }

    /// <summary>The colour of a job, or the body text colour for anything we do not know.</summary>
    public static uint Colour(uint jobId) =>
        jobId < Count && Colours[jobId] != 0 ? Colours[jobId] : Tokens.Col.Ink;

    public static JobRole Role(uint jobId) => jobId < Count ? Roles[jobId] : JobRole.None;

    /// <summary>The colour of a role — the three a player reads before they read a name.</summary>
    public static uint RoleColour(JobRole role) => role switch
    {
        JobRole.Tank => Tokens.Col.RoleTank,
        JobRole.Healer => Tokens.Col.RoleHealer,
        JobRole.Dps => Tokens.Col.RoleDps,
        _ => Tokens.Col.InkDim,
    };

    /// <summary>The framed job icon for a job, or zero if we have no job.</summary>
    public static uint IconId(uint jobId) => jobId == 0 ? 0u : (uint)IconBase + jobId;

    private static void Set(int jobId, JobRole role, uint hex)
    {
        Roles[jobId] = role;
        Colours[jobId] = 0xFF000000u | ((hex & 0x0000FFu) << 16) | (hex & 0x00FF00u) | ((hex & 0xFF0000u) >> 16);
    }

    private static void Inherit(int classId, int jobId)
    {
        Roles[classId] = Roles[jobId];
        Colours[classId] = Colours[jobId];
    }
}
