using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface.Widgets;
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

    /// <summary>The stand-ins the settings window preview is drawn from. Never the live one.</summary>
    private readonly PartySnapshot m_preview = new();

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
    /// Where a block of frames landed, kept from the pass that drew it.
    /// <para>
    /// 🔴 One of these per block, and there are two blocks: the one on the world and the one
    /// in the settings window's preview. They cannot share, because these numbers are what
    /// the mouse is tested against — a preview drawn after the real frames would leave the
    /// hit boxes sitting inside the settings window, and clicking a party member would target
    /// whoever the preview happened to have in that spot. Which of the two draws first is not
    /// something to rely on.
    /// </para>
    /// </summary>
    private sealed class FrameGeometry
    {
        /// <summary>
        /// Each frame's inside, worked out in the first pass and read back in the second. The
        /// two passes exist so a name or an icon can sit outside its own frame without the
        /// next frame's ground being painted over it.
        /// </summary>
        public readonly Vector2[] InnerMin = new Vector2[PartySnapshot.Capacity];
        public readonly Vector2[] InnerMax = new Vector2[PartySnapshot.Capacity];
        public readonly bool[] HasInside = new bool[PartySnapshot.Capacity];

        /// <summary>
        /// Each frame's outside, for the mouse. The inside is where things are drawn; the
        /// edge is still part of the thing you are clicking on.
        /// </summary>
        public readonly Vector2[] FrameMin = new Vector2[PartySnapshot.Capacity];
        public readonly Vector2[] FrameMax = new Vector2[PartySnapshot.Capacity];
    }

    private readonly FrameGeometry m_liveGeo = new();
    private readonly FrameGeometry m_previewGeo = new();

    /// <summary>
    /// What this block is leaving out. Always nothing for the live block: the eye switches
    /// are a way of looking at the preview, never a setting (see <see cref="PreviewMask"/>).
    /// </summary>
    private PreviewPart m_hidden;

    /// <summary>Whether this part is being drawn in the block currently being drawn.</summary>
    private bool Shows(PreviewPart part) => (m_hidden & part) == 0;

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
    /// How solid that same frame is. Kept apart from the brightness because the two answer
    /// different questions: darker says "this person is gone", see-through says "you cannot
    /// reach them right now".
    /// </summary>
    private float m_alpha = 1f;

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
        // Always the live block, never whatever was drawn last. Edit mode moves the frames on
        // the world, and the preview is a picture of them somewhere else entirely.
        FrameGeometry geo = m_liveGeo;

        min = default;
        max = default;
        bool any = false;

        for (int i = 0; i < m_snapshot.Count; i++)
        {
            if (!geo.HasInside[i])
            {
                continue;
            }

            if (!any)
            {
                min = geo.FrameMin[i];
                max = geo.FrameMax[i];
                any = true;
                continue;
            }

            min = Vector2.Min(min, geo.FrameMin[i]);
            max = Vector2.Max(max, geo.FrameMax[i]);
        }
    }

    /// <summary>
    /// Takes a screen position and stores it.
    /// <para>
    /// 🔴 Stored as it arrives, with no scale in the arithmetic. It used to be divided by
    /// the interface scale, which was the matching half of the bug where the frames grew
    /// with the settings window — the drawing multiplied and this divided, so the two
    /// agreed with each other and disagreed with the slider. A HUD position is a screen
    /// position (2026-09-21, see <see cref="Tokens.WorldPx"/>).
    /// </para>
    /// </summary>
    public override void MoveTo(Vector2 topLeft)
    {
        m_config.PartyFrames.PositionX = MathF.Round(topLeft.X);
        m_config.PartyFrames.PositionY = MathF.Round(topLeft.Y);
        m_config.MarkDirty();
    }

    public override void Collect()
    {
        // 🔴 The preview brings a full party of its own, and does NOT go through edit mode to
        // get one. Edit mode closes the settings window on purpose — you are dragging the
        // things it covers — which is exactly wrong for a tab whose every control needs to be
        // watched while it is moved (Florian, 2026-09-13: the icons were never visible,
        // because turning on the thing that showed eight frames took the window away).
        if (EditMode.IsActive)
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
            this.DrawLive(dl);
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
            this.DrawLive(dl);
            dl.PopClipRect();
        }
    }

    public override bool HasPreview => true;

    public override Vector2 PreviewSize(int count) =>
        FrameLayout.BlockSize(
            count,
            (FrameDirection)m_config.PartyFrames.Direction,
            m_config.PartyFrames.Lines,
            Tokens.WorldPx(m_config.PartyFrames.FrameWidth),
            Tokens.WorldPx(m_config.PartyFrames.FrameHeight),
            Tokens.WorldPx(m_config.PartyFrames.Spacing));

    /// <summary>
    /// The frames as they will look, drawn into the settings window.
    /// <para>
    /// Its own snapshot, and not only to keep the stand-ins out of the real one: the bars
    /// carry their animation forward <em>as they are drawn</em>, so a block sharing the live
    /// snapshot would advance it a second time every frame and every smooth bar in the game
    /// would run at double speed.
    /// </para>
    /// </summary>
    public override void DrawPreview(ImDrawListPtr dl, Vector2 origin, int count)
    {
        // Always with stand-in effects. The checkbox that used to put made-up afflictions on
        // the real frames is gone: it existed because the icons could not be seen without a
        // fight, and this band is that answer done properly (Florian, 2026-09-19).
        m_preview.FillPlaceholders(count, true);
        this.DrawContent(dl, origin, m_preview, m_previewGeo, false);
    }

    /// <summary>
    /// The block on the world, at the position the player put it.
    /// </summary>
    private void DrawLive(ImDrawListPtr dl) =>
        this.DrawContent(
            dl,
            new Vector2(Tokens.WorldPx(m_config.PartyFrames.PositionX), Tokens.WorldPx(m_config.PartyFrames.PositionY)),
            m_snapshot,
            m_liveGeo,
            true);

    /// <summary>
    /// Draws one block of frames: the real one on the world, or the preview in the settings
    /// window. One routine for both, and that is the point — a preview drawn by a second,
    /// simpler renderer is a preview that lies, and every change after it would have to be
    /// made twice.
    /// </summary>
    /// <param name="origin">The block's top-left corner, already in screen pixels.</param>
    /// <param name="snapshot">Who to draw. The preview brings its own, filled with stand-ins.</param>
    /// <param name="geo">Where to record what was drawn.</param>
    /// <param name="live">
    /// Whether this block is the one on the world. Only that one takes the mouse, tells the
    /// game what is being pointed at, and shows tooltips — a picture in a window does none of
    /// those things, and a second block asking ImGui for the same mouse would be two answers
    /// to one question.
    /// </param>
    private void DrawContent(
        ImDrawListPtr dl,
        Vector2 origin,
        PartySnapshot snapshot,
        FrameGeometry geo,
        bool live)
    {
        Configuration.PartyFramesConfig cfg = m_config.PartyFrames;

        float width = Tokens.WorldPx(cfg.FrameWidth);
        float height = Tokens.WorldPx(cfg.FrameHeight);
        float spacing = Tokens.WorldPx(cfg.Spacing);
        float border = Tokens.Metric.FrameBorder;
        float x = origin.X;
        float y = origin.Y;
        float delta = ImGui.GetIO().DeltaTime;

        // Settled once for the whole block, before anything is drawn from it.
        m_hidden = live ? PreviewPart.None : PreviewMask.Hidden;

        BarStyle style = BarStyles.At(BarStyles.ForBar, cfg.BarStyleName);

        // Resolved here beside the bar's, once for the whole block rather than once per frame
        // per member: looking a name up walks the list, which is nothing on its own and is
        // eight times nothing in a full party, sixty times a second, for an answer that cannot
        // change between two members.
        BarStyle shieldStyle = BarStyles.At(BarStyles.ForShield, cfg.ShieldStyleName);
        var colourMode = (BarColourMode)cfg.ColourMode;
        var manaStyle = (ManaStyle)cfg.ManaStyle;
        HealthTextMode textMode = HealthText.At(cfg.HpTextMode);
        // The switch decides whether there is a mark; the style decides what it looks like.
        // Resolved to None here so the drawing below has one question to ask instead of two.
        FrameMarkStyle cleanseMark = cfg.ShowCleanseMark ? FrameMark.At(cfg.CleanseMark) : FrameMarkStyle.None;
        FrameMarkStyle raiseMark = cfg.ShowRaiseMark ? FrameMark.At(cfg.RaiseMark) : FrameMarkStyle.None;

        // Settled once for the whole block. Off while the stand-ins are up: there is nothing
        // real to describe, and edit mode wants the cursor for dragging rather than for
        // pointing at things.
        // Never for the preview: there is nothing real to describe, and the pointer is over a
        // settings window whose own tooltips would fight with these.
        // Whether a tooltip may go up at all. Which ROW it may go up over is asked per row,
        // where the icons are drawn — the three ask different questions (see the config).
        m_wantTooltips = live && !EditMode.IsActive;

        // The mark is an instruction. On a job that cannot carry it out it is noise, so it is
        // off there by default — the icons still show the effect either way. The preview
        // ignores this, or setting it up on the wrong job would show nothing.
        if (live && cfg.CleanseOnlyWhenAble && !CanCleanseNow())
        {
            cleanseMark = FrameMarkStyle.None;
        }

        PartyMemberSnapshot[] members = snapshot.Members;
        int count = snapshot.Count;

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
            geo.HasInside[i] = innerMax.X > innerMin.X && innerMax.Y > innerMin.Y;
            if (!geo.HasInside[i])
            {
                continue;
            }

            geo.InnerMin[i] = innerMin;
            geo.InnerMax[i] = innerMax;
            geo.FrameMin[i] = min;
            geo.FrameMax[i] = max;

            // 🔴 One factor for the whole frame, set here and read by everything that draws
            // part of it. Dimming only the bar left a frame whose name, icons and number were
            // as loud as everybody else's, so it did not read as stepped back at all
            // (Florian, 2026-09-12).
            // Three states, and they step back in two different ways on purpose.
            //
            // 🔴 Out of range is only made SEE-THROUGH, never darker. They are standing right
            // there and will be back in a moment — the frame has to stay as readable as
            // anybody else's, and darkening it would say something about them rather than
            // about the distance (Florian, 2026-09-13). The other two are genuinely gone, and
            // those recede in colour as well.
            (m_dim, m_alpha) = member.Presence switch
            {
                PartyPresence.Here => (1f, 1f),
                PartyPresence.Offline => (Tokens.Metric.OfflineDim, Tokens.Metric.AbsentAlpha),
                PartyPresence.Away => (Tokens.Metric.AwayDim, Tokens.Metric.AbsentAlpha),
                _ => (1f, Tokens.Metric.OutOfRangeAlpha),
            };

            dl.AddRectFilled(min, max, this.Dim(Tokens.Col.FrameBg));

            float healthBottom = innerMax.Y;
            bool mana = ShowsMana(cfg, ref member) && this.Shows(PreviewPart.Mana);
            float manaHeight = Tokens.WorldPx(cfg.ManaHeight);
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
            uint barColour = cleanseMark == FrameMarkStyle.Bar && member.HasDispellable
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

            // Over the health, under everything else. A shield is part of what the bar says
            // about staying alive, so it belongs in the bar rather than on top of the icons.
            if (cfg.ShowShield && this.Shows(PreviewPart.Shield) && member.HasData && member.Shield > 0)
            {
                // The game keeps a percentage, so a hundredth of it is the share of the bar.
                ShieldBand band = Shield.Band(fraction, member.Shield / 100f);

                // 🔴 Its own strength, NOT the health bar's. The two were tied together at
                // first and that was wrong: the bar's opacity is about how much the frame
                // asserts itself over the world, while the shield's is about reading as
                // something laid ON the bar (Florian, 2026-09-18, who wants the texture
                // showing through weakened once there are shield textures).
                uint shieldColour = this.Dim(Tokens.Col.Faded(cfg.ShieldColour, cfg.ShieldOpacity));
                float barWidth = barMax.X - barMin.X;

                // One band in one colour. Painted across the whole bar and clipped, exactly as
                // the health fill above is — which is also the seam a shield texture drops
                // into later: it becomes a style of its own here and nothing else moves.
                if (band.HasBand)
                {
                    dl.PushClipRect(
                        new Vector2(BarX(barMin.X, barWidth, band.Start), barMin.Y),
                        new Vector2(BarX(barMin.X, barWidth, band.End), barMax.Y),
                        true);
                    BarStyles.Draw(dl, shieldStyle, barMin, barMax, shieldColour, 0f);
                    dl.PopClipRect();
                }

                // Where the band starts inside the fill it has no contrast of its own, so it
                // gets a line. As the shield decays this line is what you watch: it travels
                // right until the overflow is gone, and only then does the far end start
                // coming back (Florian, 2026-09-18, describing exactly that).
                if (band.HasMark)
                {
                    float markX = BarX(barMin.X, barWidth, band.MarkAt);
                    float thickness = MathF.Max(1f, Tokens.WorldPx(Tokens.Metric.ShieldMark));

                    dl.AddRectFilled(
                        new Vector2(markX, barMin.Y),
                        new Vector2(markX + thickness, barMax.Y),
                        shieldColour);
                }
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
        if (live)
        {
            this.TakeTheMouse(dl, cfg, geo, count);
        }

        // The second pass. A name or an icon may be placed outside its own frame — above it,
        // beside it — and that is a layout people build on purpose, not a mistake to guard
        // against. Drawn in the same loop as the bars, anything hanging below a frame would
        // be painted over by the next frame's ground a moment later.
        float reach = Tokens.WorldPx(Configuration.MaxTextOffset);

        for (int i = 0; i < count; i++)
        {
            if (!geo.HasInside[i])
            {
                continue;
            }

            ref PartyMemberSnapshot member = ref members[i];
            Vector2 innerMin = geo.InnerMin[i];
            Vector2 innerMax = geo.InnerMax[i];

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
            this.DrawAuras(dl, cfg, snapshot, i, innerMin, innerMax);

            // Over everything, and outside the frame rather than on its edge. On the edge a
            // thick mark eats into the bar it is meant to be framing, and under the second
            // pass the next frame's ground painted across it (Florian, 2026-09-13: it must
            // not sit behind the frame).
            // Cleanse first, raise over it. They can both be true — somebody being picked up
            // may well have something cleansable on them — and of the two, "you personally
            // have to do something" outranks "this one is already being handled".
            if (member.HasDispellable && this.Shows(PreviewPart.CleanseMark))
            {
                FrameMark.Draw(
                    dl,
                    cleanseMark,
                    geo.FrameMin[i],
                    geo.FrameMax[i],
                    this.Dim(cfg.CleanseColour),
                    FrameMark.Thickness(cfg.CleanseThickness),
                    cfg.CleanseOpacity);
            }

            // Raise covers the cast as well as the landed effect, which is the whole point:
            // the eight seconds of casting are exactly when a second healer needs to know
            // somebody is already on this one, and that is what the mark says from across the
            // screen (Florian, 2026-09-18).
            if (member.RaiseRemaining > 0f && this.Shows(PreviewPart.RaiseMark))
            {
                FrameMark.Draw(
                    dl,
                    raiseMark,
                    geo.FrameMin[i],
                    geo.FrameMax[i],
                    this.Dim(cfg.RaiseColour),
                    FrameMark.Thickness(cfg.RaiseThickness),
                    cfg.RaiseOpacity);
            }

            // 🔴 AFTER the marks, not before. The raise mark and the rescue icon are about the
            // same moment, so they are on screen together more often than not — and with the
            // icon drawn first the wash laid straight over the picture it was agreeing with
            // (Florian, 2026-09-18). The mark is the thing read from across the screen and the
            // icon is the thing read when you look, so the icon is the one that has to survive.
            this.DrawRescue(dl, cfg, ref member, innerMin, innerMax);

            DrawPresenceNote(dl, cfg, ref member, innerMin, innerMax);
            dl.PopClipRect();
        }

        if (live)
        {
            this.DrawAuraTooltip();
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
    private void TakeTheMouse(ImDrawListPtr dl, Configuration.PartyFramesConfig cfg, FrameGeometry geo, int count)
    {
        // Nothing to take while the layout is being set against stand-ins: there is nobody to
        // select, and edit mode wants the same button for dragging.
        //
        // 🔴 The mouseover spells are in this list too, and they were the easy one to leave
        // out: nothing on a frame reacts to them, so nothing on screen would have said the
        // mouse was never taken — the spells would simply have gone to the selected target,
        // which is what they do anyway when you are not pointing at anybody. Pointing at
        // somebody is only known while the mouse is ours.
        uint job = LocalJobId();

        if (count == 0
            || EditMode.IsActive
            || (cfg.Bindings.For(job).Count == 0
                && cfg.Mouseover.For(job).Count == 0
                && !cfg.MouseoverTarget
                && !cfg.HighlightHovered))
        {
            this.ReleaseMouseOver();
            return;
        }

        Vector2 blockMin = geo.FrameMin[0];
        Vector2 blockMax = geo.FrameMax[0];

        for (int i = 1; i < count; i++)
        {
            if (!geo.HasInside[i])
            {
                continue;
            }

            blockMin = Vector2.Min(blockMin, geo.FrameMin[i]);
            blockMax = Vector2.Max(blockMax, geo.FrameMax[i]);
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
                if (!geo.HasInside[i])
                {
                    continue;
                }

                ImGui.SetCursorScreenPos(geo.FrameMin[i]);
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
                    geo.FrameMax[i] - geo.FrameMin[i],
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
                        new Vector2(geo.FrameMin[i].X - ring, geo.FrameMin[i].Y - ring),
                        new Vector2(geo.FrameMax[i].X + ring, geo.FrameMax[i].Y + ring),
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
                MouseoverCasting.PointAt(target.GameObjectId);
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
                    // 🔴 SETTLED IN THE GAME (Florian, 2026-09-13). Three numbers could have
                    // been meant and the call documents none of them; right-clicking the party
                    // leader opened the local player's own profile, which is only possible if
                    // the index goes into the HUD agent's array — that one always begins with
                    // the local player, so the leader's place in the party list, zero, landed
                    // on us.
                    //
                    // Neither of the other two, then: not the row the frame is drawn on, and
                    // not the place in the party list Dalamud hands us, which is what was
                    // being passed on the strength of another plugin doing so for years.
                    // Evidence beat inference.
                    NativeUi.OpenPartyContextMenu(member.HudIndex);
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
    /// A fraction of a bar, as a pixel on the screen. Rounded, so two pieces that meet at the
    /// same fraction land on the same pixel and leave no seam between them.
    /// </summary>
    private static float BarX(float left, float width, float fraction) =>
        MathF.Round(left + (width * fraction));

    /// <summary>
    /// Says across the middle of a frame what the bar alone cannot: that the game has no
    /// numbers for this member, or that it has numbers and they are zero.
    /// <para>
    /// 🔴 In the middle, not where the health figure goes. The figure is a setting somebody
    /// can switch off, and this is not — a frame that has stopped reporting has to say so
    /// whatever else is turned on (Florian, 2026-09-12, who runs without one).
    /// </para>
    /// <para>
    /// Out of range says nothing at all. It is the common case, it lasts a few seconds, and a
    /// word written across four frames every time the group spreads out is noise. The dimming
    /// already carries it; the others are the ones worth a word.
    /// </para>
    /// <para>
    /// Dead joined them on 2026-09-18 (Florian). It is the odd one out: the other notes are
    /// about somebody the game has stopped describing, and this one is about somebody it
    /// describes perfectly well. What they share is that an empty bar is the same picture for
    /// all of them, and only the word tells them apart.
    /// </para>
    /// </summary>
    private static void DrawPresenceNote(
        ImDrawListPtr dl,
        Configuration.PartyFramesConfig cfg,
        ref PartyMemberSnapshot member,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        string note;

        if (member.HasData)
        {
            // Dead is the one note about somebody who IS here, which is why it cannot ride on
            // the presence value the way the other two do — presence answers "can we see
            // them", and a corpse answers yes.
            //
            // Not left to the health figure alone: that figure is optional, can be set to say
            // percent, and can be anchored anywhere in the frame, so a group that turned it
            // off would have nothing but an empty bar to go on. An empty bar is also what
            // out of range looked like before session 9, and one of those two is urgent.
            // MaxHp guards the frame or two after a zone change, where everything reads zero.
            if (member.Hp != 0 || member.MaxHp == 0)
            {
                return;
            }

            note = Strings.PresenceDead;
        }
        else
        {
            note = member.Presence switch
            {
                PartyPresence.Offline => Strings.PresenceOffline,
                PartyPresence.Away => Strings.PresenceAway,
                _ => string.Empty,
            };

            if (note.Length == 0)
            {
                return;
            }
        }

        float size = Tokens.WorldPx(cfg.HpTextSize);
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
    /// <summary>
    /// Steps a frame back without making it see-through.
    /// <para>
    /// 🔴 By DARKENING the colour, not by taking its alpha away. These frames lie over the
    /// game world, so a bar at half alpha shows grass and stone through itself — and whatever
    /// is behind it drags every colour towards the same muddy grey. At 0.85 nothing was
    /// visible, at 0.45 the class colour was gone, and both were the same mistake
    /// (Florian, 2026-09-13, twice).
    /// </para>
    /// <para>
    /// Exactly the rule already written down after session 8: opacity says how present
    /// something is, colour says how important. Something that should recede needs a darker
    /// colour, not a thinner one. The frame stays solid, and a dark green is still green.
    /// </para>
    /// </summary>
    private uint Dim(uint colour) =>
        Tokens.Col.Softer(m_dim >= 1f ? colour : Tokens.Col.Darker(colour, m_dim), m_alpha);

    /// <summary>
    /// The same, for writing, and half as far.
    /// <para>
    /// A bar can go properly dark and still be a bar — its job is to be a colour and a length.
    /// A name has to be read, and text taken as far down as the fill it sits on stops being
    /// text. So the frame steps back and its writing steps back with it, but only half as far.
    /// </para>
    /// </summary>
    private uint DimInk(uint colour) =>
        m_dim >= 1f ? colour : Tokens.Col.Darker(colour, 0.5f + (m_dim * 0.5f));

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
            MouseoverCasting.PointAt(0ul);
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
        if (!cfg.ShowJobIcon || !this.Shows(PreviewPart.JobIcon) || (cfg.JobIconHideDps && member.Role == JobRole.Dps))
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
        if (!cfg.ShowLeaderIcon || !this.Shows(PreviewPart.Leader) || !member.IsLeader)
        {
            return;
        }

        this.DrawIcon(dl, m_leaderIcon, cfg.LeaderIconSize, cfg.LeaderIconPosition, cfg.LeaderIconX, cfg.LeaderIconY, innerMin, innerMax);
    }

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
    /// 🔴 Reads the snapshot it was handed, not the live one. It read m_snapshot directly
    /// until 2026-09-19, which meant the preview drew the real party's effects onto stand-in
    /// people — that is to say none at all, since the settings window is usually open out of
    /// combat. Everything else on a frame came through the member struct and was right by
    /// accident; the icon rows are the one thing that goes back to the snapshot for a slice.
    /// </para>
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
        PartySnapshot snapshot,
        int slot,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        if (cfg.ShowAuras && this.Shows(PreviewPart.Debuffs))
        {
            this.DrawIconRow(
                dl,
                snapshot.Auras(slot),
                cfg.AuraMaxCount,
                cfg.AuraSize,
                cfg.AuraPosition,
                cfg.AuraX,
                cfg.AuraY,
                cfg.AuraShowStacks,
                cfg.AuraSwipe,
                cfg.AuraShowDuration,
                cfg.AuraDispelBorder,
                m_wantTooltips && cfg.ShowAuraTooltips,
                innerMin,
                innerMax);
        }

        if (cfg.ShowBuffs && this.Shows(PreviewPart.OwnBuffs))
        {
            this.DrawIconRow(
                dl,
                snapshot.Buffs(slot),
                cfg.BuffMaxCount,
                cfg.BuffSize,
                cfg.BuffPosition,
                cfg.BuffX,
                cfg.BuffY,
                cfg.BuffShowStacks,
                cfg.BuffSwipe,
                cfg.BuffShowDuration,
                false,
                m_wantTooltips && cfg.ShowBuffTooltips,
                innerMin,
                innerMax);
        }

        if (cfg.ShowOtherBuffs && this.Shows(PreviewPart.OtherBuffs))
        {
            this.DrawIconRow(
                dl,
                snapshot.Others(slot),
                cfg.OtherMaxCount,
                cfg.OtherSize,
                cfg.OtherPosition,
                cfg.OtherX,
                cfg.OtherY,
                cfg.BuffShowStacks,
                cfg.BuffSwipe,
                cfg.BuffShowDuration,
                false,
                m_wantTooltips && cfg.ShowOtherTooltips,
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
    /// <summary>
    /// Whether the mouse is inside a rectangle, in screen pixels.
    /// <para>
    /// Asked of ImGui rather than of the game: the frames already put an invisible window
    /// under the cursor to collect their clicks, so ImGui's idea of where the pointer is is
    /// the same one every other part of this element works from.
    /// </para>
    /// </summary>
    private static bool Inside(Vector2 min, Vector2 max)
    {
        Vector2 mouse = ImGui.GetMousePos();
        return mouse.X >= min.X && mouse.X < max.X && mouse.Y >= min.Y && mouse.Y < max.Y;
    }

    /// <summary>Whether tooltips are wanted this frame, so the hover test is skipped when they are off.</summary>
    private bool m_wantTooltips;

    /// <summary>The effect the mouse is over, or zero. Settled during the pass, drawn after it.</summary>
    private uint m_tooltipStatus;

    /// <summary>
    /// The tooltip for whichever icon the mouse ended up over.
    /// <para>
    /// Drawn after every frame has been painted, because a tooltip belongs over all of them —
    /// drawn where it was noticed, the next frame's icons would be painted across it. Nothing
    /// is looked up unless something is actually hovered, so the common case is one comparison.
    /// </para>
    /// </summary>
    private void DrawAuraTooltip()
    {
        uint status = m_tooltipStatus;
        m_tooltipStatus = 0u;

        if (status == 0u || !StatusData.Describe(status, out string name, out string description))
        {
            return;
        }

        Chrome.Tooltip(name, description);
    }

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
        bool showDuration,
        bool dispelBorder,
        bool tooltips,
        Vector2 innerMin,
        Vector2 innerMax)
    {
        int count = Math.Min(auras.Length, limit);

        if (count <= 0)
        {
            return;
        }

        float side = Tokens.WorldPx(iconSize);
        float gap = Tokens.WorldPx(AuraGap);
        float width = (side * count) + (gap * (count - 1));

        Anchor anchor = Anchors.At(position);
        Vector2 at = Anchors.Place(
            anchor,
            innerMin,
            innerMax,
            new Vector2(width, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.WorldPx(offsetX);
        at.Y += Tokens.WorldPx(offsetY);

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

            // Noted, not drawn. The tooltip belongs over every frame rather than over this
            // one, and the rows are painted frame by frame — writing it here would put it
            // under whatever is drawn next. So the last one the mouse was inside wins and the
            // panel goes up once, after the loop.
            if (tooltips && Inside(min, max))
            {
                m_tooltipStatus = aura.StatusId;
            }

            if (swipe)
            {
                this.DrawSwipe(dl, min, max, aura.Remaining, aura.Duration);
            }

            // A coloured edge on what can be taken off, so the row answers "which one" once
            // the frame's own mark has answered "is there one".
            //
            // 🔴 It was a one-pixel line, always on, in the cleanse colour — which at a
            // twenty-pixel icon is a line nobody sees, and a mark nobody sees is a missing
            // mark, not a quiet one (Florian, 2026-09-21: the debuffs are hard to tell
            // apart; the same lesson the cleanse mark itself learned in session 9).
            if (dispelBorder && aura.CanDispel)
            {
                dl.AddRect(
                    min,
                    max,
                    this.Dim(m_config.PartyFrames.CleanseColour),
                    0f,
                    ImDrawFlags.None,
                    Tokens.WorldLine(m_config.PartyFrames.AuraDispelThickness));
            }

            if (showDuration)
            {
                this.DrawDuration(dl, min, max, aura.Remaining);
            }

            if (showStacks && aura.Stacks > 1)
            {
                this.DrawStacks(dl, min, max, aura.Stacks);
            }
        }
    }

    /// <summary>
    /// How long is left, across the middle of the icon.
    /// <para>
    /// A share of the icon, like the stack count, and for the same reason: a number with a
    /// size of its own runs out of its icon the moment somebody moves the icon slider.
    /// </para>
    /// </summary>
    private void DrawDuration(ImDrawListPtr dl, Vector2 min, Vector2 max, float remaining)
    {
        string? text = DurationText(remaining);

        if (text is null)
        {
            return;
        }

        float size = this.AuraNumberHeight(max.Y - min.Y, 1f);
        float width = Ink.MeasureWidth(size, text);

        Vector2 at = new(
            MathF.Round(((min.X + max.X) * 0.5f) - (width * 0.5f)),
            MathF.Round(((min.Y + max.Y) * 0.5f) - (size * 0.5f)));

        // Outlined whatever the frame's text edge is, like the stack count: this one sits on
        // a picture, and a picture can be any colour underneath.
        Ink.DrawScaledEdged(dl, size, at, this.DimInk(Tokens.Col.HudInk), text, TextEdge.Outline);
    }

    /// <summary>
    /// The seconds, or the minutes once there are too many seconds to read.
    /// <para>
    /// Out of two tables built once, because this runs per icon per frame and building
    /// "37" would allocate every one of them (CLAUDE.md §7.1). Null for an effect that does
    /// not run out — there is no number to write for something that is simply there.
    /// </para>
    /// </summary>
    private static string? DurationText(float remaining)
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

    /// <summary>
    /// How many of it there are, astride the bottom edge of its icon.
    /// <para>
    /// 🔴 It used to sit in the bottom right corner, inside the icon — which is the corner
    /// the duration reaches toward, so with both numbers on the two collided and neither
    /// could be read (Florian, 2026-09-21, at a twenty pixel icon). Two numbers do not fit
    /// on a twenty pixel square side by side; one of them has to leave, and this is the one
    /// that can. It is a small number that rarely changes, while the duration is the one
    /// being watched — so the duration keeps the icon and the count moves to the edge.
    /// </para>
    /// <para>
    /// ⚠️ Half of it therefore hangs BELOW the icon, over whatever is underneath: the frame
    /// on the bottom row, and nothing at all past the frame's edge. The ordinary outline
    /// carries it — a thicker one was tried and taken straight back out (see
    /// <see cref="AuraStackRatio"/>).
    /// </para>
    /// </summary>
    private void DrawStacks(ImDrawListPtr dl, Vector2 min, Vector2 max, ushort stacks)
    {
        string text = StackText[Math.Min((int)stacks, StackText.Length) - 1];

        // A share of the icon, so the number scales with whatever size the icons are set to
        // rather than staying put and swallowing a small one.
        float size = this.AuraNumberHeight(max.Y - min.Y, AuraStackRatio);
        float width = Ink.MeasureWidth(size, text);

        Vector2 at = new(
            MathF.Round(((min.X + max.X) * 0.5f) - (width * 0.5f)),
            MathF.Round(max.Y - (size * 0.5f)));

        // Always outlined, whatever the frame's own text edge is set to. This one sits on a
        // picture rather than on a bar, and a picture can be any colour underneath.
        Ink.DrawScaledEdged(dl, size, at, this.DimInk(Tokens.Col.HudInk), text, TextEdge.Outline);
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
    /// How tall a number on an effect icon comes out: the player's share of the icon, times
    /// whatever this particular number's ratio to it is, never below the floor.
    /// <para>
    /// One place, because the two numbers have to agree — they sit on the same icon in the
    /// same typeface, and a rule applied twice is a rule that drifts apart once.
    /// </para>
    /// </summary>
    private float AuraNumberHeight(float iconHeight, float ratio) => MathF.Max(
        Tokens.WorldPx(AuraStackMinSize),
        MathF.Round(iconHeight * m_config.PartyFrames.AuraNumberSize * ratio));

    /// <summary>
    /// How the stack count relates to the duration. A shade under it: it is the lesser of
    /// the two numbers, and half of it is out in the open where a tall glyph reads bigger
    /// than it measures.
    /// <para>
    /// Fixed, while the size itself is the player's. Nobody wants the seconds large and the
    /// stack count small — one slider moves both and this holds them in step.
    /// </para>
    /// <para>
    /// 🔴 Size was the whole answer. Both numbers were given a two pixel outline at the same
    /// time, on the thought that a number lying over artwork needs more of an edge — and at
    /// twelve pixels of text a two pixel edge is nearly as thick as the strokes themselves,
    /// so it closed up the eye of a 9 and the bowls of a 3 and the numbers came out worse
    /// than before (Florian, 2026-09-21, one round after asking for it). The size-based rule
    /// in <c>Ink.EdgeWidth</c> already thickens on its own past twenty-eight pixels, which
    /// is where these numbers land at a large icon and exactly where a second pixel helps.
    /// <b>An edge is a share of the stroke it surrounds, not a constant.</b>
    /// </para>
    /// </summary>
    private const float AuraStackRatio = 0.93f;

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
        // Whichever effect is actually on them, so the picture is the game's own for it and
        // there is nothing of ours to keep in step with a patch.
        //
        // 🔴 Each is asked about its OWN switch, and the fall-through matters: somebody who is
        // invulnerable AND has a raise in flight, with the invulnerability mark turned off,
        // shows the raise rather than nothing. Written as one test on the pair — pick the
        // effect first, then ask whether it may be drawn — that case would go blank, which is
        // the one outcome neither switch was asked for.
        // The group switch, and then the two inside it. The master is what the eye and the
        // card head both speak for; the pair below says which of the two situations is worth
        // an icon.
        if (!cfg.ShowRescueIcon || !this.Shows(PreviewPart.RescueIcon))
        {
            return;
        }

        uint status =
            member.InvulnerableStatus != 0 && cfg.ShowInvulnIcon ? member.InvulnerableStatus
            : member.RaiseRemaining > 0f && cfg.ShowRaiseIcon ? StatusData.Raise
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

        float side = Tokens.WorldPx(cfg.RescueIconSize);
        Vector2 at = Anchors.Place(
            Anchors.At(cfg.RescueIconPosition),
            innerMin,
            innerMax,
            new Vector2(side, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.WorldPx(cfg.RescueIconX);
        at.Y += Tokens.WorldPx(cfg.RescueIconY);

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

        float side = Tokens.WorldPx(size);
        Vector2 at = Anchors.Place(
            Anchors.At(anchor),
            innerMin,
            innerMax,
            new Vector2(side, side),
            Tokens.Metric.FramePadding);

        at.X += Tokens.WorldPx(offsetX);
        at.Y += Tokens.WorldPx(offsetY);

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

        if (cfg.ShowName && this.Shows(PreviewPart.Name))
        {
            string name = this.DrawnName(slot, ref member, PlayerName.At(cfg.NameShortening));
            float size = Tokens.WorldPx(cfg.NameSize);
            Vector2 measured = new(Ink.MeasureWidth(size, name), size);
            Vector2 at = Anchors.Place(Anchors.At(cfg.NamePosition), innerMin, innerMax, measured, padding);

            at.X += Tokens.WorldPx(cfg.NameX);
            at.Y += Tokens.WorldPx(cfg.NameY);

            // Your own name is drawn like everyone else's. It used to come out gold, which
            // looked like a state rather than a whose-name-is-this, and the one frame you
            // never have to search for is your own (Florian, 2026-09-12).
            // 🔴 A dimmed frame gets darker text, not just fainter text. White at two thirds
            // opacity is still white, and on a frame that has stepped back the name was the
            // one thing still shouting (Florian, 2026-09-12).
            uint colour = this.DimInk(cfg.NameInJobColour
                ? Jobs.Colour(member.JobId)
                : (member.HasData ? Tokens.Col.HudInk : Tokens.Col.HudInkQuiet));

            Ink.DrawScaledEdged(dl, size, at, colour, name, cfg.Edge);
        }

        if (cfg.ShowPartyNumber && this.Shows(PreviewPart.PartyNumber) && member.PartyNumber >= 1 && member.PartyNumber <= NumberText.Length)
        {
            // On a rounded plate with a black edge, the way the game's own party list puts a
            // position. A bare digit over a health bar has no shape of its own to be
            // recognised by and reads as a stray number (Florian).
            string number = NumberText[member.PartyNumber - 1];
            float glyph = Tokens.WorldPx(cfg.PartyNumberSize);
            float plate = MathF.Round(glyph * NumberPlateScale);
            Vector2 plateAt = Anchors.Place(
                Anchors.At(cfg.PartyNumberPosition),
                innerMin,
                innerMax,
                new Vector2(plate, plate),
                padding);

            plateAt.X += Tokens.WorldPx(cfg.PartyNumberX);
            plateAt.Y += Tokens.WorldPx(cfg.PartyNumberY);

            // The edge is a filled shape with the plate laid inside it, NOT a stroke. ImGui
            // centres a stroke on its path, so half of every edge pixel falls outside the
            // rectangle and gets antialiased — which is exactly why the window's own rings are
            // filled rectangles too (design bible §2.1, learned the same way in session 5).
            Vector2 plateEnd = new(plateAt.X + plate, plateAt.Y + plate);
            float edge = MathF.Max(Tokens.WorldLine(1f), MathF.Round(plate * NumberPlateEdge));
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

        if (!cfg.ShowHealthText || !this.Shows(PreviewPart.HealthText))
        {
            return;
        }

        string health = this.HealthFigure(slot, ref member, textMode);
        if (health.Length == 0)
        {
            return;
        }

        float healthSize = Tokens.WorldPx(cfg.HpTextSize);
        Vector2 healthMeasured = new(Ink.MeasureWidth(healthSize, health), healthSize);
        Vector2 healthAt = Anchors.Place(Anchors.At(cfg.HpTextPosition), innerMin, innerMax, healthMeasured, padding);

        healthAt.X += Tokens.WorldPx(cfg.HpTextX);
        healthAt.Y += Tokens.WorldPx(cfg.HpTextY);

        Ink.DrawScaledEdged(dl, healthSize, healthAt, this.DimInk(Tokens.Col.HudInk), health, cfg.Edge);
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
