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
    /// How strongly the frame being drawn right now is faded. One for a member who is there,
    /// less for one the game has stopped reporting.
    /// <para>
    /// Frame-local drawing state rather than a setting, which is why it lives here and not in
    /// the configuration: it is set at the top of each member and read by everything that
    /// paints part of that member.
    /// </para>
    /// </summary>
    private float m_dim = 1f;

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

    public override bool Movable => true;

    /// <summary>
    /// The block all the frames together occupy, worked out from what was actually drawn.
    /// <para>
    /// From the drawn rectangles rather than recalculated from the settings: the two would
    /// have to be kept in step by hand, and the one that matters is the one on screen.
    /// </para>
    /// </summary>
    public override void Bounds(out Vector2 min, out Vector2 max)
    {
        min = default;
        max = default;
        bool any = false;

        for (int i = 0; i < m_snapshot.Count; i++)
        {
            if (!m_hasInside[i])
            {
                continue;
            }

            if (!any)
            {
                min = m_frameMin[i];
                max = m_frameMax[i];
                any = true;
                continue;
            }

            min = Vector2.Min(min, m_frameMin[i]);
            max = Vector2.Max(max, m_frameMax[i]);
        }
    }

    /// <summary>
    /// Takes a screen position and stores it the way the layout does — unscaled, so the
    /// arrangement is the same shape at any interface scale.
    /// </summary>
    public override void MoveTo(Vector2 topLeft)
    {
        float scale = Tokens.Scale <= 0f ? 1f : Tokens.Scale;

        m_config.PartyFrames.PositionX = MathF.Round(topLeft.X / scale);
        m_config.PartyFrames.PositionY = MathF.Round(topLeft.Y / scale);
        m_config.MarkDirty();
    }

    public override void Collect()
    {
        // 🔴 The preview brings a full party of its own, and does NOT go through edit mode to
        // get one. Edit mode closes the settings window on purpose — you are dragging the
        // things it covers — which is exactly wrong for a tab whose every control needs to be
        // watched while it is moved (Florian, 2026-09-13: the icons were never visible,
        // because turning on the thing that showed eight frames took the window away).
        if (EditMode.IsActive || AuraPreview.Active)
        {
            m_snapshot.FillPlaceholders();
            this.CollectIcons();
            return;
        }

        m_snapshot.Collect(m_config.PartyFrames.OwnBuffsOnly);
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

    /// <summary>
    /// One frame of party frames, with the game's own right-click menu left uncovered.
    /// <para>
    /// 🔴 Nothing WispUI draws can go behind a game window: ImGui renders after the entire
    /// game interface. So the menu is allowed to open where the game puts it — at the pointer,
    /// the way its own party list does — and the frames simply leave that patch unpainted.
    /// </para>
    /// <para>
    /// A clip cannot cut a hole, so the screen is cut into the strips around the menu and the
    /// frames are drawn once per strip. The hole is set a little inside the menu's own window
    /// (see <see cref="NativeUi.ContextMenuBounds"/>), because the window is larger than the
    /// panel it paints and cutting to its full size left a gap that read as a border.
    /// </para>
    /// </summary>
    public override void Draw(ImDrawListPtr dl)
    {
        if (!NativeUi.ContextMenuBounds(out Vector2 menuMin, out Vector2 menuMax))
        {
            this.DrawContent(dl);
            return;
        }

        Vector2 screen = ImGui.GetIO().DisplaySize;
        Span<Vector4> strips = stackalloc Vector4[4];
        int stripCount = 0;

        if (menuMin.Y > 0f)
        {
            strips[stripCount++] = new Vector4(0f, 0f, screen.X, menuMin.Y);
        }

        if (menuMax.Y < screen.Y)
        {
            strips[stripCount++] = new Vector4(0f, menuMax.Y, screen.X, screen.Y);
        }

        if (menuMin.X > 0f)
        {
            strips[stripCount++] = new Vector4(0f, menuMin.Y, menuMin.X, menuMax.Y);
        }

        if (menuMax.X < screen.X)
        {
            strips[stripCount++] = new Vector4(menuMax.X, menuMin.Y, screen.X, menuMax.Y);
        }

        for (int i = 0; i < stripCount; i++)
        {
            Vector4 strip = strips[i];
            dl.PushClipRect(new Vector2(strip.X, strip.Y), new Vector2(strip.Z, strip.W), true);

            // No special case for the mouse: while a menu is up the frames have already let go
            // of it, so nothing in here asks ImGui for anything that could be counted twice.
            this.DrawContent(dl);
            dl.PopClipRect();
        }
    }

    private void DrawContent(ImDrawListPtr dl)
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
        var mark = (CleanseMark)cfg.CleanseMark;

        // The mark is an instruction. On a job that cannot carry it out it is noise, so it is
        // off there by default — the icons still show the effect either way. The preview
        // ignores this, or setting it up on the wrong job would show nothing.
        if (!AuraPreview.Active && cfg.CleanseOnlyWhenAble && !CanCleanseNow())
        {
            mark = CleanseMark.None;
        }

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

            // 🔴 One factor for the whole frame, set here and read by everything that draws
            // part of it. Dimming only the bar left a frame whose name, icons and number were
            // as loud as everybody else's, so it did not read as stepped back at all
            // (Florian, 2026-09-12).
            m_dim = member.Presence switch
            {
                PartyPresence.Here => 1f,
                PartyPresence.Offline => Tokens.Metric.OfflineDim,
                _ => Tokens.Metric.OutOfRangeDim,
            };

            dl.AddRectFilled(min, max, this.Dim(Tokens.Col.FrameBg));

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

            // Dimmed rather than recoloured, so the frame is still recognisably that
            // person's job at a glance.
            uint barColour = mark == CleanseMark.Bar && member.HasDispellable
                ? cfg.CleanseColour
                : BarColour(colourMode, ref member);

            uint colour = this.Dim(Tokens.Col.Faded(barColour, cfg.BarOpacity));
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
                    dl.AddRectFilled(manaMin, innerMax, this.Dim(Tokens.Col.BarTrack));
                }

                if (manaFraction > 0f)
                {
                    float manaRight = MathF.Round(manaMin.X + ((innerMax.X - manaMin.X) * manaFraction));
                    dl.AddRectFilled(
                        manaMin,
                        new Vector2(manaRight, innerMax.Y),
                        this.Dim(Tokens.Col.Faded(Tokens.Col.Mana, cfg.BarOpacity)));
                }
            }

            dl.AddRect(min, max, this.Dim(Tokens.Col.FrameEdge), 0f, ImDrawFlags.None, border);
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
            // rather than run across the screen.
            dl.PushClipRect(
                new Vector2(innerMin.X - reach, innerMin.Y - reach),
                new Vector2(innerMax.X + reach, innerMax.Y + reach),
                true);

            // 🔴 Three layers, and the order between them is the answer to "which of these two
            // has to stay readable".
            //
            // The badges go under the writing: a job icon is recognised from its shape and
            // colour, and half of one is still that job, while half a name is not a name.
            this.DrawJobIcon(dl, cfg, i, ref member, innerMin, innerMax);
            this.DrawLeaderIcon(dl, cfg, ref member, innerMin, innerMax);

            this.DrawTexts(dl, cfg, textMode, i, ref member, innerMin, innerMax);

            // The status pictures go OVER the writing. They are the newest thing on the frame
            // and the thing being looked for; a name is read once and then known, so a name
            // crossing them is the one that gives way (Florian, 2026-09-13).
            this.DrawAuras(dl, cfg, i, innerMin, innerMax);
            this.DrawRescue(dl, cfg, ref member, innerMin, innerMax);

            // Over everything, and outside the frame rather than on its edge. On the edge a
            // thick mark eats into the bar it is meant to be framing, and under the second
            // pass the next frame's ground painted across it (Florian, 2026-09-13: it must
            // not sit behind the frame).
            if (mark == CleanseMark.Border && member.HasDispellable)
            {
                this.DrawCleanseMark(dl, cfg, m_frameMin[i], m_frameMax[i]);
            }

            DrawPresenceNote(dl, cfg, ref member, innerMin, innerMax);
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
        if (count == 0
            || EditMode.IsActive
            || (cfg.Bindings.For(LocalJobId()).Count == 0 && !cfg.MouseoverTarget && !cfg.HighlightHovered))
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


        // 🔴 And while one is up, the frames let go of the mouse. This window takes every
        // button over itself, so a menu that overlaps it at all was on screen and unclickable
        // — the click went to the frame underneath instead (Florian, 2026-09-12). For those
        // few seconds the mouse belongs to the menu, which is the only thing it can sensibly
        // belong to.
        if (NativeUi.ContextMenuOpen())
        {
            this.ReleaseMouseOver();
            return;
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
                // Every button the bindings could want, which is all of them: ImGui takes them
                // over this window whatever is asked for here (spec §15), so claiming fewer
                // would only mean a button that is taken from the player and handed nothing.
                bool clicked = ImGui.InvisibleButton(
                    IdSlot,
                    m_frameMax[i] - m_frameMin[i],
                    ImGuiButtonFlags.MouseButtonLeft
                    | ImGuiButtonFlags.MouseButtonRight
                    | ImGuiButtonFlags.MouseButtonMiddle);
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

                // The bindings, asked of the frame a release happened on. A button set to
                // answer on release reports in the very frame of that release, so whichever
                // button is fresh right now is the one that did it. The two side buttons never
                // reach the invisible button at all — ImGui has no flag for them — so they are
                // asked about directly, gated on the frame being hovered.
                this.Fire(cfg, clicked, hovered, ref members[i], target);

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

    /// <summary>
    /// Runs whatever the player has bound to the button they just released on this frame.
    /// <para>
    /// The set is the one for the job they are on, so the same button is a heal on a White
    /// Mage and nothing on a Warrior — which is the point of keeping them per job.
    /// </para>
    /// </summary>
    private void Fire(
        Configuration.PartyFramesConfig cfg,
        bool clicked,
        bool hovered,
        ref PartyMemberSnapshot member,
        IGameObject target)
    {
        int button = ReleasedButton(clicked, hovered);

        if (button < 0)
        {
            return;
        }

        BindingModifiers held = HeldModifiers();
        System.Collections.Generic.List<MouseBinding> bindings = cfg.Bindings.For(LocalJobId());

        for (int i = 0; i < bindings.Count; i++)
        {
            MouseBinding binding = bindings[i];

            if (!binding.Matches(button, held))
            {
                continue;
            }

            switch (binding.Kind)
            {
                case BindingKind.Target:
                    Services.Targets.Target = target;
                    break;

                case BindingKind.ContextMenu:
                    // By place in the HUD agent's array, not by object — see
                    // NativeUi.OpenPartyContextMenu.
                    //
                    // 🔴 Not the number on the frame, which this used to pass. That number is
                    // the row the game draws the member on; the agent's array always starts
                    // with the local player instead. The two agree only in a party nobody has
                    // sorted, which is why passing the wrong one looked right.
                    NativeUi.OpenPartyContextMenu(member.PartyIndex);
                    break;

                case BindingKind.Action:
                    ActionUse.On(binding.ActionId, target.GameObjectId, target.Address);
                    break;
            }

            // One binding per press. Two that match the same button and modifiers is a
            // configuration nobody meant, and running both would be the worse reading of it.
            return;
        }
    }

    /// <summary>
    /// Which button was just released on this frame, or -1 for none.
    /// <para>
    /// The first three come from the invisible button, which answers on release and only
    /// inside its own area — that is what lets a press slide off a frame without counting,
    /// the way the game's own party list behaves. The two side buttons have no ImGui flag, so
    /// they are asked about directly and only while the frame is hovered.
    /// </para>
    /// </summary>
    private static int ReleasedButton(bool clicked, bool hovered)
    {
        if (clicked)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                return 1;
            }

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Middle))
            {
                return 2;
            }

            return 0;
        }

        if (!hovered)
        {
            return -1;
        }

        if (ImGui.IsMouseReleased((ImGuiMouseButton)3))
        {
            return 3;
        }

        return ImGui.IsMouseReleased((ImGuiMouseButton)4) ? 4 : -1;
    }

    /// <summary>What is being held right now, as the bindings describe it.</summary>
    private static BindingModifiers HeldModifiers()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        BindingModifiers held = BindingModifiers.None;

        if (io.KeyCtrl)
        {
            held |= BindingModifiers.Ctrl;
        }

        if (io.KeyShift)
        {
            held |= BindingModifiers.Shift;
        }

        if (io.KeyAlt)
        {
            held |= BindingModifiers.Alt;
        }

        return held;
    }

    /// <summary>The job the player is on, or zero when there is nobody to ask.</summary>
    private static uint LocalJobId() => Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u;

    /// <summary>
    /// Says what is wrong with a member the game has no numbers for, across the middle of
    /// their frame.
    /// <para>
    /// 🔴 In the middle, not where the health figure goes. The figure is a setting somebody
    /// can switch off, and this is not — a frame that has stopped reporting has to say so
    /// whatever else is turned on (Florian, 2026-09-12, who runs without one).
    /// </para>
    /// <para>
    /// Out of range says nothing at all. It is the common case, it lasts a few seconds, and a
    /// word written across four frames every time the group spreads out is noise. The dimming
    /// already carries it; the other two are the ones worth a word.
    /// </para>
    /// </summary>
    private static void DrawPresenceNote(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (member.HasData)
        {
            return;
        }

        string note = member.Presence switch
        {
            PartyPresence.Offline => Strings.PresenceOffline,
            PartyPresence.Away => Strings.PresenceAway,
            _ => string.Empty,
        };

        if (note.Length == 0)
        {
            return;
        }

        float size = Tokens.Px(cfg.HpTextSize);
        float width = Ink.MeasureNote(size, note);

        Vector2 at = new(
            MathF.Round(innerMin.X + (((innerMax.X - innerMin.X) - width) * 0.5f)),
            MathF.Round(innerMin.Y + (((innerMax.Y - innerMin.Y) - size) * 0.5f)));

        // 🔴 Not styled like the frame's own text, on any of the three counts.
        //
        // Grey, not white: the note explains why a frame is quiet, and white is what this
        // palette keeps for what must be read. Never outlined, whatever the lettering is set
        // to: an outline makes text cut itself out of the background and shout, which is right
        // for a name over the world and wrong for this. And always in the interface face,
        // never the chosen one — a name belongs to the frame and follows the player's taste, a
        // status the plugin reports does not, and in a serif face it would read as part of the
        // design rather than as a message (Florian, 2026-09-12).
        TextEdge edge = cfg.Edge == TextEdge.None ? TextEdge.None : TextEdge.Shadow;
        Ink.DrawNote(dl, size, at, Tokens.Col.HudInkQuiet, note, edge);
    }

    /// <summary>
    /// This colour, faded by however much the frame being drawn is stepped back. A no-op on a
    /// member who is there, which is nearly always.
    /// </summary>
    private uint Dim(uint colour) => m_dim >= 1f ? colour : Tokens.Col.Faded(colour, m_dim);

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
    /// <summary>
    /// Whether the player could actually take an effect off somebody right now. Read once a
    /// frame from the one player object, which costs nothing.
    /// </summary>
    private static bool CanCleanseNow()
    {
        var player = Services.Objects.LocalPlayer;

        return player is not null && StatusData.CanCleanse(player.ClassJob.RowId, player.Level);
    }

    /// <summary>
    /// Looks for raises in flight. On the tick because it costs a walk of the object table
    /// and must keep running whether or not anything is being drawn.
    /// </summary>
    public override void Tick() => m_snapshot.Tick(Environment.TickCount64 / 1000d);

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

        this.DrawIcon(dl, m_icon[slot], cfg.JobIconSize, cfg.JobIconPosition, cfg.JobIconX, cfg.JobIconY, innerMin, innerMax);
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

        this.DrawIcon(dl, m_leaderIcon, cfg.LeaderIconSize, cfg.LeaderIconPosition, cfg.LeaderIconX, cfg.LeaderIconY, innerMin, innerMax);
    }

    /// <summary>
    /// The mark that says something on this person can be taken off: a band of colour rising
    /// out of the bottom of the frame, and a thick edge around the whole of it.
    /// <para>
    /// 🔴 Two marks and not one, because one was not enough. A coloured edge alone was missed
    /// at a glance, which is the only thing this mark has to do — a healer is not reading
    /// frames, they are catching one out of eight (Florian, 2026-09-13). The rise gives it an
    /// area rather than a line, and area is what the eye catches.
    /// </para>
    /// <para>
    /// Drawn outside the frame, over everything. Inside it, a thick edge eats the bar it is
    /// framing; underneath, the next frame's ground paints across it.
    /// </para>
    /// </summary>
    private void DrawCleanseMark(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        Vector2 min,
        Vector2 max)
    {
        uint colour = this.Dim(cfg.CleanseColour);
        float thickness = MathF.Max(Tokens.Line(1f), Tokens.Px(cfg.CleanseThickness));

        // The rise, from the bottom of the frame to somewhere below halfway: far enough up to
        // be an area, not so far that it washes the whole bar and takes the role colour with
        // it. Fades to nothing, so it has no edge of its own to be mistaken for one.
        float height = MathF.Round((max.Y - min.Y) * CleanseRise);
        uint clear = colour & 0x00FFFFFFu;
        uint strong = Fade(colour, CleanseRiseOpacity);

        dl.AddRectFilledMultiColor(
            new Vector2(min.X, max.Y - height),
            max,
            clear,
            clear,
            strong,
            strong);

        // Outside, so the frame keeps all of its own room. AddRect puts half the thickness
        // either side of the path, so the path is pushed out by half.
        float out2 = thickness * 0.5f;
        dl.AddRect(
            new Vector2(min.X - out2, min.Y - out2),
            new Vector2(max.X + out2, max.Y + out2),
            colour,
            0f,
            ImDrawFlags.None,
            thickness);
    }

    /// <summary>How far up the frame the cleanse band reaches, as a share of its height.</summary>
    private const float CleanseRise = 0.45f;

    /// <summary>How solid that band is where it meets the bottom edge.</summary>
    private const float CleanseRiseOpacity = 0.55f;

    /// <summary>The same colour at a share of its own alpha.</summary>
    private static uint Fade(uint colour, float amount)
    {
        uint alpha = (colour >> 24) & 0xFFu;
        uint faded = (uint)MathF.Round(alpha * Math.Clamp(amount, 0f, 1f));
        return (colour & 0x00FFFFFFu) | (faded << 24);
    }

    /// <summary>
    /// The row of afflictions, highest ranked first.
    /// <para>
    /// The row is hung on one of the nine points as a whole, so it stays put as effects come
    /// and go: laying it out icon by icon would make the first one move every time a second
    /// appeared, which is the opposite of somewhere to look.
    /// </para>
    /// <para>
    /// Which way it grows follows the anchor, the way text does — a row hung on the right
    /// grows left. Anything else would run it off the frame it belongs to.
    /// </para>
    /// </summary>
    private void DrawAuras(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        int slot,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (cfg.ShowAuras)
        {
            this.DrawIconRow(
                dl,
                m_snapshot.Auras(slot),
                cfg.AuraMaxCount,
                cfg.AuraSize,
                cfg.AuraPosition,
                cfg.AuraX,
                cfg.AuraY,
                cfg.AuraShowStacks,
                cfg.AuraSwipe,
                innerMin,
                innerMax);
        }

        if (cfg.ShowBuffs)
        {
            this.DrawIconRow(
                dl,
                m_snapshot.Buffs(slot),
                cfg.BuffMaxCount,
                cfg.BuffSize,
                cfg.BuffPosition,
                cfg.BuffX,
                cfg.BuffY,
                cfg.BuffShowStacks,
                cfg.BuffSwipe,
                innerMin,
                innerMax);
        }

        if (cfg.ShowOtherBuffs)
        {
            this.DrawIconRow(
                dl,
                m_snapshot.Others(slot),
                cfg.OtherMaxCount,
                cfg.OtherSize,
                cfg.OtherPosition,
                cfg.OtherX,
                cfg.OtherY,
                cfg.BuffShowStacks,
                cfg.BuffSwipe,
                innerMin,
                innerMax);
        }
    }

    /// <summary>
    /// One row of status icons, hung on one of the nine points as a whole.
    /// <para>
    /// Written once and used by both rows. They differ only in which list they read and where
    /// they hang; the same row that draws the afflictions draws the benefits, so a change to
    /// how an icon looks lands on both (CLAUDE.md §5.2).
    /// </para>
    /// <para>
    /// The row is placed as a block so it stays put as effects come and go: laying it out icon
    /// by icon would make the first one move every time a second appeared, which is the
    /// opposite of somewhere to look. Which way it grows follows the anchor, the way text
    /// does — a row hung on the right grows left.
    /// </para>
    /// </summary>
    private void DrawIconRow(
        ImDrawListPtr dl,
        ReadOnlySpan<AuraSnapshot> auras,
        int limit,
        float iconSize,
        int position,
        float offsetX,
        float offsetY,
        bool showStacks,
        bool swipe,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        int count = Math.Min(auras.Length, limit);

        if (count <= 0)
        {
            return;
        }

        float side = Tokens.Px(iconSize);
        float gap = Tokens.Px(AuraGap);
        float width = (side * count) + (gap * (count - 1));

        Anchor anchor = Anchors.At(position);
        Vector2 at = Anchors.Place(
            anchor,
            innerMin,
            innerMax,
            new Vector2(width, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.Px(offsetX);
        at.Y += Tokens.Px(offsetY);

        // Hung on the right, the first icon belongs at the right end and the row fills
        // leftwards. The block is already placed, so this is only which end to start from.
        bool rightToLeft = (int)anchor % 3 == 2;
        float step = side + gap;

        for (int i = 0; i < count; i++)
        {
            ref readonly AuraSnapshot aura = ref auras[i];

            if (!Icons.StatusIcon(aura.Icon, out ImTextureID icon, out Vector2 uv0, out Vector2 uv1))
            {
                continue;
            }

            float x = rightToLeft ? at.X + width - side - (i * step) : at.X + (i * step);
            Vector2 min = new(MathF.Round(x), at.Y);
            Vector2 max = new(min.X + side, min.Y + side);

            // Cropped to the art. See Icons.StatusIcon — the whole texture is mostly margin.
            dl.AddImage(icon, min, max, uv0, uv1, this.Dim(0xFFFFFFFFu));

            if (swipe)
            {
                this.DrawSwipe(dl, min, max, aura.Remaining, aura.Duration);
            }

            // A bright edge on what can be taken off, so the row answers "which one" once the
            // frame's own edge has answered "is there one".
            if (aura.CanDispel)
            {
                dl.AddRect(min, max, this.Dim(m_config.PartyFrames.CleanseColour), 0f, ImDrawFlags.None, Tokens.Px(1f));
            }

            if (showStacks && aura.Stacks > 1)
            {
                this.DrawStacks(dl, min, max, aura.Stacks);
            }
        }
    }

    /// <summary>Air between two affliction icons. Small on purpose: the row reads as a row.</summary>
    private const float AuraGap = 2f;

    /// <summary>
    /// The dark wedge that sweeps off an icon as its effect runs out.
    /// <para>
    /// Drawn as a fan of triangles from the centre rather than one filled path, because a
    /// wedge past a half turn is not convex and ImGui's filled-polygon call quietly draws
    /// nonsense for one that is not. Each triangle is convex whatever the angle.
    /// </para>
    /// <para>
    /// 🔴 It covers what is LEFT, not what is spent, and so it shrinks away as the effect runs
    /// out. The other way round the wedge grows while the effect fades, which reads as
    /// something filling up rather than running down (Florian, 2026-09-13).
    /// </para>
    /// </summary>
    private void DrawSwipe(ImDrawListPtr dl, Vector2 min, Vector2 max, float remaining, float duration)
    {
        if (duration <= 0f || remaining <= 0f)
        {
            return;
        }

        float left = MathF.Min(remaining / duration, 1f);

        Vector2 centre = (min + max) * 0.5f;
        uint colour = this.Dim(Tokens.Col.AuraSwipe);

        // Enough steps that the edge of the wedge reads as straight at any icon size we allow,
        // and few enough that eight of these a frame cost nothing.
        const int Steps = 24;
        int taken = (int)MathF.Ceiling(Steps * left);

        for (int i = 0; i < taken; i++)
        {
            float from = i / (float)Steps;
            float to = MathF.Min((i + 1) / (float)Steps, left);

            if (to <= from)
            {
                break;
            }

            dl.AddTriangleFilled(centre, OnSquare(centre, min, max, from), OnSquare(centre, min, max, to), colour);
        }
    }

    /// <summary>
    /// Where a fraction of a turn clockwise from the top meets the edge of the square.
    /// <para>
    /// Against the square rather than a circle inside it, so the wedge reaches the corners and
    /// the icon is actually covered — a circular sweep leaves four lit triangles behind.
    /// </para>
    /// </summary>
    private static Vector2 OnSquare(Vector2 centre, Vector2 min, Vector2 max, float turn)
    {
        float angle = turn * MathF.Tau;
        float dx = MathF.Sin(angle);
        float dy = -MathF.Cos(angle);

        float halfX = (max.X - min.X) * 0.5f;
        float halfY = (max.Y - min.Y) * 0.5f;

        // The longer of the two reaches decides: whichever axis would leave the square first
        // is the one scaled to its edge.
        float scale = MathF.Max(MathF.Abs(dx) / halfX, MathF.Abs(dy) / halfY);

        return scale <= 0f ? centre : new Vector2(centre.X + (dx / scale), centre.Y + (dy / scale));
    }

    /// <summary>How many of it there are, in the bottom right of its icon.</summary>
    private void DrawStacks(ImDrawListPtr dl, Vector2 min, Vector2 max, ushort stacks)
    {
        string text = StackText[Math.Min((int)stacks, StackText.Length) - 1];

        // Half the icon, so the number scales with whatever size the icons are set to rather
        // than staying put and swallowing a small one.
        float size = MathF.Max(Tokens.Px(AuraStackMinSize), MathF.Round((max.Y - min.Y) * 0.5f));
        float width = Ink.MeasureWidth(size, text);

        Vector2 at = new(MathF.Round(max.X - width - 1f), MathF.Round(max.Y - size));

        // Always outlined, whatever the frame's own text edge is set to. This one sits on a
        // picture rather than on a bar, and a picture can be any colour underneath.
        Ink.DrawScaledEdged(dl, size, at, this.Dim(Tokens.Col.HudInk), text, TextEdge.Outline);
    }

    /// <summary>Stack counts, built once. Past the end the number is simply not drawn.</summary>
    private static readonly string[] StackText = BuildStackText();

    private static string[] BuildStackText()
    {
        // Well past anything the game stacks, and it is thirty strings made once at load
        // rather than one made per icon per frame.
        var text = new string[30];

        for (int i = 0; i < text.Length; i++)
        {
            text[i] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return text;
    }

    private const float AuraStackMinSize = 10f;

    /// <summary>
    /// A raise on its way, or somebody who cannot be killed.
    /// <para>
    /// Its own place rather than a slot in the icon row, because the row is ranked and drops
    /// what does not fit — and these two are exactly what may not be dropped (Florian,
    /// 2026-09-13). Invulnerability wins when both are somehow true: it is the one that
    /// changes what you do in the next second.
    /// </para>
    /// </summary>
    private void DrawRescue(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (!cfg.ShowRescueIcon)
        {
            return;
        }

        // Whichever effect is actually on them, so the picture is the game's own for it and
        // there is nothing of ours to keep in step with a patch.
        uint status = member.InvulnerableStatus != 0 ? member.InvulnerableStatus
            : member.RaiseRemaining > 0f ? StatusData.Raise
            : 0u;

        if (status == 0)
        {
            return;
        }

        // Through the status route, not the plain one: these are status pictures like the rows
        // are, and drawn whole they carry the same margin and plate the afflictions used to
        // (Florian, 2026-09-13: it was not coming out rectangular like the reference shot).
        if (!Icons.StatusIcon(StatusData.Of(status).Icon, out ImTextureID icon, out Vector2 uv0, out Vector2 uv1))
        {
            return;
        }

        float side = Tokens.Px(cfg.RescueIconSize);
        Vector2 at = Anchors.Place(
            Anchors.At(cfg.RescueIconPosition),
            innerMin,
            innerMax,
            new Vector2(side, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.Px(cfg.RescueIconX);
        at.Y += Tokens.Px(cfg.RescueIconY);

        dl.AddImage(icon, at, new Vector2(at.X + side, at.Y + side), uv0, uv1, this.Dim(0xFFFFFFFFu));
    }

    /// <summary>
    /// One picture on a frame, square and hung on one of the nine points. Written once because
    /// every icon a frame will ever carry — job, leader, raid marker — is placed the same way,
    /// and a second copy of this is a second place to fix a rounding.
    /// </summary>
    private void DrawIcon(
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

        // Tinted white at the frame's own fade, so an icon steps back with the rest of it
        // rather than staying the one bright thing on a frame that has gone quiet.
        dl.AddImage(icon, at, new Vector2(at.X + side, at.Y + side), Vector2.Zero, Vector2.One, this.Dim(0xFFFFFFFFu));
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
            // 🔴 A dimmed frame gets darker text, not just fainter text. White at two thirds
            // opacity is still white, and on a frame that has stepped back the name was the
            // one thing still shouting (Florian, 2026-09-12).
            uint colour = this.Dim(cfg.NameInJobColour
                ? Jobs.Colour(member.JobId)
                : (member.HasData ? Tokens.Col.HudInk : Tokens.Col.HudInkQuiet));

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

        Ink.DrawScaledEdged(dl, healthSize, healthAt, this.Dim(Tokens.Col.HudInk), health, cfg.Edge);
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
        // 🔴 A member who is merely unreachable is drawn full, not empty: an empty bar is a
        // statement about their health, and that is the thing we do not know. Full and dimmed
        // says "no reading" instead.
        //
        // Offline is the exception and is drawn empty, because there it is not a missing
        // reading — the person is gone, and a full bar would say they are fine (Florian,
        // 2026-09-12).
        if (!member.HasData)
        {
            float away = member.Presence == PartyPresence.Offline ? 0f : 1f;
            m_shownHealthFor[slot] = member.EntityId;
            m_shownHealth[slot] = away;
            return away;
        }

        float target = member.MaxHp > 0 ? Math.Clamp(member.Hp / (float)member.MaxHp, 0f, 1f) : 0f;

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
        // Nothing rather than a number. "0" or "100%" about somebody the game has no reading
        // for is an invention. What is wrong with them is said in the middle of the frame
        // instead — see PresenceNote, which has to be somewhere the player has not switched
        // off (Florian, 2026-09-12, who runs without a health figure).
        if (!member.HasData)
        {
            return string.Empty;
        }

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
                "  [{Slot}] {Name} no={Number} content={Content} job={Job} role={Role} presence={Presence} hp={Hp}/{MaxHp} mp={Mp}/{MaxMp} self={Self}",
                i,
                member.Name,
                member.PartyNumber,
                member.NameKey,
                member.JobId,
                member.Role,
                member.Presence,
                member.Hp,
                member.MaxHp,
                member.Mp,
                member.MaxMp,
                member.IsLocalPlayer);
        }
    }
}
