using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
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

    /// <summary>
    /// How much wider the party number's tile is than the figure on it. Enough air that the
    /// digit is not touching an edge, little enough that the tile still reads as a badge and
    /// not as a button. The size the user sets is the figure, because that is the thing they
    /// are judging.
    /// </summary>
    private const float NumberPlateScale = 1.35f;

    /// <summary>
    /// How round the plate's corners are, as a share of its side. A share rather than a token
    /// because the plate follows the figure's size: a fixed radius is a soft square at eight
    /// pixels and a sharp one at forty.
    /// </summary>
    private const float NumberPlateRadius = 0.2f;

    /// <summary>
    /// How thick the plate's edge is, as a share of its side. A hairline is what a plate on a
    /// panel gets; a plate lying over the game needs a real edge to cut itself out of whatever
    /// is behind it, and at one pixel it stopped doing that (Florian, 2026-09-12).
    /// <para>
    /// It is drawn as a filled shape with the plate inside it, never as a stroke — see the
    /// note where it is used.
    /// </para>
    /// </summary>
    private const float NumberPlateEdge = 0.09f;

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
    private readonly NameShortening[] m_drawnNameMode = new NameShortening[PartySnapshot.Capacity];

    /// <summary>The window that takes the mouse, and the hit box of one frame inside it.</summary>
    private const string IdInput = "##wisp-pf-input";
    private const string IdSlot = "##wisp-pf-slot";

    /// <summary>
    /// Everything off: it carries no chrome, paints nothing, saves nothing, and never comes
    /// forward. The frames are still drawn into the background list — this window exists only
    /// so that ImGui asks for the mouse over them.
    /// </summary>
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

    /// <summary>The party number as text, built once for the eight numbers there can be.</summary>
    private static readonly string[] NumberText = { "1", "2", "3", "4", "5", "6", "7", "8" };

    /// <summary>Where a smoothed bar has got to, and who it belongs to.</summary>
    private readonly float[] m_shownHealth = new float[PartySnapshot.Capacity];
    private readonly uint[] m_shownHealthFor = new uint[PartySnapshot.Capacity];

    /// <summary>
    /// Each frame's inside, worked out in the first pass and read back in the second. The
    /// two passes exist so a name or an icon can sit outside its own frame without the next
    /// frame's ground being painted over it.
    /// </summary>
    private readonly Vector2[] m_innerMin = new Vector2[PartySnapshot.Capacity];
    private readonly Vector2[] m_innerMax = new Vector2[PartySnapshot.Capacity];
    private readonly bool[] m_hasInside = new bool[PartySnapshot.Capacity];

    /// <summary>
    /// Each frame's outside, for the mouse. The inside is where things are drawn; the edge is
    /// still part of the thing you are clicking on.
    /// </summary>
    private readonly Vector2[] m_frameMin = new Vector2[PartySnapshot.Capacity];
    private readonly Vector2[] m_frameMax = new Vector2[PartySnapshot.Capacity];

    /// <summary>
    /// Whether we were the ones who last said what the mouse is over. The game fills that
    /// field from its own hit test every frame, so ours only has to be taken back on the
    /// frame the mouse leaves.
    /// </summary>
    private bool m_heldMouseOver;

    /// <summary>Whether we told the action hook somebody was under the mouse.</summary>
    private bool m_pointedAt;

    /// <summary>
    /// The job icon per slot, resolved while collecting and only painted while drawing.
    /// Looking a texture up is asking Dalamud a question, and the draw path asks nothing.
    /// </summary>
    private readonly ImTextureID[] m_icon = new ImTextureID[PartySnapshot.Capacity];

    /// <summary>The leader's mark. One picture for the whole party, so it is resolved once.</summary>
    private ImTextureID m_leaderIcon;

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
            this.CollectIcons();
            return;
        }

        m_snapshot.Collect();
        this.CollectIcons();
        this.LogIfPartyChanged();
    }

    /// <summary>
    /// Picks up this frame's job icons. Dalamud only promises a texture for the frame it was
    /// asked in, so it is asked every frame — but here, once per member, and never from inside
    /// the loop that paints.
    /// </summary>
    private void CollectIcons()
    {
        m_leaderIcon = m_config.PartyFrames.ShowLeaderIcon ? Icons.Handle(Icons.PartyLeader) : default;

        if (!m_config.PartyFrames.ShowJobIcon)
        {
            return;
        }

        bool framed = m_config.PartyFrames.JobIconStyle == (int)JobIconStyle.Framed;
        PartyMemberSnapshot[] members = m_snapshot.Members;

        for (int i = 0; i < m_snapshot.Count; i++)
        {
            m_icon[i] = Icons.Handle(Jobs.IconId(members[i].JobId, framed));
        }
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
            m_hasInside[i] = innerMax.X > innerMin.X && innerMax.Y > innerMin.Y;
            if (!m_hasInside[i])
            {
                continue;
            }

            m_innerMin[i] = innerMin;
            m_innerMax[i] = innerMax;
            m_frameMin[i] = min;
            m_frameMax[i] = max;

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

        }

        // Between the two passes on purpose. The mouse decides which frame gets the ring, and
        // the ring belongs over the bars but under the icons and the text — a highlight that
        // covers the job icon hides the thing you were pointing at to read (Florian,
        // 2026-09-12). Asking the mouse here is what puts it in the middle of the stack.
        this.TakeTheMouse(dl, cfg, count);

        // The second pass. A name or an icon may be placed outside its own frame — above it,
        // beside it — and that is a layout people build on purpose, not a mistake to guard
        // against. Drawn in the same loop as the bars, anything hanging below a frame would
        // be painted over by the next frame's ground a moment later.
        float reach = Tokens.Px(Configuration.MaxTextOffset);

        for (int i = 0; i < count; i++)
        {
            if (!m_hasInside[i])
            {
                continue;
            }

            ref PartyMemberSnapshot member = ref members[i];
            Vector2 innerMin = m_innerMin[i];
            Vector2 innerMax = m_innerMax[i];

            // Clipped to the frame grown by the whole reach of the offset sliders: everything
            // that can be placed is drawn in full, and a name too long for even that is cut
            // rather than run across the screen. The icon goes down first — where the two are
            // set to overlap, the name is the one that has to stay readable.
            dl.PushClipRect(
                new Vector2(innerMin.X - reach, innerMin.Y - reach),
                new Vector2(innerMax.X + reach, innerMax.Y + reach),
                true);
            this.DrawJobIcon(dl, cfg, i, ref member, innerMin, innerMax);
            this.DrawLeaderIcon(dl, cfg, ref member, innerMin, innerMax);
            this.DrawTexts(dl, cfg, textMode, i, ref member, innerMin, innerMax);
            dl.PopClipRect();
        }
    }

    /// <summary>
    /// Lets the frames be clicked and pointed at.
    /// <para>
    /// This is the one place the module opens an ImGui window, and it is not for drawing — it
    /// paints nothing. Without a window ImGui never asks for the mouse, Dalamud passes the
    /// click through, and the game reads a click on empty screen as dropping your target. The
    /// frame and the world would fight over every click. A window is what makes the click ours.
    /// </para>
    /// <para>
    /// The honest cost: over the block the right button no longer turns the camera, because
    /// ImGui takes every button or none.
    /// </para>
    /// </summary>
    private void TakeTheMouse(ImDrawListPtr dl, Configuration.PartyFramesConfig cfg, int count)
    {
        // Nothing to take while the layout is being set against stand-ins: there is nobody to
        // select, and edit mode wants the same button for dragging.
        if (count == 0 || EditMode.IsActive || (!cfg.ClickToTarget && !cfg.MouseoverTarget && !cfg.ContextMenu))
        {
            this.ReleaseMouseOver();
            return;
        }

        Vector2 blockMin = m_frameMin[0];
        Vector2 blockMax = m_frameMax[0];

        for (int i = 1; i < count; i++)
        {
            if (!m_hasInside[i])
            {
                continue;
            }

            blockMin = Vector2.Min(blockMin, m_frameMin[i]);
            blockMax = Vector2.Max(blockMax, m_frameMax[i]);
        }

        ImGui.SetNextWindowPos(blockMin);
        ImGui.SetNextWindowSize(blockMax - blockMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        bool hitAnything = false;

        if (ImGui.Begin(IdInput, InputWindowFlags))
        {
            // The pointer over a frame stays the game's own, and stays whatever shape the game
            // chose. Without this Dalamud paints a Windows arrow over the block, which is a
            // different pointer from the one in the rest of the game (Florian, 2026-09-12).
            // No shape is asked for on purpose: a unit frame is a thing you point at, not a
            // button, and the game's own party list does not put a hand on one either.
            //
            // 🔴 The two allowances are not decoration. Pressing the button makes the frame
            // under it the active item, and a plain hover test answers no from that moment on
            // — so the claim dropped for exactly as long as the button was held and the
            // Windows pointer flashed back in its place, once per click (Florian, 2026-09-12).
            bool ours = ImGui.IsWindowHovered(
                ImGuiHoveredFlags.RootAndChildWindows
                | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem
                | ImGuiHoveredFlags.AllowWhenBlockedByPopup);

            PartyMemberSnapshot[] members = m_snapshot.Members;

            for (int i = 0; i < count; i++)
            {
                if (!m_hasInside[i])
                {
                    continue;
                }

                ImGui.SetCursorScreenPos(m_frameMin[i]);
                ImGui.PushID(i);
                // The button's own answer, which comes on release inside the frame and not on
                // press. That is what the game's party list does — you can put the button down
                // on the wrong person and slide off without selecting them — and it is why
                // this is the return value rather than IsItemClicked (Florian, 2026-09-12).
                //
                // The right button is asked for only when it has somewhere to go. It is taken
                // from the player either way — ImGui captures every button over the block, all
                // or none (spec §15) — but a button that is claimed and then handed nothing is
                // worse than one that was never claimed, and this way the flags say which it is.
                bool clicked = ImGui.InvisibleButton(
                    IdSlot,
                    m_frameMax[i] - m_frameMin[i],
                    cfg.ContextMenu
                        ? ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight
                        : ImGuiButtonFlags.MouseButtonLeft);
                bool hovered = ImGui.IsItemHovered();

                // Held down and dragged off the block is still our press. Without this the
                // pointer changes hands mid-drag, which is the same flash at the other end.
                ours |= ImGui.IsItemActive();
                ImGui.PopID();

                if (!hovered && !clicked)
                {
                    continue;
                }

                hitAnything = true;

                // Drawn now rather than in the pass above, because only here is it known which
                // frame the mouse is on. The background list is the same one the frames went
                // into, so appending puts the ring on top of its own frame — and the input
                // window paints nothing, so there is nothing of it to draw over.
                // Drawn AROUND the frame, not on it. On it, a three pixel ring sat squarely on
                // the mana strip, which lives against the bottom inside edge (Florian,
                // 2026-09-12). Outside, there is nothing of ours for it to cover, whatever the
                // frame is carrying and wherever the badges were put.
                if (hovered && cfg.HighlightHovered)
                {
                    float ring = Tokens.Metric.FrameHoverRing;
                    Ring(
                        dl,
                        new Vector2(m_frameMin[i].X - ring, m_frameMin[i].Y - ring),
                        new Vector2(m_frameMax[i].X + ring, m_frameMax[i].Y + ring),
                        ring,
                        Tokens.Col.FrameHover);
                }

                // The game puts the pointing hand over its own party list, so ours wears it
                // too (Florian, 2026-09-12). NativeUi turns it into the game's own shape at
                // the end of the frame; asking for it here is only saying what this is.
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                // Only now is the game object worth looking up. Finding one walks the object
                // table, so it happens for the one member under the cursor and never for all
                // eight of them (spec 12.5).
                IGameObject? target = Services.Objects.SearchByEntityId(members[i].EntityId);
                if (target is null)
                {
                    // Out of range, so the game has not loaded them. The same thing happens in
                    // the game's own party list; it is not an error and not worth a log line.
                    continue;
                }

                if (clicked)
                {
                    // Which button it was, asked of the frame the button answered on. A button
                    // set to answer on release reports in the very frame the release happens,
                    // so the release that is still fresh this frame is the one that did it.
                    // Right is asked first: it is only ever claimed when it has a menu to open,
                    // so anything else that got through is the left one.
                    if (cfg.ContextMenu && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    {
                        NativeUi.OpenContextMenuFor(target.Address);
                    }
                    else if (cfg.ClickToTarget)
                    {
                        Services.Targets.Target = target;
                    }
                }

                if (!hovered)
                {
                    continue;
                }

                // Said whether or not the game is told as well: the two are separate features
                // and the hook is only in place when the player asked for it.
                MouseoverCasting.PointAt(target.GameObjectId, target.Address);
                m_pointedAt = true;

                if (cfg.MouseoverTarget)
                {
                    Services.Targets.MouseOverTarget = target;
                    m_heldMouseOver = true;
                }
            }

            if (ours)
            {
                NativeUi.KeepGameCursor();
            }
        }

        ImGui.End();
        ImGui.PopStyleVar();

        if (!hitAnything)
        {
            this.ReleaseMouseOver();
        }
    }

    /// <summary>
    /// A rectangle drawn as four filled bars rather than as a stroke. ImGui centres a stroke
    /// on its path, so half of it falls outside the rectangle and is antialiased — the same
    /// reason the window's rings and the party number's edge are filled shapes.
    /// </summary>
    private static void Ring(ImDrawListPtr dl, Vector2 min, Vector2 max, float thickness, uint colour)
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
    /// Says the mouse is over nobody, and hands the game's mouseover back if we were the ones
    /// holding it. Both have to happen on the frame the mouse leaves: a stale answer here
    /// sends the next action to somebody the player stopped pointing at.
    /// </summary>
    private void ReleaseMouseOver()
    {
        if (m_pointedAt)
        {
            m_pointedAt = false;
            MouseoverCasting.PointAt(0ul, 0);
        }

        if (!m_heldMouseOver)
        {
            return;
        }

        m_heldMouseOver = false;
        Services.Targets.MouseOverTarget = null;
    }

    /// <summary>
    /// The job icon, hung on one of the nine points like everything else on a frame. It is
    /// square: a job icon is drawn square, and letting it be stretched would only offer a way
    /// to make it wrong.
    /// </summary>
    private void DrawJobIcon(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        int slot,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (!cfg.ShowJobIcon || (cfg.JobIconHideDps && member.Role == JobRole.Dps))
        {
            return;
        }

        DrawIcon(dl, m_icon[slot], cfg.JobIconSize, cfg.JobIconPosition, cfg.JobIconX, cfg.JobIconY, innerMin, innerMax);
    }

    /// <summary>The leader's mark, on whoever leads. Same anatomy, same placement.</summary>
    private void DrawLeaderIcon(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (!cfg.ShowLeaderIcon || !member.IsLeader)
        {
            return;
        }

        DrawIcon(dl, m_leaderIcon, cfg.LeaderIconSize, cfg.LeaderIconPosition, cfg.LeaderIconX, cfg.LeaderIconY, innerMin, innerMax);
    }

    /// <summary>
    /// One picture on a frame, square and hung on one of the nine points. Written once because
    /// every icon a frame will ever carry — job, leader, raid marker — is placed the same way,
    /// and a second copy of this is a second place to fix a rounding.
    /// </summary>
    private static void DrawIcon(
        ImDrawListPtr dl,
        ImTextureID icon,
        float size,
        int anchor,
        float offsetX,
        float offsetY,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (icon.Handle == 0)
        {
            // Not loaded yet, or an id the game has nothing for. Nothing is drawn rather than
            // a grey square where a picture is meant to be.
            return;
        }

        float side = Tokens.Px(size);
        Vector2 at = Anchors.Place(
            Anchors.At(anchor),
            innerMin,
            innerMax,
            new Vector2(side, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.Px(offsetX);
        at.Y += Tokens.Px(offsetY);

        dl.AddImage(icon, at, new Vector2(at.X + side, at.Y + side));
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
            string name = this.DrawnName(slot, ref member, PlayerName.At(cfg.NameShortening));
            float size = Tokens.Px(cfg.NameSize);
            Vector2 measured = new(Ink.MeasureWidth(size, name), size);
            Vector2 at = Anchors.Place(Anchors.At(cfg.NamePosition), innerMin, innerMax, measured, padding);

            at.X += Tokens.Px(cfg.NameX);
            at.Y += Tokens.Px(cfg.NameY);

            // Your own name is drawn like everyone else's. It used to come out gold, which
            // looked like a state rather than a whose-name-is-this, and the one frame you
            // never have to search for is your own (Florian, 2026-09-12).
            uint colour = cfg.NameInJobColour ? Jobs.Colour(member.JobId) : Tokens.Col.HudInk;

            Ink.DrawScaledEdged(dl, size, at, colour, name, cfg.Edge);
        }

        if (cfg.ShowPartyNumber && member.PartyNumber >= 1 && member.PartyNumber <= NumberText.Length)
        {
            // On a rounded plate with a black edge, the way the game's own party list puts a
            // position. A bare digit over a health bar has no shape of its own to be
            // recognised by and reads as a stray number (Florian).
            string number = NumberText[member.PartyNumber - 1];
            float glyph = Tokens.Px(cfg.PartyNumberSize);
            float plate = MathF.Round(glyph * NumberPlateScale);
            Vector2 plateAt = Anchors.Place(
                Anchors.At(cfg.PartyNumberPosition),
                innerMin,
                innerMax,
                new Vector2(plate, plate),
                padding);

            plateAt.X += Tokens.Px(cfg.PartyNumberX);
            plateAt.Y += Tokens.Px(cfg.PartyNumberY);

            // The edge is a filled shape with the plate laid inside it, NOT a stroke. ImGui
            // centres a stroke on its path, so half of every edge pixel falls outside the
            // rectangle and gets antialiased — which is exactly why the window's own rings are
            // filled rectangles too (design bible §2.1, learned the same way in session 5).
            Vector2 plateEnd = new(plateAt.X + plate, plateAt.Y + plate);
            float edge = MathF.Max(Tokens.Line(1f), MathF.Round(plate * NumberPlateEdge));
            float radius = MathF.Max(Tokens.Radius.Small, MathF.Round(plate * NumberPlateRadius));

            dl.AddRectFilled(plateAt, plateEnd, Tokens.Col.NumberEdge, radius, ImDrawFlags.RoundCornersAll);
            dl.AddRectFilled(
                new Vector2(plateAt.X + edge, plateAt.Y + edge),
                new Vector2(plateEnd.X - edge, plateEnd.Y - edge),
                Tokens.Col.NumberPlate,
                MathF.Max(0f, radius - edge),
                ImDrawFlags.RoundCornersAll);

            // Centred on the plate rather than anchored to it: a digit is the one text whose
            // width changes with nothing the user did, and it has to stay in the middle.
            // Written once. A second pass a pixel away thickened the stroke and smeared the
            // glyph with it, which is the opposite of what a number this small needs
            // (Florian, 2026-09-12) — weight is what the size slider is for.
            float glyphWidth = Ink.MeasureWidth(glyph, number);
            Ink.DrawScaled(
                dl,
                glyph,
                new Vector2(
                    MathF.Round(plateAt.X + ((plate - glyphWidth) * 0.5f)),
                    MathF.Round(plateAt.Y + ((plate - glyph) * 0.5f))),
                Tokens.Col.NumberInk,
                number);
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

        Ink.DrawScaledEdged(dl, healthSize, healthAt, Tokens.Col.HudInk, health, cfg.Edge);
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
    private string DrawnName(int slot, ref PartyMemberSnapshot member, NameShortening mode)
    {
        string source = member.Name ?? string.Empty;

        if (m_drawnName[slot] is null
            || m_drawnNameMode[slot] != mode
            || !ReferenceEquals(m_drawnNameFrom[slot], source))
        {
            m_drawnNameFrom[slot] = source;
            m_drawnNameMode[slot] = mode;
            m_drawnName[slot] = PlayerName.Build(mode, source);
        }

        return m_drawnName[slot];
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
