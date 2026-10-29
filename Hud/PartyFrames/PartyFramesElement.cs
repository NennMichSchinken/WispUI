using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using WispUI.Core;
using WispUI.Data;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// The party list module: the cleanse and raise marks WispUI lays on the game's own party
/// list. Nothing of its own is drawn on the world.
/// <para>
/// 🔴 The game's list only (Florian, 2026-10-29). WispUI's own rectangular frames are gone:
/// it plays better on the game's list and stays closer to the game. The marks are nodes in
/// that list (<see cref="NativeUi.UpdatePartyMarks"/>), updated right before it draws.
/// </para>
/// </summary>
internal sealed class PartyFramesElement : HudElement, IDisposable
{
    private const string PartyListAddon = "_PartyList";

    private readonly Configuration m_config;

    /// <summary>The live party, shared with Quick Dispel so the party is read once a frame.</summary>
    private readonly PartySnapshot m_snapshot;

    /// <summary>The stand-ins the settings window preview is drawn from. Never the live one.</summary>
    private readonly PartySnapshot m_preview = new();

    /// <summary>
    /// The mark each row of the game's list should wear this frame, top to bottom — the
    /// colour with its strength as alpha, zero for none. Worked out in <see cref="Collect"/>
    /// from the shared party pass, and handed to the game's list right before it draws.
    /// </summary>
    private readonly uint[] m_marks = new uint[PartySnapshot.Capacity];

    public PartyFramesElement(Configuration config, PartySnapshot party)
    {
        m_config = config;
        m_snapshot = party;

        Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, PartyListAddon, this.OnPartyListDraw);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, PartyListAddon, this.OnPartyListGone);
    }

    public override string Name => Strings.NavPartyFrames;

    public override bool Enabled => m_config.PartyFramesEnabled;

    /// <summary>Nothing to drag: the list is where the game's HUD layout puts it.</summary>
    public override bool Movable => false;

    /// <summary>
    /// Unhooks and takes the marks out. Dalamud disposes a plugin on the game's thread, which
    /// is the thread the list may be touched on; anywhere else it is left to the game.
    /// </summary>
    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, PartyListAddon, this.OnPartyListDraw);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, PartyListAddon, this.OnPartyListGone);

        if (Services.Framework.IsInFrameworkUpdateThread)
        {
            NativeUi.RemovePartyMarks();
        }
    }

    private void OnPartyListDraw(AddonEvent type, AddonArgs args)
    {
        if (!m_config.PartyFramesEnabled)
        {
            NativeUi.RemovePartyMarks();
            return;
        }

        NativeUi.UpdatePartyMarks(args.Addon.Address, m_marks);
    }

    private void OnPartyListGone(AddonEvent type, AddonArgs args) => NativeUi.ForgetPartyMarks(args.Addon.Address);

    public override void Bounds(out Vector2 min, out Vector2 max)
    {
        min = default;
        max = default;
    }

    public override void MoveTo(Vector2 topLeft)
    {
    }

    public override void Collect()
    {
        // Stand-ins in edit mode, the party otherwise — decided inside, once for every reader
        // of the shared snapshot. In edit mode the stand-ins carry no marks, so the game's
        // list goes unmarked while things are arranged.
        m_snapshot.Refresh(m_config.PartyFrames.OwnBuffsOnly);
        this.CollectMarks();
    }

    /// <summary>
    /// Which mark each row wears: raise over cleanse, because "somebody is already on it" is
    /// the reason to stop looking at a row; cleanse only on jobs that can cleanse, when asked.
    /// </summary>
    private void CollectMarks()
    {
        Configuration.PartyFramesConfig cfg = m_config.PartyFrames;
        bool cleanse = cfg.ShowCleanseMark && (!cfg.CleanseOnlyWhenAble || CanCleanseNow());
        PartyMemberSnapshot[] members = m_snapshot.Members;

        for (int i = 0; i < m_marks.Length; i++)
        {
            m_marks[i] = 0u;

            if (i >= m_snapshot.Count)
            {
                continue;
            }

            ref PartyMemberSnapshot member = ref members[i];

            if (cfg.ShowRaiseMark && member.RaiseRemaining > 0f)
            {
                m_marks[i] = Tokens.Col.Faded(cfg.RaiseColour, cfg.RaiseOpacity);
            }
            else if (cleanse && member.HasDispellable)
            {
                m_marks[i] = Tokens.Col.Faded(cfg.CleanseColour, cfg.CleanseOpacity);
            }
        }
    }

    /// <summary>
    /// Whether the player could actually take an effect off somebody right now. Read once a
    /// frame from the one player object, which costs nothing.
    /// </summary>
    private static bool CanCleanseNow()
    {
        var player = Services.Objects.LocalPlayer;

        return player is not null && StatusData.CanCleanse(player.ClassJob.RowId, player.Level);
    }

    /// <summary>Nothing on the world: the marks are in the game's list.</summary>
    public override void Draw(ImDrawListPtr dl)
    {
    }

    public override bool HasPreview => true;

    public override Vector2 PreviewSize(int count) => LegacyPreview.Size(count);

    /// <summary>
    /// The game's list as a stand-in, with the marks WispUI lays on it, so the marks can be
    /// set up without a party.
    /// </summary>
    public override void DrawPreview(ImDrawListPtr dl, Vector2 origin, int count)
    {
        m_preview.FillPlaceholders(count, true);
        LegacyPreview.Draw(dl, origin, m_preview, m_config.PartyFrames);
    }

    /// <summary>
    /// A rectangle drawn as four filled bars rather than as a stroke. ImGui centres a stroke
    /// on its path, so half of it falls outside the rectangle and is antialiased — the same
    /// reason the window's rings and the party number's edge are filled shapes.
    /// </summary>
    internal static void Ring(ImDrawListPtr dl, Vector2 min, Vector2 max, float thickness, uint colour)
    {
        float t = MathF.Min(thickness, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        if (t <= 0f)
        {
            return;
        }

        dl.AddRectFilled(min, new Vector2(max.X, min.Y + t), colour);
        dl.AddRectFilled(new Vector2(min.X, max.Y - t), max, colour);
        dl.AddRectFilled(new Vector2(min.X, min.Y + t), new Vector2(min.X + t, max.Y - t), colour);
        dl.AddRectFilled(new Vector2(max.X - t, min.Y + t), new Vector2(max.X, max.Y - t), colour);
    }

    /// <summary>
    /// The seconds, or the minutes once there are too many seconds to read.
    /// <para>
    /// Out of two tables built once, because this runs per icon per frame and building
    /// "37" would allocate every one of them (CLAUDE.md §7.1). Null for an effect that does
    /// not run out — there is no number to write for something that is simply there.
    /// </para>
    /// </summary>
    internal static string? DurationText(float remaining)
    {
        if (remaining <= 0f)
        {
            return null;
        }

        if (remaining < 60f)
        {
            int seconds = (int)MathF.Ceiling(remaining);
            return seconds >= 1 && seconds <= SecondsText.Length ? SecondsText[seconds - 1] : null;
        }

        int minutes = (int)MathF.Ceiling(remaining / 60f);
        return minutes >= 1 && minutes <= MinutesText.Length ? MinutesText[minutes - 1] : null;
    }

    private static readonly string[] SecondsText = BuildSecondsText();

    private static readonly string[] MinutesText = BuildMinutesText();

    private static string[] BuildSecondsText()
    {
        var text = new string[60];

        for (int i = 0; i < text.Length; i++)
        {
            text[i] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static string[] BuildMinutesText()
    {
        // Half an hour is past anything a party frame shows; the long ones are the upkeep
        // effects that never reach these rows in the first place.
        var text = new string[30];

        for (int i = 0; i < text.Length; i++)
        {
            text[i] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
        }

        return text;
    }
}
