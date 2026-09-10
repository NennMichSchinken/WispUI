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
        public static readonly uint PanelSoft = Rgb(0x232223);
        public static readonly uint Rail = Rgb(0x232223);
        public static readonly uint TitleBar = Rgb(0x232223);

        // Structure comes from these, since the surfaces no longer carry it.
        public static readonly uint EdgeDim = Rgb(0x4A474A);
        public static readonly uint Hairline = Rgb(0x3A383A);

        // --- controls (derived: the old values neutralised onto the measured hue) ---
        public static readonly uint Control = Rgb(0x3A383A);
        public static readonly uint Control2 = Rgb(0x2E2C2E);
        public static readonly uint ControlEdge = Rgb(0x6B676B);
        public static readonly uint ButtonTop = Rgb(0x464346);
        public static readonly uint ButtonBottom = Rgb(0x353335);
        public static readonly uint Input = Rgb(0x1B1A1B);

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
        public static readonly uint SliderFill = Rgb(0x7FA84A);
        public static readonly uint SliderFillHi = Rgb(0x9DC85F);
        public static readonly uint SliderGrab = Rgb(0xE4E7EA);
        public static readonly uint SliderGrabHover = Rgb(0xFFFFFF);

        // --- scrollbar ---
        public static readonly uint ScrollTrack = Rgb(0x1B1A1B);
        public static readonly uint ScrollGrab = Rgb(0x4E4B4E);
        public static readonly uint ScrollGrabHover = Rgb(0x605D60);

        /// <summary>Everything disabled is drawn at this strength, nothing invents its own.</summary>
        public const float DisabledAlpha = 0.45f;

        public static uint Faded(uint colour, float alpha) =>
            (colour & 0x00FFFFFFu) | ((uint)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f) << 24);

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

    /// <summary>A small, fixed set of radii — not invented per widget.</summary>
    public static class Radius
    {
        /// <summary>
        /// Square. The game's frame is four rings of measured pixels with a different sequence
        /// per edge, and that cannot be bent around a corner — nor does the game bend it.
        /// </summary>
        public static float Window => 0f;

        public static float Control => Px(3f);
        public static float Small => Px(2f);
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
        /// <summary>The name of the screen you are on, in its header. 24 px.</summary>
        public const GameFontFamilyAndSize ScreenTitle = GameFontFamilyAndSize.Axis18;

        /// <summary>The window title and section headings. 18.7 px.</summary>
        public const GameFontFamilyAndSize Title = GameFontFamilyAndSize.Axis14;

        /// <summary>Body copy, labels, buttons, navigation, tabs. 16 px.</summary>
        public const GameFontFamilyAndSize Body = GameFontFamilyAndSize.Axis12;

        /// <summary>Badges and the version chip — the only things smaller than body. 12.8 px.</summary>
        public const GameFontFamilyAndSize Small = GameFontFamilyAndSize.Axis96;
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
        public static float SliderGrabRadius => Px(8f);


        public static float RowHeight => Px(30f);

        /// <summary>
        /// Settings are laid out on a two-column grid. A control fills one column and never
        /// the whole width: a slider stretched across a wide window is unusable and looks it.
        /// </summary>
        public const int Columns = 2;

        public static float ColumnGutter => Px(28f);

        public static float BadgeHeight => Px(18f);
        public static float BadgePaddingX => Px(6f);

        public static float ScrollbarWidth => Px(11f);
        public static float SectionPaddingX => Px(16f);
        public static float SectionPaddingY => Px(16f);
    }
}
