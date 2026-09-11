using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// The party frames. At this stage it is the skeleton the spec asks for first: one rectangle
/// per member, stacked, with the name and the health it read. No bars, no icons, no layout
/// options yet — what this proves is that the data arrives correctly and that nothing draws
/// when nothing should (spec §9, steps 1 and 2).
/// </summary>
internal sealed class PartyFramesElement : HudElement
{
    private readonly Configuration m_config;
    private readonly PartySnapshot m_snapshot = new();

    /// <summary>
    /// The health line, kept per slot and rebuilt only when the numbers move. Formatting a
    /// number allocates, and a party of eight would do it eight times a frame for text that
    /// is identical to the frame before.
    /// </summary>
    private readonly string[] m_healthText = new string[PartySnapshot.Capacity];
    private readonly uint[] m_healthTextFor = new uint[PartySnapshot.Capacity];

    /// <summary>What the party looked like when it was last written to the log.</summary>
    private readonly uint[] m_logged = new uint[PartySnapshot.Capacity];
    private int m_loggedCount = -1;

    public PartyFramesElement(Configuration config)
    {
        m_config = config;
    }

    public override string Name => Strings.NavPartyFrames;

    public override bool Enabled => m_config.PartyFramesEnabled;

    public override void Collect()
    {
        if (EditMode.IsActive)
        {
            m_snapshot.FillPlaceholders();
            return;
        }

        m_snapshot.Collect();
        this.LogIfPartyChanged();
    }

    public override void Draw(ImDrawListPtr dl)
    {
        Configuration.PartyFramesConfig cfg = m_config.PartyFrames;

        float width = Tokens.Px(cfg.FrameWidth);
        float height = Tokens.Px(cfg.FrameHeight);
        float spacing = Tokens.Px(cfg.Spacing);
        float padding = Tokens.Metric.FramePadding;
        float x = Tokens.Px(cfg.PositionX);
        float y = Tokens.Px(cfg.PositionY);

        PartyMemberSnapshot[] members = m_snapshot.Members;
        int count = m_snapshot.Count;

        for (int i = 0; i < count; i++)
        {
            ref PartyMemberSnapshot member = ref members[i];
            Vector2 offset = FrameLayout.Offset(
                i,
                count,
                (FrameDirection)cfg.Direction,
                cfg.Lines,
                width,
                height,
                spacing);

            Vector2 min = new(x + offset.X, y + offset.Y);
            Vector2 max = new(min.X + width, min.Y + height);
            float top = min.Y;

            dl.AddRectFilled(min, max, Tokens.Col.FrameBg);

            // The role as a stripe down the leading edge. It is the one piece of the finished
            // frame that already has a measured colour, and it makes the snapshot's reading of
            // the job visible without a second line of text.
            dl.AddRectFilled(min, new Vector2(min.X + Tokens.Line(3f), max.Y), Jobs.RoleColour(member.Role));
            dl.AddRect(min, max, Tokens.Col.FrameEdge, 0f, ImDrawFlags.None, Tokens.Line(1f));

            float textY = Chrome.CenterY(top, height, Ink.Role.Body);
            Ink.Draw(
                dl,
                Ink.Role.Body,
                new Vector2(min.X + padding + Tokens.Line(3f), textY),
                member.IsLocalPlayer ? Tokens.Col.GoldHi : Tokens.Col.Ink,
                member.Name);

            string health = this.HealthText(i, ref member);
            float healthWidth = Ink.Measure(Ink.Role.Body, health).X;
            Ink.Draw(dl, Ink.Role.Body, new Vector2(max.X - padding - healthWidth, textY), Tokens.Col.Ink, health);
        }
    }

    private string HealthText(int slot, ref PartyMemberSnapshot member)
    {
        if (m_healthTextFor[slot] != member.Hp || m_healthText[slot] is null)
        {
            m_healthTextFor[slot] = member.Hp;
            m_healthText[slot] = member.Hp.ToString(CultureInfo.InvariantCulture)
                + " / "
                + member.MaxHp.ToString(CultureInfo.InvariantCulture);
        }

        return m_healthText[slot];
    }

    /// <summary>
    /// Writes the snapshot to the log whenever the party changes hands — once per change, not
    /// per frame. This is what lets the numbers be checked against the game's own party list
    /// instead of taken on trust, and it costs nothing while the group stays as it is.
    /// </summary>
    private void LogIfPartyChanged()
    {
        PartyMemberSnapshot[] members = m_snapshot.Members;
        bool changed = m_loggedCount != m_snapshot.Count;

        for (int i = 0; !changed && i < m_snapshot.Count; i++)
        {
            changed = m_logged[i] != members[i].EntityId;
        }

        if (!changed)
        {
            return;
        }

        m_loggedCount = m_snapshot.Count;
        Services.Log.Information(
            "Party snapshot: {Count} member(s), solo={Solo}",
            m_snapshot.Count,
            m_snapshot.IsSolo);

        for (int i = 0; i < m_snapshot.Count; i++)
        {
            ref PartyMemberSnapshot member = ref members[i];
            m_logged[i] = member.EntityId;
            Services.Log.Information(
                "  [{Slot}] {Name} job={Job} role={Role} hp={Hp}/{MaxHp} mp={Mp}/{MaxMp} self={Self}",
                i,
                member.Name,
                member.JobId,
                member.Role,
                member.Hp,
                member.MaxHp,
                member.Mp,
                member.MaxMp,
                member.IsLocalPlayer);
        }
    }
}
