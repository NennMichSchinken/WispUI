using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Data;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// The party frames themselves: a rectangle per member carrying a health bar, a figure on it,
/// and a strip of mana for whoever is meant to have one (spec §9, step 4).
/// <para>
/// Everything here reads from the snapshot and nothing asks the game a second time. The parts
/// go down in the order the spec lays out (§5): the ground, the bars, the edge, and the text
/// on top of all of it.
/// </para>
/// </summary>
internal sealed class PartyFramesElement : HudElement
{
    /// <summary>
    /// How quickly a smoothed bar closes the gap to the real value, per second. Set so a
    /// large change is all but arrived inside about a tenth of a second: long enough to read
    /// as movement, short enough that the bar is never meaningfully behind the fight.
    /// </summary>
    private const float SmoothRate = 22f;

    /// <summary>Below this the bar is simply at its value; the last thousandth is not worth carrying.</summary>
    private const float SmoothSettle = 0.001f;

    private readonly Configuration m_config;
    private readonly PartySnapshot m_snapshot = new();

    /// <summary>
    /// The health figure, kept per slot and rebuilt only when one of the numbers behind it
    /// moves. Formatting allocates, and a party of eight would do it eight times a frame for
    /// text identical to the frame before.
    /// </summary>
    private readonly string[] m_healthText = new string[PartySnapshot.Capacity];
    private readonly int[] m_healthTextMode = new int[PartySnapshot.Capacity];
    private readonly uint[] m_healthTextHp = new uint[PartySnapshot.Capacity];
    private readonly uint[] m_healthTextMaxHp = new uint[PartySnapshot.Capacity];

    /// <summary>The name as it is drawn: the game's own string, or a shortened copy of it.</summary>
    private readonly string[] m_drawnName = new string[PartySnapshot.Capacity];
    private readonly string?[] m_drawnNameFrom = new string?[PartySnapshot.Capacity];
    private readonly bool[] m_drawnNameShort = new bool[PartySnapshot.Capacity];

    /// <summary>Where a smoothed bar has got to, and who it belongs to.</summary>
    private readonly float[] m_shownHealth = new float[PartySnapshot.Capacity];
    private readonly uint[] m_shownHealthFor = new uint[PartySnapshot.Capacity];

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
        float border = Tokens.Metric.FrameBorder;
        float x = Tokens.Px(cfg.PositionX);
        float y = Tokens.Px(cfg.PositionY);
        float delta = ImGui.GetIO().DeltaTime;

        BarStyle style = BarStyles.All[Math.Clamp(cfg.BarStyle, 0, BarStyles.All.Length - 1)];
        var colourMode = (BarColourMode)cfg.ColourMode;
        var manaStyle = (ManaStyle)cfg.ManaStyle;
        HealthTextMode textMode = HealthText.At(cfg.HpTextMode);

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

            Vector2 innerMin = new(min.X + border, min.Y + border);
            Vector2 innerMax = new(max.X - border, max.Y - border);

            // A frame smaller than its own edge has no inside to draw into. It cannot happen
            // at the sizes the sliders offer, and one branch is cheaper than handing a draw
            // list a backwards rectangle.
            if (innerMax.X <= innerMin.X || innerMax.Y <= innerMin.Y)
            {
                continue;
            }

            dl.AddRectFilled(min, max, Tokens.Col.FrameBg);

            float healthBottom = innerMax.Y;
            bool mana = ShowsMana(cfg, ref member);
            float manaHeight = Tokens.Px(cfg.ManaHeight);
            float manaGap = manaStyle == ManaStyle.Bar ? border : 0f;

            if (mana)
            {
                healthBottom = innerMax.Y - manaHeight - manaGap;

                // A frame short enough that mana would leave no health bar keeps the health
                // bar. Which of the two has to stay readable is never in question.
                if (healthBottom <= innerMin.Y)
                {
                    mana = false;
                    healthBottom = innerMax.Y;
                }
            }

            uint colour = Tokens.Col.Faded(BarColour(colourMode, ref member), cfg.BarOpacity);
            float fraction = this.HealthFraction(i, ref member, cfg.SmoothBars, delta);
            Vector2 barMin = innerMin;
            Vector2 barMax = new(innerMax.X, healthBottom);

            if (fraction > 0f)
            {
                // The style is painted across the whole bar and then clipped to the fill, so
                // the fill uncovers a bar that always has the same shape rather than squeezing
                // that shape into whatever width is left.
                float fillRight = MathF.Round(barMin.X + ((barMax.X - barMin.X) * fraction));
                dl.PushClipRect(barMin, new Vector2(fillRight, barMax.Y), true);
                BarStyles.Draw(dl, style, barMin, barMax, colour, 0f);
                dl.PopClipRect();
            }

            if (mana)
            {
                Vector2 manaMin = new(innerMin.X, innerMax.Y - manaHeight);
                float manaFraction = member.MaxMp > 0
                    ? Math.Clamp(member.Mp / (float)member.MaxMp, 0f, 1f)
                    : 0f;

                // The strip gets no track of its own: at three pixels a dark bed under it only
                // adds a second line. A bar wide enough to read as a bar gets one.
                if (manaStyle == ManaStyle.Bar)
                {
                    dl.AddRectFilled(manaMin, innerMax, Tokens.Col.BarTrack);
                }

                if (manaFraction > 0f)
                {
                    float manaRight = MathF.Round(manaMin.X + ((innerMax.X - manaMin.X) * manaFraction));
                    dl.AddRectFilled(
                        manaMin,
                        new Vector2(manaRight, innerMax.Y),
                        Tokens.Col.Faded(Tokens.Col.Mana, cfg.BarOpacity));
                }
            }

            dl.AddRect(min, max, Tokens.Col.FrameEdge, 0f, ImDrawFlags.None, border);

            // Text last and clipped to the frame, so a long name runs out of room rather than
            // out of the frame and across whatever is beside it.
            dl.PushClipRect(innerMin, innerMax, true);
            this.DrawTexts(dl, cfg, textMode, i, ref member, innerMin, innerMax);
            dl.PopClipRect();
        }
    }

    private void DrawTexts(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        HealthTextMode textMode,
        int slot,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        float padding = Tokens.Metric.FramePadding;

        if (cfg.ShowName)
        {
            string name = this.DrawnName(slot, ref member, cfg.ShortenNames);
            float size = Tokens.Px(cfg.NameSize);
            Vector2 measured = new(Ink.MeasureWidth(size, name), size);
            Vector2 at = Anchors.Place(Anchors.At(cfg.NamePosition), innerMin, innerMax, measured, padding);

            at.X += Tokens.Px(cfg.NameX);
            at.Y += Tokens.Px(cfg.NameY);

            uint colour = cfg.NameInJobColour
                ? Jobs.Colour(member.JobId)
                : member.IsLocalPlayer ? Tokens.Col.GoldHi : Tokens.Col.Ink;

            Ink.DrawScaledShadowed(dl, size, at, colour, name);
        }

        if (!cfg.ShowHealthText)
        {
            return;
        }

        string health = this.HealthFigure(slot, ref member, textMode);
        if (health.Length == 0)
        {
            return;
        }

        float healthSize = Tokens.Px(cfg.HpTextSize);
        Vector2 healthMeasured = new(Ink.MeasureWidth(healthSize, health), healthSize);
        Vector2 healthAt = Anchors.Place(Anchors.At(cfg.HpTextPosition), innerMin, innerMax, healthMeasured, padding);

        healthAt.X += Tokens.Px(cfg.HpTextX);
        healthAt.Y += Tokens.Px(cfg.HpTextY);

        Ink.DrawScaledShadowed(dl, healthSize, healthAt, Tokens.Col.Ink, health);
    }

    /// <summary>Whether this member is one of the ones mana was switched on for.</summary>
    private static bool ShowsMana(Configuration.PartyFramesConfig cfg, ref PartyMemberSnapshot member)
    {
        if (!cfg.ShowMana || member.MaxMp == 0)
        {
            return false;
        }

        return member.Role switch
        {
            JobRole.Tank => cfg.ManaForTanks,
            JobRole.Healer => cfg.ManaForHealers,
            JobRole.Dps => cfg.ManaForDps,
            _ => false,
        };
    }

    /// <summary>
    /// What colour this member's bar is. A fixed colour has no colour to be yet — the picker
    /// arrives with the rest of the module — so it draws in the plain body tone rather than in
    /// an accent nobody chose.
    /// </summary>
    private static uint BarColour(BarColourMode mode, ref PartyMemberSnapshot member) => mode switch
    {
        BarColourMode.Job => Jobs.Colour(member.JobId),
        BarColourMode.Fixed => Tokens.Col.Ink,
        _ => Jobs.RoleColour(member.Role),
    };

    /// <summary>
    /// How full the bar is drawn. With smoothing off that is simply the health; with it on the
    /// drawn value walks towards the real one at a rate per second, so the movement takes the
    /// same time at 60 frames and at 144.
    /// <para>
    /// The walked value is kept up to date even while smoothing is off, so switching it on
    /// never starts with a slide from wherever the bar last was.
    /// </para>
    /// </summary>
    private float HealthFraction(int slot, ref PartyMemberSnapshot member, bool smooth, float delta)
    {
        float target = member.MaxHp > 0
            ? Math.Clamp(member.Hp / (float)member.MaxHp, 0f, 1f)
            : 0f;

        // A slot that changed hands holds a different person, not a health change: their bar
        // starts where they are rather than sliding out of the last member's value.
        if (!smooth || m_shownHealthFor[slot] != member.EntityId)
        {
            m_shownHealthFor[slot] = member.EntityId;
            m_shownHealth[slot] = target;
            return target;
        }

        float shown = m_shownHealth[slot] + ((target - m_shownHealth[slot]) * (1f - MathF.Exp(-delta * SmoothRate)));
        if (MathF.Abs(target - shown) < SmoothSettle)
        {
            shown = target;
        }

        m_shownHealth[slot] = shown;
        return shown;
    }

    private string HealthFigure(int slot, ref PartyMemberSnapshot member, HealthTextMode mode)
    {
        if (m_healthText[slot] is null
            || m_healthTextMode[slot] != (int)mode
            || m_healthTextHp[slot] != member.Hp
            || m_healthTextMaxHp[slot] != member.MaxHp)
        {
            m_healthTextMode[slot] = (int)mode;
            m_healthTextHp[slot] = member.Hp;
            m_healthTextMaxHp[slot] = member.MaxHp;
            m_healthText[slot] = Hud.HealthText.Build(mode, member.Hp, member.MaxHp);
        }

        return m_healthText[slot];
    }

    /// <summary>
    /// The name as it goes on the frame. Shortening builds a string, so it happens once per
    /// member rather than once per frame: the source string only changes when the slot changes
    /// hands, which makes it its own cache key.
    /// </summary>
    private string DrawnName(int slot, ref PartyMemberSnapshot member, bool shorten)
    {
        string source = member.Name ?? string.Empty;

        if (m_drawnName[slot] is null
            || m_drawnNameShort[slot] != shorten
            || !ReferenceEquals(m_drawnNameFrom[slot], source))
        {
            m_drawnNameFrom[slot] = source;
            m_drawnNameShort[slot] = shorten;
            m_drawnName[slot] = shorten ? Shorten(source) : source;
        }

        return m_drawnName[slot];
    }

    /// <summary>
    /// "Firstname Lastname" down to "Firstname L." — a character in this game has exactly the
    /// two names, and the first one is what people call them.
    /// </summary>
    private static string Shorten(string name)
    {
        int space = name.LastIndexOf(' ');
        if (space <= 0 || space >= name.Length - 1)
        {
            return name;
        }

        return string.Concat(name.AsSpan(0, space + 1), name.AsSpan(space + 1, 1), ".");
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
