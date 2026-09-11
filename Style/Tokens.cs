using System;
using System.Numerics;
using Dalamud.Interface.GameFonts;

namespace WispUI.Style;

/// <summary>
/// The single source for every colour, measurement and font size WispUI draws.
/// UI code never writes a raw number, it asks for a token — that is what lets one
/// scale factor reach the whole suite, and what keeps a palette change a one-file edit.
/// <para>
/// Base values are stated at scale 1.0. Anything that reaches the screen goes through
/// <see cref="Px"/> or <see cref="Line"/>, which snap to whole pixels: half pixels are
/// the reason edges look soft.
/// </para>
/// </summary>
internal static class Tokens
{
    private const float MinScale = 0.5f;
    private const float MaxScale = 2.0f;

    /// <summary>The interface scale in use. Set once at load and whenever the user changes it.</summary>
    public static float Scale { get; private set; } = 1f;

    public static void SetScale(float scale) => Scale = Math.Clamp(scale, MinScale, MaxScale);

    /// <summary>Scales a base value and snaps it to a whole pixel.</summary>
    public static float Px(float baseValue) => MathF.Round(baseValue * Scale);

    /// <summary>Scales a base pair and snaps both parts to whole pixels.</summary>
    public static Vector2 Px(float x, float y) => new(Px(x), Px(y));

    /// <summary>Like <see cref="Px"/>, but never thinner than one pixel — for borders and hairlines.</summary>
    public static float Line(float baseValue) => MathF.Max(1f, MathF.Round(baseValue * Scale));

    /// <summary>
    /// Packs an <c>0xRRGGBB</c> literal into the ABGR word ImGui draws with, fully opaque.
    /// Only this file may call it — every colour is a named token below.
    /// </summary>
    private static uint Rgb(uint hex) =>
        0xFF000000u | ((hex & 0x0000FFu) << 16) | (hex & 0x00FF00u) | ((hex & 0xFF0000u) >> 16);

    /// <summary>
    /// The palette. Values marked MEASURED were picked out of FFXIV's own window with a
    /// dropper (Florian, 2026-09-10) and are not to be "improved" by eye. The rest are
    /// derived from them and stay on the same neutral hue — those are the ones to question
    /// if something looks off.
    /// <para>
    /// The measurement overturned an assumption: the panel, the title bar and the nav rail are
    /// all the SAME colour in the game. FFXIV separates its regions with
    /// lines, not with shades, and there is no gradient on the title bar.
    /// </para>
    /// </summary>
    public static class Col
    {
        // --- chrome: one flat surface, MEASURED #232223 ---
        // Kept as separate tokens on purpose. They happen to be equal today; giving a region
        // its own shade later is then a one-line change here rather than a change in the code.
        public static readonly uint Panel = Rgb(0x232223);
        public static readonly uint Rail = Rgb(0x232223);
        public static readonly uint TitleBar = Rgb(0x232223);

        /// <summary>
        /// A settings group sits one small step above the window (derived, +5 on the measured
        /// surface). The game itself separates with lines only, and the frame still does the
        /// work; this is just enough lift that a group reads as an object on the panel rather
        /// than as an outline drawn onto it. LumenUI takes a larger step (+9) — deliberately
        /// not copied, because there the panel is near-black and has the room for it.
        /// </summary>
        public static readonly uint GroupBg = Rgb(0x282728);

        // Structure comes from these, since the surfaces no longer carry it.
        public static readonly uint EdgeDim = Rgb(0x4A474A);
        public static readonly uint Hairline = Rgb(0x3A383A);

        /// <summary>
        /// Between two compact options inside a group. Fainter than <see cref="Hairline"/> on
        /// purpose: at the same strength it would compete with the group frame, and a line
        /// inside an object must never read as loud as the line around it.
        /// <para>
        /// Lifted a step towards the hairline (was 0x2E2C2E): a divider that only just clears
        /// the surface is gone on a bright screen, and a line nobody can see is not a quiet
        /// line, it is a missing one.
        /// </para>
        /// </summary>
        public static readonly uint RowDivider = Rgb(0x343234);

        // --- controls (derived: the old values neutralised onto the measured hue) ---
        public static readonly uint Control = Rgb(0x3A383A);
        public static readonly uint Control2 = Rgb(0x2E2C2E);
        public static readonly uint ControlEdge = Rgb(0x6B676B);
        public static readonly uint ButtonTop = Rgb(0x464346);
        public static readonly uint ButtonBottom = Rgb(0x353335);
        public static readonly uint Input = Rgb(0x1B1A1B);

        // --- floating layers (derived) ---

        /// <summary>
        /// A popup list or panel. Darker than the surface it covers rather than lighter: a
        /// floating layer that sits at the same brightness as the window disappears into it,
        /// and the window has no shadow to fall back on.
        /// </summary>
        public static readonly uint PopupBg = Rgb(0x171617);

        /// <summary>
        /// The edge of a floating layer. Brighter than the hairlines inside the window on
        /// purpose — it is the one line that says where the layer ends.
        /// </summary>
        public static readonly uint PopupEdge = Rgb(0x6B676B);

        /// <summary>
        /// A box you type into, when what is behind it is already dark — the search field in
        /// a popup list.
        /// <para>
        /// It is LIGHTER than its surroundings, which is the opposite of <see cref="Input"/>,
        /// and that is the point. LumenUI's rule, arrived at the hard way: what has to stay
        /// constant is the STEP over a field's own background, not the colour — perceived lift
        /// is relative, so one absolute value cannot work across surfaces that span from
        /// #171617 to #232223. Aim for +8 to +11. Our search box was <see cref="Input"/> at
        /// #1B1A1B inside a #171617 popup: a step of four, which is nearly nothing, and
        /// exactly the case Lumen ran into when a menu and the box in it shared a value.
        /// </para>
        /// </summary>
        public static readonly uint FieldOnDark = Rgb(0x222122);

        // --- nav (derived) ---
        public static readonly uint NavHover = Rgb(0x2A282A);
        public static readonly uint NavSelected = Rgb(0x2E2C2E);
        public static readonly uint NavCard = Rgb(0x2A282A);
        public static readonly uint NavCardEdge = Rgb(0x3F3C3F);

        // --- text ---
        /// <summary>Body copy and option labels. MEASURED.</summary>
        public static readonly uint Ink = Rgb(0xC3C3C3);

        /// <summary>
        /// Section and category headings. MEASURED — this warm beige is what the game puts on
        /// its category lines, and it reads as a heading precisely because it is warmer than
        /// the body copy rather than brighter.
        /// </summary>
        public static readonly uint Heading = Rgb(0xB5AA92);

        // Derived from the measured body colour.
        public static readonly uint InkDim = Rgb(0x8E8E8E);
        public static readonly uint InkFaint = Rgb(0x6E6E6E);
        public static readonly uint InkOnGold = Rgb(0x1B1A1B);

        // --- accent: FFXIV's own gold, not a brand of ours ---
        public static readonly uint Gold = Rgb(0xD8B567);
        public static readonly uint GoldHi = Rgb(0xEFD58F);
        public static readonly uint GoldDim = Rgb(0x8D7539);
        public static readonly uint GoldSwitchTrack = Rgb(0x4B421F);

        /// <summary>
        /// The window edge, MEASURED pixel by pixel off the game's own frame: four rings,
        /// listed outermost first. The top edge carries the highlight (that near-white second
        /// pixel is what makes the frame read as lit from above); the sides are their own
        /// sequence, and the bottom runs the side sequence reversed, so the light stays up top.
        /// </summary>
        public static readonly uint[] EdgeTop =
        {
            Rgb(0x4C443E), Rgb(0xCFD0AD), Rgb(0x674F22), Rgb(0x0F0000),
        };

        public static readonly uint[] EdgeSide =
        {
            Rgb(0x3D2F2D), Rgb(0x856C49), Rgb(0x6E582E), Rgb(0x200B06),
        };

        public static readonly uint[] EdgeBottom =
        {
            Rgb(0x200B06), Rgb(0x6E582E), Rgb(0x856C49), Rgb(0x3D2F2D),
        };

        /// <summary>The lit top of the title bar, MEASURED. It fades down into <see cref="TitleBar"/>.</summary>
        public static readonly uint TitleBarTop = Rgb(0x454445);

        /// <summary>
        /// The three-pixel rule under the title bar, MEASURED top to bottom: a dark line, the
        /// surface colour, then a light line. It fades out towards the corners.
        /// </summary>
        public static readonly uint[] TitleRule =
        {
            Rgb(0x0D0D0D), Rgb(0x232223), Rgb(0x454445),
        };

        // --- slider ---
        // FFXIV fills its own sliders green rather than in the gold it uses for ticks and
        // arrows, and WispUI follows that. These greens are read by eye from the game and
        // are the first values to correct if they sit wrong next to it.
        /// <summary>The unfilled part of a slider — darker than a text field, so the fill reads clearly.</summary>
        public static readonly uint SliderTrackBg = Rgb(0x131213);

        public static readonly uint SliderFill = Rgb(0x7FA84A);
        public static readonly uint SliderFillHi = Rgb(0x9DC85F);
        /// <summary>
        /// The knob, as a spun metal disc rather than a flat white dot: ground in circles, so
        /// the sheen wanders around the face and converges in the middle. The two tones are
        /// the light and dark of that grind — derived; the finish itself is what the game's
        /// own knob shows under magnification.
        /// </summary>
        public static readonly uint SliderGrab = Rgb(0xE6E4DE);

        public static readonly uint SliderGrabMill = Rgb(0x9B9891);
        public static readonly uint SliderGrabCore = Rgb(0xBDBAB3);
        public static readonly uint SliderGrabEdge = Rgb(0x2B2927);

        // --- scrollbar ---
        public static readonly uint ScrollTrack = Rgb(0x1B1A1B);
        public static readonly uint ScrollGrab = Rgb(0x4E4B4E);
        public static readonly uint ScrollGrabHover = Rgb(0x605D60);

        /// <summary>Everything disabled is drawn at this strength, nothing invents its own.</summary>
        public const float DisabledAlpha = 0.45f;

        public static uint Faded(uint colour, float alpha) =>
            (colour & 0x00FFFFFFu) | ((uint)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f) << 24);

        /// <summary>
        /// Blends two packed colours channel by channel. Used where one run of the window edge
        /// hands over to the next: at a hard swap the corner shows a seam, and the eye finds a
        /// seam on a curve faster than anywhere else.
        /// </summary>
        public static uint Mix(uint from, uint to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            uint result = 0u;
            for (int shift = 0; shift < 32; shift += 8)
            {
                float a = (from >> shift) & 0xFFu;
                float b = (to >> shift) & 0xFFu;
                result |= (uint)MathF.Round(a + ((b - a) * t)) << shift;
            }

            return result;
        }
    }

    /// <summary>The spacing ladder. No free in-between values.</summary>
    public static class Space
    {
        public static float Xs => Px(2f);
        public static float Sm => Px(4f);
        public static float Md => Px(8f);
        public static float Lg => Px(12f);
        public static float Xl => Px(16f);
    }

    /// <summary>
    /// A small, fixed set of radii — not invented per widget. They step down from the outside
    /// in, so each shell is a little rounder than what sits inside it.
    /// </summary>
    public static class Radius
    {
        /// <summary>
        /// The window edge. The four measured rings ARE bendable: each ring is stroked as a
        /// path whose top and bottom carry their own corner arcs, so the measured colour
        /// sequence survives and the corner takes the colour of the horizontal edge — the same
        /// approximation the square version already made.
        /// <para>
        /// The honest cost is antialiasing: a straight line lands on whole pixels, an arc does
        /// not, so four one-pixel rings blur into each other along the curve. FFXIV's own
        /// corners read darker for exactly that reason (Florian, 2026-09-11), which is why
        /// this is the game's look rather than a defect of ours.
        /// </para>
        /// </summary>
        public static float Window => Px(8f);

        /// <summary>What is left of the window radius once the frame has taken its four pixels.</summary>
        public static float WindowInner => MathF.Max(0f, Window - Metric.WindowBorder);

        /// <summary>The frame around a settings group — between the window and a control.</summary>
        public static float Group => Px(6f);

        /// <summary>
        /// Control faces: buttons, tabs, the selector, a popup.
        /// <para>
        /// Raised from 3. LumenUI's tested ladder runs chrome : card : control at
        /// 1 : 0.78 : 0.61, and it got there by bumping the control radius up twice, because
        /// a square-ish face inside a rounded shell reads as a part from another kit. Ours sat
        /// at 0.375 of the window radius; five puts it at 0.63.
        /// </para>
        /// </summary>
        public static float Control => Px(5f);

        /// <summary>Tick boxes, badges, the bar preview. Same step below Control that Lumen uses.</summary>
        public static float Small => Px(3f);
    }

    /// <summary>
    /// Font sizes by role, never by number. The code asks for <c>Title</c>, not for "15 px".
    /// </summary>
    public static class FontRole
    {
        // Axis is a bitmap face: it is only sharp at the exact sizes it ships in, and every
        // other size is a rescale of one of them. So a role names a SHIPPED STEP, never a
        // pixel count.
        //
        // The step names are POINT sizes, not pixels — Dalamud converts with *4/3:
        //   Axis96 = 9.6pt = 12.8px · Axis12 = 12pt = 16px · Axis14 = 14pt = 18.7px · Axis18 = 18pt = 24px
        //
        // Asking for "14 pixels" therefore picked Axis12 and shrank it, which came out both
        // smaller and softer than the game's own text. Naming the step avoids that entirely.
        /// <summary>
        /// The name of the screen you are on, in its header. The only role NOT on a shipped
        /// size: Axis18 is 24 px, which read as too big and too soft, and there is no step
        /// between it and 18.7 px. So it is Axis18 brought down to 22 px — a mild reduction,
        /// and the one place where the sharpness rule is knowingly traded for the right size.
        /// </summary>
        public const GameFontFamilyAndSize ScreenTitle = GameFontFamilyAndSize.Axis18;
        public const float ScreenTitlePx = 22f;

        /// <summary>The window title and section headings. Native 18.7 px.</summary>
        public const GameFontFamilyAndSize Title = GameFontFamilyAndSize.Axis14;
        public const float TitlePx = Native;

        /// <summary>Body copy, labels, buttons, navigation, tabs. Native 16 px.</summary>
        public const GameFontFamilyAndSize Body = GameFontFamilyAndSize.Axis12;
        public const float BodyPx = Native;

        /// <summary>Badges and the version chip — the only things smaller than body. Native 12.8 px.</summary>
        public const GameFontFamilyAndSize Small = GameFontFamilyAndSize.Axis96;
        public const float SmallPx = Native;

        /// <summary>Use the step's own pixel size, which is the sharp case.</summary>
        public const float Native = 0f;
    }

    /// <summary>Window and chrome measurements, all stated at scale 1.0.</summary>
    public static class Metric
    {
        public static float WindowWidth => Px(920f);
        public static float WindowHeight => Px(640f);

        /// <summary>The window edge: four rings, as measured off the game's own frame.</summary>
        public static float WindowBorder => Line(4f);

        /// <summary>The rule under the title bar. Three pixels, each its own colour.</summary>
        public static float TitleRuleHeight => Line(3f);

        /// <summary>How far the title rule fades out before it reaches the corners.</summary>
        public static float TitleRuleFade => Px(28f);

        public static float TitleBarHeight => Px(42f);

        /// <summary>
        /// How far down the lit top edge of the title bar reaches before it has arrived at the
        /// surface colour. The game lights the very top and is back to its background well
        /// before the title text; a wash across the whole bar reads as a panel of its own.
        /// </summary>
        public static float TitleBarFade => Px(14f);

        public static float TitleButton => Px(20f);

        public static float NavWidth => Px(205f);
        public static float NavItemHeight => Px(35f);
        public static float NavIndent => Px(16f);
        public static float NavAccent => Line(2f);
        public static float NavCardHeight => Px(64f);
        public static float NavButtonHeight => Px(34f);
        public static float NavVersionHeight => Px(24f);

        public static float TabHeight => Px(31f);
        public static float TabPaddingX => Px(18f);
        public static float TabGap => Px(2f);

        public static float ModuleHeaderHeight => Px(42f);

        public static float ButtonHeight => Px(29f);
        public static float ButtonPaddingX => Px(12f);

        public static float SwitchWidth => Px(34f);
        public static float SwitchHeight => Px(17f);
        public static float SwitchKnob => Px(13f);

        public static float CheckBox => Px(15f);

        public static float SliderHeight => Px(20f);
        public static float SliderTrack => Px(6f);
        public static float SliderGrabRadius => Px(7f);

        // --- the arrow selector, the one selection widget in the suite ---

        /// <summary>
        /// The box every wide control sits in: selector, slider, and whatever comes after
        /// them. A control shorter than this — a slider track is — is centred inside it rather
        /// than sitting at the top.
        /// <para>
        /// This is what keeps two columns in step. With each control setting its own height,
        /// the rows under the first one drift apart the moment a slider stands beside a
        /// selector; with one box, every field row is the same height and the labels beneath
        /// keep meeting across the grid.
        /// </para>
        /// <para>
        /// 28: 16 px of text with five pixels of air above and below and a pixel of border.
        /// One less than a button on purpose — the two never stand side by side, buttons live
        /// in the screen header — and this way the inner spacing is symmetrical, which a
        /// bitmap face shows.
        /// </para>
        /// </summary>
        public static float FieldControlHeight => Px(28f);

        /// <summary>
        /// The width of one arrow. Wide enough to hit while clicking quickly: the whole
        /// button is the target, not the triangle drawn on it.
        /// </summary>
        public static float SelectorArrow => Px(26f);

        public static float SelectorPaddingX => Px(8f);

        /// <summary>
        /// The live preview inside the face. With textures this preview, not the name, is
        /// the actual point of the interaction.
        /// </summary>
        public static Vector2 SelectorSwatch => Px(60f, 13f);

        /// <summary>The triangle drawn on an arrow button.</summary>
        public static float SelectorGlyph => Px(7f);

        /// <summary>The fold-away triangle in a group head.</summary>
        public static float CollapseGlyph => Px(12f);

        // --- the popup list an arrow selector can open ---
        public static float PopupGap => Px(3f);
        public static float PopupPadding => Px(6f);
        /// <summary>
        /// A row of the popup list. Raised from 24: LumenUI went 30 → 35 → 40 screen px on
        /// its own list rows because cramped ones were rejected twice, and 24 sat below even
        /// the first value it threw out. 28 leaves six pixels of air around a 16 px line.
        /// </summary>
        public static float PopupRowHeight => Px(28f);
        public static float PopupSearchHeight => Px(26f);

        /// <summary>How many rows the list shows before it starts to scroll.</summary>
        public const int PopupRows = 7;

        /// <summary>The swatch in a popup row, smaller than the one in the face.</summary>
        public static Vector2 PopupSwatch => Px(44f, 11f);

        // --- the appearance clipboard ---
        public static float PastePanelWidth => Px(268f);
        public static float PastePanelPadding => Px(10f);

        /// <summary>One tick box in the paste panel to the next.</summary>
        public static float FieldGap => Px(9f);


        /// <summary>
        /// Settings are laid out on a two-column grid. A control fills one column and never
        /// the whole width: a slider stretched across a wide window is unusable and looks it.
        /// <para>
        /// Field controls (sliders, selectors) take one column each, two to a row, and compact
        /// options (check boxes) do the same. Only a section head and the rule between two
        /// sections run the full width. Nothing stretches and nothing shrinks — a control that
        /// does not fit goes on the next row.
        /// </para>
        /// </summary>
        public const int Columns = 2;

        /// <summary>
        /// Between the two columns, and between one group and the one under it — the same
        /// value in both directions, because a grid of framed boxes shows an uneven gap at
        /// once. It came down from 28 when groups arrived: bare controls side by side needed
        /// the air, framed ones already have their own edge.
        /// </summary>
        public static float ColumnGutter => Px(16f);

        // --- settings groups ---
        // A group is the hairline in a second shape: as a stroke it separates, as a frame it
        // gathers. That keeps the window at two kinds of divider rather than gaining a third,
        // and it needs no surface colour of its own — which the measured palette does not have.

        /// <summary>
        /// Inside the group frame, on every side. Raised from 12 to match the proportion
        /// LumenUI arrived at after testing: its card padding is 24 screen px against a 41 px
        /// control, ours is 16 against 28 — the same ratio, at our density.
        /// </summary>
        public static float GroupPadding => Px(16f);

        /// <summary>The group head to the rule under it.</summary>
        public static float GroupHeadGap => Px(12f);

        /// <summary>The rule to the first row.</summary>
        public static float GroupRuleGap => Px(16f);

        /// <summary>
        /// A row that holds one compact control: label on the left, the control hard against
        /// the right edge of the group. Same height as a selector, so two of them beside a
        /// field row still line up.
        /// </summary>
        public static float OptionRowHeight => Px(28f);

        // --- the vertical rhythm of a settings screen ---
        // Four values, and every block advances by its own MEASURED height plus one of them.
        // The head gap is the larger one on purpose: a section head is set off from its
        // controls by space rather than by a line, because the window has exactly two kinds of
        // divider and neither of them belongs there.

        /// <summary>Section head to its first row.</summary>
        public static float SectionHeadGap => Px(20f);

        // The gap between two rows is chosen by their RELATIONSHIP, not by feel — LumenUI's
        // semantic rhythm, which it arrived at in play. The principle behind it: a jump in
        // height needs more air than two rows of the same shape.

        /// <summary>The standard gap between two rows of the same kind.</summary>
        public static float RowGap => Px(16f);

        /// <summary>
        /// From a run of compact options to the first field row under it. A short control
        /// followed by a tall one needs more room, or the tall one reads as crowding it.
        /// </summary>
        public static float RowGapAfterShort => Px(24f);

        /// <summary>Air under the last row, so a scrolled screen does not end flush with the edge.</summary>
        public static float ContentPaddingBottom => Px(24f);

        public static float BadgeHeight => Px(18f);
        public static float BadgePaddingX => Px(6f);

        /// <summary>
        /// How wide a tooltip may run before it wraps. Without a wrap width ImGui sets a
        /// tooltip as a single line, which on a wide screen reaches right across the game.
        /// </summary>
        public static float TooltipWrap => Px(280f);

        public static float ScrollbarWidth => Px(11f);
        public static float SectionPaddingX => Px(16f);
        public static float SectionPaddingY => Px(16f);
    }
}
