using System;
using System.Collections.Generic;
using WispUI.Data;

namespace WispUI.Core;

/// <summary>
/// What decides whether a profile is the one being played.
/// <para>
/// 🔴 Two kinds rather than one, and the reason is the setup somebody actually has. Naming
/// every job one at a time is exact and nobody wants to do it five times to say "healers";
/// naming only roles cannot say "White Mage differently from the rest" (Florian,
/// 2026-09-20). Both, with jobs beating roles, is the shortest way to say either.
/// </para>
/// </summary>
public enum ProfileScopeKind
{
    /// <summary>Whatever no other profile has claimed. Exactly one profile is this.</summary>
    Fallback = 0,

    /// <summary>Every job of one role.</summary>
    Role = 1,

    /// <summary>The jobs named in the list, and no others.</summary>
    Jobs = 2,
}

/// <summary>
/// One named set of settings, and who it is for.
/// <para>
/// A profile holds the modules and nothing else. What is <em>not</em> in here is as
/// deliberate as what is: the interface scale belongs to the monitor rather than to the job,
/// and bindings belong to the hands at the keyboard — a profile from somebody else must not
/// be able to move either (Florian, 2026-09-20).
/// </para>
/// </summary>
[Serializable]
public sealed class Profile
{
    /// <summary>What it is called. The one thing about a profile the player wrote themselves.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Which of the three ways this profile says who it is for.</summary>
    public int ScopeKind { get; set; } = (int)ProfileScopeKind.Jobs;

    /// <summary>The role, when <see cref="ScopeKind"/> says role. A <c>JobRole</c> value.</summary>
    public int Role { get; set; } = (int)JobRole.Healer;

    /// <summary>The ClassJob row ids, when <see cref="ScopeKind"/> says jobs.</summary>
    public List<uint> Jobs { get; set; } = new();

    /// <summary>
    /// Whether changing to a job this profile covers puts it on by itself.
    /// <para>
    /// Per profile rather than one switch for the suite: a profile built for one fight is
    /// something to reach for on purpose, and it should be able to sit in the list without
    /// grabbing the screen every time its job comes up.
    /// </para>
    /// </summary>
    public bool Automatic { get; set; } = true;

    /// <summary>The party frames as this profile has them.</summary>
    public Configuration.PartyFramesConfig PartyFrames { get; set; } = new();

    /// <summary>
    /// Whether the party frames are drawn at all under this profile. Off by default, for the
    /// reason on <see cref="Configuration.PartyFramesEnabled"/>.
    /// </summary>
    public bool PartyFramesEnabled { get; set; }

    /// <summary>
    /// Whether this profile answers for the job being played. The fallback answers for
    /// nobody here — it is what is left when nothing else answered, which is a question for
    /// <see cref="ProfileSet"/> and not for a profile about itself.
    /// </summary>
    public bool Covers(uint jobId)
    {
        switch ((ProfileScopeKind)this.ScopeKind)
        {
            case ProfileScopeKind.Role:
                return jobId != 0u && (int)Jobs_Role(jobId) == this.Role;

            case ProfileScopeKind.Jobs:
                return jobId != 0u && this.Jobs.Contains(jobId);

            default:
                return false;
        }
    }

    /// <summary>
    /// How specifically it covers that job: higher wins. Named jobs beat a role, because
    /// somebody who wrote a job down meant that job and not the four beside it.
    /// </summary>
    public int Specificity => (ProfileScopeKind)this.ScopeKind switch
    {
        ProfileScopeKind.Jobs => 2,
        ProfileScopeKind.Role => 1,
        _ => 0,
    };

    /// <summary>
    /// A copy that shares nothing with this one. Used for duplicating, for taking a profile
    /// out of a code, and for putting one on — a profile that handed out its own objects
    /// would find them edited behind its back the moment the settings screen touched them.
    /// </summary>
    public Profile Clone() => new()
    {
        Name = this.Name,
        ScopeKind = this.ScopeKind,
        Role = this.Role,
        Jobs = new List<uint>(this.Jobs),
        Automatic = this.Automatic,
        PartyFramesEnabled = this.PartyFramesEnabled,
        PartyFrames = CloneConfig(this.PartyFrames),
    };

    /// <summary>
    /// Puts every number back inside its range. The same pass the configuration runs on
    /// load, which is the point: a profile out of a stranger's code goes through exactly
    /// what a file on disk goes through, and neither gets a shorter path than the other.
    /// </summary>
    internal void Sanitise()
    {
        this.Name = Trimmed(this.Name);
        this.ScopeKind = Enum.IsDefined((ProfileScopeKind)this.ScopeKind) ? this.ScopeKind : (int)ProfileScopeKind.Jobs;
        this.Role = Enum.IsDefined((JobRole)this.Role) && this.Role != (int)JobRole.None
            ? this.Role
            : (int)JobRole.Healer;

        this.Jobs ??= new List<uint>();
        this.PartyFrames ??= new Configuration.PartyFramesConfig();
        this.PartyFrames.Sanitise();

        // A job list out of a code can hold anything. Only rows the game has are kept, and
        // duplicates go: a list of the same job eight times is a list that would be drawn
        // eight times in the settings screen.
        for (int i = this.Jobs.Count - 1; i >= 0; i--)
        {
            uint id = this.Jobs[i];

            if (JobList.IndexOf(id) < 0 || this.Jobs.IndexOf(id) != i)
            {
                this.Jobs.RemoveAt(i);
            }
        }
    }

    /// <summary>The longest a name may be. Past this it stops fitting its own row.</summary>
    internal const int MaxNameLength = 24;

    private static string Trimmed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Localization.Strings.ProfileUnnamed;
        }

        name = name.Trim();

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    private static JobRole Jobs_Role(uint jobId) => Data.Jobs.Role(jobId);

    /// <summary>
    /// A party frames block copied field by field.
    /// <para>
    /// 🔴 Written out rather than serialised and read back. A round trip through JSON copies
    /// whatever the serialiser happens to pick up, which is a different set from what a
    /// profile means to carry — the migration relics would ride along, and a stranger's code
    /// would hand this build a value its own migration has already run past.
    /// </para>
    /// </summary>
    internal static Configuration.PartyFramesConfig CloneConfig(Configuration.PartyFramesConfig from)
    {
        var to = new Configuration.PartyFramesConfig();
        Copy(from, to);
        return to;
    }

    /// <summary>
    /// Every setting a profile carries, from one block to another.
    /// <para>
    /// 🔴 This list is GENERATED, not typed. A hundred lines written by hand is a list
    /// with a setting missing in it, and a missing line here is the worst kind of bug: the
    /// profile simply does not carry that one setting, silently, and nobody finds out until
    /// they wonder why their friend's frames look slightly different. Regenerate it from
    /// the configuration itself after adding a setting, from the repository root:
    /// </para>
    /// <code>
    /// awk '/public sealed class PartyFramesConfig/,/^    }$/' Core/Configuration.cs \
    ///   | grep -oP 'public [\w\.\&lt;&gt;\?]+ \K\w+(?= \{ get; set; \})' \
    ///   | grep -v -x -f skip.txt \
    ///   | awk '{print "        to."$1" = from."$1";"}'
    /// </code>
    /// <para>
    /// <c>skip.txt</c> holds what a profile must NOT carry, one name per line:
    /// <c>BarStyle</c>, <c>Font</c>, <c>ShortenNames</c>, <c>ClickToTarget</c>,
    /// <c>ContextMenu</c>, <c>MouseoverCasting</c>, <c>AuraNumberSize</c>, <c>FontName</c>,
    /// <c>TextWeight</c>, <c>TextEdge</c>, <c>Spacing</c> — migration relics, kept only so an old file can
    /// be read once — and <c>Bindings</c>, <c>Mouseover</c>, which belong to the hands at
    /// the keyboard rather than to the look of a frame.
    /// </para>
    /// </summary>
    internal static void Copy(Configuration.PartyFramesConfig from, Configuration.PartyFramesConfig to)
    {
        to.BarStyleName = from.BarStyleName;
        to.ColourMode = from.ColourMode;
        to.BarOpacity = from.BarOpacity;
        to.SmoothBars = from.SmoothBars;
        to.ShowName = from.ShowName;
        to.NamePosition = from.NamePosition;
        to.NameSize = from.NameSize;
        to.NameX = from.NameX;
        to.NameY = from.NameY;
        to.NameInJobColour = from.NameInJobColour;
        to.NameShortening = from.NameShortening;
        to.ShowHealthText = from.ShowHealthText;
        to.HpTextMode = from.HpTextMode;
        to.HpTextSize = from.HpTextSize;
        to.HpTextPosition = from.HpTextPosition;
        to.HpTextX = from.HpTextX;
        to.HpTextY = from.HpTextY;
        to.ShowMana = from.ShowMana;
        to.ManaStyle = from.ManaStyle;
        to.ManaHeight = from.ManaHeight;
        to.ManaForTanks = from.ManaForTanks;
        to.ManaForHealers = from.ManaForHealers;
        to.ManaForDps = from.ManaForDps;
        to.ShowShield = from.ShowShield;
        to.ShieldStyleName = from.ShieldStyleName;
        to.ShieldColour = from.ShieldColour;
        to.ShieldOpacity = from.ShieldOpacity;
        to.ShowJobIcon = from.ShowJobIcon;
        to.JobIconSize = from.JobIconSize;
        to.JobIconStyle = from.JobIconStyle;
        to.JobIconPosition = from.JobIconPosition;
        to.JobIconX = from.JobIconX;
        to.JobIconY = from.JobIconY;
        to.JobIconHideDps = from.JobIconHideDps;
        to.MouseoverTarget = from.MouseoverTarget;
        to.HighlightHovered = from.HighlightHovered;
        to.ShowLeaderIcon = from.ShowLeaderIcon;
        to.LeaderIconSize = from.LeaderIconSize;
        to.LeaderIconPosition = from.LeaderIconPosition;
        to.LeaderIconX = from.LeaderIconX;
        to.LeaderIconY = from.LeaderIconY;
        to.ShowPartyNumber = from.ShowPartyNumber;
        to.PartyNumberSize = from.PartyNumberSize;
        to.PartyNumberPosition = from.PartyNumberPosition;
        to.PartyNumberX = from.PartyNumberX;
        to.PartyNumberY = from.PartyNumberY;
        to.ShowAuras = from.ShowAuras;
        to.AuraSize = from.AuraSize;
        to.AuraPosition = from.AuraPosition;
        to.AuraX = from.AuraX;
        to.AuraY = from.AuraY;
        to.AuraMaxCount = from.AuraMaxCount;
        to.AuraShowStacks = from.AuraShowStacks;
        to.AuraSwipe = from.AuraSwipe;
        to.AuraShowDuration = from.AuraShowDuration;
        to.AuraDispelBorder = from.AuraDispelBorder;
        to.AuraDispelThickness = from.AuraDispelThickness;
        to.AuraDurationSize = from.AuraDurationSize;
        to.AuraStackSize = from.AuraStackSize;
        to.ShowAuraTooltips = from.ShowAuraTooltips;
        to.ShowBuffTooltips = from.ShowBuffTooltips;
        to.ShowOtherTooltips = from.ShowOtherTooltips;
        to.ShowBuffs = from.ShowBuffs;
        to.OwnBuffsOnly = from.OwnBuffsOnly;
        to.BuffSize = from.BuffSize;
        to.BuffPosition = from.BuffPosition;
        to.BuffX = from.BuffX;
        to.BuffY = from.BuffY;
        to.BuffMaxCount = from.BuffMaxCount;
        to.BuffShowStacks = from.BuffShowStacks;
        to.BuffSwipe = from.BuffSwipe;
        to.BuffShowDuration = from.BuffShowDuration;
        to.BuffDurationSize = from.BuffDurationSize;
        to.BuffStackSize = from.BuffStackSize;
        to.ShowOtherBuffs = from.ShowOtherBuffs;
        to.OtherSize = from.OtherSize;
        to.OtherPosition = from.OtherPosition;
        to.OtherX = from.OtherX;
        to.OtherY = from.OtherY;
        to.OtherMaxCount = from.OtherMaxCount;
        to.OtherShowDuration = from.OtherShowDuration;
        to.OtherDurationSize = from.OtherDurationSize;
        to.OtherStackSize = from.OtherStackSize;
        to.CleanseMark = from.CleanseMark;
        to.ShowCleanseMark = from.ShowCleanseMark;
        to.CleanseOnlyWhenAble = from.CleanseOnlyWhenAble;
        to.CleanseThickness = from.CleanseThickness;
        to.CleanseColour = from.CleanseColour;
        to.CleanseOpacity = from.CleanseOpacity;
        to.RaiseMark = from.RaiseMark;
        to.ShowRaiseMark = from.ShowRaiseMark;
        to.RaiseColour = from.RaiseColour;
        to.RaiseThickness = from.RaiseThickness;
        to.RaiseOpacity = from.RaiseOpacity;
        to.ShowRaiseIcon = from.ShowRaiseIcon;
        to.ShowInvulnIcon = from.ShowInvulnIcon;
        to.ShowRescueIcon = from.ShowRescueIcon;
        to.RescueIconSize = from.RescueIconSize;
        to.RescueIconPosition = from.RescueIconPosition;
        to.RescueIconX = from.RescueIconX;
        to.RescueIconY = from.RescueIconY;
        to.PositionX = from.PositionX;
        to.PositionY = from.PositionY;
        to.FrameWidth = from.FrameWidth;
        to.FrameHeight = from.FrameHeight;
        to.SpacingX = from.SpacingX;
        to.SpacingY = from.SpacingY;
        to.Direction = from.Direction;
        to.Lines = from.Lines;
        to.HideNativePartyList = from.HideNativePartyList;
    }
}
