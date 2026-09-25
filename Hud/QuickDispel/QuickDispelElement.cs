using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using WispUI.Core;
using WispUI.Data;
using WispUI.Hud.PartyFrames;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.QuickDispel;

/// <summary>How the squares are laid out. Stored as its number.</summary>
internal enum DispelLayout
{
    /// <summary>One row of up to eight.</summary>
    Row = 0,

    /// <summary>Four across, as many rows as the party needs.</summary>
    Grid = 1,
}

/// <summary>
/// Quick Dispel: one small square per party member, which lights up in the cleanse colour
/// while that member carries something the player's cleanse takes off, and casts it on them
/// when clicked (Florian, 2026-09-22, after a WoW addon's behaviour — only the behaviour).
/// <para>
/// It works without the party frames, and that is the point of it: somebody who keeps the
/// game's own party list gets the one thing that list never says loudly enough.
/// </para>
/// <para>
/// 🔴 It reads the same live snapshot as the party frames (see
/// <see cref="PartySnapshot.Refresh"/>). With both on, the party is still read once a frame.
/// </para>
/// </summary>
internal sealed class QuickDispelElement : HudElement
{
    private const string IdInput = "##wisp-qd-input";
    private const string IdSquare = "##wisp-qd-square";
    private const string IdHandle = "##wisp-qd-handle";

    /// <summary>Everything off: it paints nothing and only asks ImGui for the mouse over the squares.</summary>
    private const ImGuiWindowFlags InputWindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoNavFocus
        | ImGuiWindowFlags.NoScrollWithMouse;

    /// <summary>"1" to "8", built once so a square never formats its own number.</summary>
    private static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6", "7", "8" };

    private readonly Configuration m_config;

    /// <summary>The live party, shared with the party frames.</summary>
    private readonly PartySnapshot m_snapshot;

    /// <summary>Stand-ins for the settings window's preview. Never the live one.</summary>
    private readonly PartySnapshot m_preview = new();

    /// <summary>Where each square went this frame, for the mouse and for edit mode.</summary>
    private readonly Vector2[] m_min = new Vector2[PartySnapshot.Capacity];
    private readonly Vector2[] m_max = new Vector2[PartySnapshot.Capacity];

    /// <summary>What each square is saying this frame, worked out once in Collect.</summary>
    private readonly bool[] m_lit = new bool[PartySnapshot.Capacity];
    private readonly float[] m_left = new float[PartySnapshot.Capacity];

    /// <summary>The same for the preview's stand-ins, kept apart from the live squares.</summary>
    private readonly bool[] m_previewLit = new bool[PartySnapshot.Capacity];
    private readonly float[] m_previewLeft = new float[PartySnapshot.Capacity];

    /// <summary>How many squares went down this frame. Zero when the module showed nothing.</summary>
    private int m_drawn;

    /// <summary>Whether the pointer was over the squares last frame — the Alt handle waits for it.</summary>
    private bool m_hovered;

    /// <summary>The Alt handle being dragged: where it was grabbed, and where the squares were then.</summary>
    private bool m_dragging;
    private Vector2 m_grabbedAt;
    private Vector2 m_grabbedFrom;

    public QuickDispelElement(Configuration config, PartySnapshot party)
    {
        m_config = config;
        m_snapshot = party;
    }

    public override string Name => Strings.NavQuickDispel;

    public override bool Enabled => m_config.QuickDispelEnabled;

    public override bool Movable => true;

    /// <summary>
    /// Nothing to do at all on a job that cannot cleanse, when the player asked for that —
    /// decided before the party is read, so a Warrior pays one field read for this module.
    /// </summary>
    public override bool HasAnythingToDraw
    {
        get
        {
            if (EditMode.IsActive || !m_config.QuickDispel.OnlyWhenAble)
            {
                return true;
            }

            uint job = NativeUi.LocalJob(out int level);
            return StatusData.CanCleanse(job, level);
        }
    }

    public override void Bounds(out Vector2 min, out Vector2 max)
    {
        if (m_drawn == 0)
        {
            min = default;
            max = default;
            return;
        }

        min = m_min[0];
        max = m_max[0];

        for (int i = 1; i < m_drawn; i++)
        {
            min = Vector2.Min(min, m_min[i]);
            max = Vector2.Max(max, m_max[i]);
        }
    }

    /// <summary>A screen position, stored as it arrives — a HUD position is a screen position.</summary>
    public override void MoveTo(Vector2 topLeft)
    {
        m_config.QuickDispel.PositionX = MathF.Round(topLeft.X);
        m_config.QuickDispel.PositionY = MathF.Round(topLeft.Y);
        m_config.MarkDirty();
    }

    public override void Collect()
    {
        m_snapshot.Refresh(m_config.PartyFrames.OwnBuffsOnly);
        Read(m_snapshot, m_lit, m_left);
    }

    public override void Draw(ImDrawListPtr dl)
    {
        Configuration.QuickDispelConfig cfg = m_config.QuickDispel;
        int count = m_snapshot.Count;

        // Nobody needs one and the player asked not to be shown an empty row. Edit mode
        // always shows it: a row that is not there cannot be put anywhere.
        if (cfg.HideWhenClear && !EditMode.IsActive && !AnyLit(m_lit, count))
        {
            m_drawn = 0;
            m_hovered = false;
            m_dragging = false;
            return;
        }

        Vector2 origin = new(MathF.Round(cfg.PositionX), MathF.Round(cfg.PositionY));
        m_drawn = this.Paint(dl, m_snapshot, m_lit, m_left, origin);

        // Edit mode moves the squares by their outside, like every element; clicking one
        // there would cast a spell on a stand-in.
        if (!EditMode.IsActive && m_drawn > 0)
        {
            this.Input(dl);
        }
        else
        {
            m_hovered = false;
            m_dragging = false;
        }
    }

    // --- preview ---------------------------------------------------------------

    public override bool HasPreview => true;

    /// <summary>Always a full party: the layout choice is only visible with eight squares.</summary>
    public override Vector2 PreviewSize(int count)
    {
        Layout(PartySnapshot.Capacity, out int columns, out int rows);
        float size = Tokens.WorldPx(m_config.QuickDispel.SquareSize);
        float gap = Tokens.Metric.DispelGap;

        return new Vector2((columns * size) + ((columns - 1) * gap), (rows * size) + ((rows - 1) * gap));
    }

    public override void DrawPreview(ImDrawListPtr dl, Vector2 origin, int count)
    {
        // Stand-ins carrying stand-in afflictions, so the lit square and its seconds can be
        // judged without a fight. Their own arrays: the live ones belong to the live squares.
        m_preview.FillPlaceholders(PartySnapshot.Capacity, withAuras: true);
        Read(m_preview, m_previewLit, m_previewLeft);
        this.Paint(dl, m_preview, m_previewLit, m_previewLeft, origin, preview: true);
    }

    // --- reading ---------------------------------------------------------------

    /// <summary>
    /// Which members are carrying something the player's cleanse takes off, and the least
    /// time any of it has left — the one that will go wrong first is the one to read.
    /// </summary>
    private static void Read(PartySnapshot snapshot, bool[] lit, float[] left)
    {
        PartyMemberSnapshot[] members = snapshot.Members;

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref PartyMemberSnapshot member = ref members[i];

            // Nothing to take off a dead member: the square would light and the click would
            // do nothing, which reads as a broken button.
            lit[i] = member.HasDispellable && member.Hp > 0;
            left[i] = 0f;

            if (!lit[i])
            {
                continue;
            }

            ReadOnlySpan<AuraSnapshot> auras = snapshot.Auras(i);
            float soonest = float.MaxValue;

            for (int a = 0; a < auras.Length; a++)
            {
                if (auras[a].CanDispel && auras[a].Remaining > 0f && auras[a].Remaining < soonest)
                {
                    soonest = auras[a].Remaining;
                }
            }

            left[i] = soonest < float.MaxValue ? soonest : 0f;
        }
    }

    private static bool AnyLit(bool[] lit, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (lit[i])
            {
                return true;
            }
        }

        return false;
    }

    // --- drawing ---------------------------------------------------------------

    /// <summary>Columns and rows for this many squares in the chosen layout.</summary>
    private void Layout(int count, out int columns, out int rows)
    {
        int across = m_config.QuickDispel.Layout == (int)DispelLayout.Grid ? 4 : PartySnapshot.Capacity;
        columns = Math.Clamp(count, 1, across);
        rows = (Math.Max(count, 1) + columns - 1) / columns;
    }

    /// <summary>
    /// Paints the squares from a snapshot and returns how many went down. The same code for
    /// the world and for the preview — a preview drawn by other code can disagree with the
    /// game exactly when somebody is relying on it.
    /// </summary>
    private int Paint(
        ImDrawListPtr dl,
        PartySnapshot snapshot,
        bool[] lit,
        float[] left,
        Vector2 origin,
        bool preview = false)
    {
        Configuration.QuickDispelConfig cfg = m_config.QuickDispel;
        int count = snapshot.Count;
        Layout(count, out int columns, out _);

        float size = Tokens.WorldPx(cfg.SquareSize);
        float gap = Tokens.Metric.DispelGap;
        float edge = Tokens.Metric.DispelEdge;
        float textSize = Tokens.WorldPx(cfg.SquareSize * Tokens.Metric.DispelNumberShare);
        uint cleanse = m_config.PartyFrames.CleanseColour | 0xFF000000u;
        PartyMemberSnapshot[] members = snapshot.Members;

        for (int i = 0; i < count; i++)
        {
            ref PartyMemberSnapshot member = ref members[i];

            Vector2 min = new(
                origin.X + ((i % columns) * (size + gap)),
                origin.Y + ((i / columns) * (size + gap)));
            Vector2 max = new(min.X + size, min.Y + size);

            if (!preview)
            {
                m_min[i] = min;
                m_max[i] = max;
            }

            // Out of reach, elsewhere or offline: the square stays, faint, so the row keeps
            // its shape and nobody's square moves when somebody walks off.
            float alpha = member.Presence == PartyPresence.Here ? 1f : Tokens.Metric.DispelAwayAlpha;

            dl.AddRectFilled(min, max, Tokens.Col.Faded(lit[i] ? cleanse : Tokens.Col.FrameBg, alpha));
            PartyFramesElement.Ring(dl, min, max, edge, Tokens.Col.Faded(Jobs.Colour(member.JobId), alpha));

            // Lit: the seconds on the one that runs out first. Otherwise the party number, if
            // asked for. Never both — two figures in a square this small is neither readable.
            string? text = lit[i] ? PartyFramesElement.DurationText(left[i]) : null;

            if (text is null && cfg.ShowPartyNumber && member.PartyNumber >= 1 && member.PartyNumber <= Numbers.Length)
            {
                text = Numbers[member.PartyNumber - 1];
            }

            if (text is null)
            {
                continue;
            }

            float width = Ink.MeasureWidth(textSize, text);
            Vector2 at = new(
                MathF.Round(((min.X + max.X) * 0.5f) - (width * 0.5f)),
                MathF.Round(Ink.DigitTop(textSize, (min.Y + max.Y) * 0.5f)));

            // Outlined whatever the suite's lettering is set to: the text sits on a job colour
            // edge or on the cleanse colour, and either can be light.
            Ink.DrawScaledEdged(dl, textSize, at, Tokens.Col.Faded(Tokens.Col.HudInk, alpha), text, TextEdge.Outline);
        }

        return count;
    }

    // --- the mouse ---------------------------------------------------------------

    /// <summary>
    /// Clicks, the hover ring, and the Alt handle. One invisible window over the squares —
    /// without a window ImGui never asks for the mouse and the click goes to the world
    /// behind (party frames spec §14).
    /// </summary>
    private void Input(ImDrawListPtr dl)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        this.Bounds(out Vector2 blockMin, out Vector2 blockMax);

        // The handle is offered while Alt is held over the squares, and stays for as long as
        // it is being dragged — letting go of Alt mid-drag must not drop the squares.
        bool handle = m_dragging || (io.KeyAlt && m_hovered);
        float dot = Tokens.WorldPx(m_config.QuickDispel.SquareSize * Tokens.Metric.DispelHandleShare);
        Vector2 dotMin = new(blockMin.X, blockMin.Y - Tokens.Metric.DispelHandleGap - dot);
        Vector2 windowMin = handle ? new Vector2(blockMin.X, dotMin.Y) : blockMin;

        ImGui.SetNextWindowPos(windowMin);
        ImGui.SetNextWindowSize(blockMax - windowMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        bool ours = false;

        if (ImGui.Begin(IdInput, InputWindowFlags))
        {
            // Same allowances as the frames: a held button makes the square the active item,
            // and a plain hover test would drop the game's pointer for exactly that long.
            ours = ImGui.IsWindowHovered(
                ImGuiHoveredFlags.RootAndChildWindows
                | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem
                | ImGuiHoveredFlags.AllowWhenBlockedByPopup);

            if (handle)
            {
                ours |= this.Handle(dl, io, dotMin, dot, blockMin);
            }

            PartyMemberSnapshot[] members = m_snapshot.Members;

            for (int i = 0; i < m_drawn; i++)
            {
                ImGui.SetCursorScreenPos(m_min[i]);
                ImGui.PushID(i);

                // On release inside the square, like the game's own party list: a press can
                // slide off without casting.
                bool clicked = ImGui.InvisibleButton(IdSquare, m_max[i] - m_min[i], ImGuiButtonFlags.MouseButtonLeft);
                bool hovered = ImGui.IsItemHovered();
                ours |= ImGui.IsItemActive();
                ImGui.PopID();

                if (!hovered)
                {
                    continue;
                }

                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                float ring = Tokens.Metric.DispelHoverRing;
                PartyFramesElement.Ring(
                    dl,
                    new Vector2(m_min[i].X - ring, m_min[i].Y - ring),
                    new Vector2(m_max[i].X + ring, m_max[i].Y + ring),
                    ring,
                    Tokens.Col.Softer(Tokens.Col.FrameHover, Tokens.Metric.DispelHoverAlpha));

                // Always, lit or not (Florian, 2026-09-25): the light says who needs it, the
                // square is simply that person. A click that sometimes does nothing reads as a
                // missed click — and the square is also the quick way to put a cleanse on
                // yourself without dropping your target.
                if (clicked)
                {
                    Cleanse(ref members[i]);
                }
            }

            if (ours)
            {
                NativeUi.KeepGameCursor();
            }
        }

        ImGui.End();
        ImGui.PopStyleVar();

        // The whole block, gaps and handle included: the handle must not blink out while the
        // pointer crosses the air between two squares on its way to it.
        m_hovered = ours || m_dragging;
    }

    /// <summary>
    /// The small square above the first one. Dragging it moves the squares without edit mode —
    /// freely, with no pull to the screen's middle: that is edit mode's job, and this is the
    /// quick way (Florian, 2026-09-22).
    /// </summary>
    private bool Handle(ImDrawListPtr dl, ImGuiIOPtr io, Vector2 dotMin, float dot, Vector2 blockMin)
    {
        ImGui.SetCursorScreenPos(dotMin);
        ImGui.InvisibleButton(IdHandle, new Vector2(dot, dot));
        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();

        if (ImGui.IsItemActivated())
        {
            m_dragging = true;
            m_grabbedAt = io.MousePos;
            m_grabbedFrom = blockMin;
        }

        if (active)
        {
            this.MoveTo(m_grabbedFrom + (io.MousePos - m_grabbedAt));
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        }
        else
        {
            m_dragging = false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        }

        // A small square in the squares' own ground, no edge — one of them, only smaller, so it
        // reads as belonging to the row. Brighter under the pointer, which is all it says.
        uint ground = hovered || active
            ? Tokens.Col.Mix(Tokens.Col.FrameBg, Tokens.Col.HudInk, Tokens.Metric.DispelHandleLift)
            : Tokens.Col.FrameBg;
        dl.AddRectFilled(dotMin, new Vector2(dotMin.X + dot, dotMin.Y + dot), ground);

        return hovered || active;
    }

    /// <summary>
    /// Casts the player's own cleanse on this member, through the same call a hotbar makes.
    /// The game decides whether it can go — range, level, line of sight — and says nothing
    /// when it cannot, exactly as a binding does.
    /// </summary>
    private static void Cleanse(ref PartyMemberSnapshot member)
    {
        uint job = NativeUi.LocalJob(out _);
        uint action = StatusData.CleanseAction(job, out bool onSelf);

        if (action == 0u)
        {
            return;
        }

        // Found now, on a click, and never kept: an object's address must not outlive the
        // frame it was read in (audit 2026-09-20).
        uint entityId = onSelf ? Services.Objects.LocalPlayer?.EntityId ?? 0u : member.EntityId;
        IGameObject? target = entityId != 0u ? Services.Objects.SearchByEntityId(entityId) : null;

        if (target is null)
        {
            return;
        }

        ActionUse.On(action, target.GameObjectId, target.Address);
    }
}
