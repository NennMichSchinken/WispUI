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
    /// The palette, read off FFXIV's own system configuration window. Neutral grey with no
    /// blue cast: hue is carried by the gold accent and the role colours, never by the chrome.
    /// </summary>
    public static class Col
    {
        // --- chrome ---
        public static readonly uint Rail = Rgb(0x1A1D20);
        public static readonly uint Panel = Rgb(0x232629);
        public static readonly uint PanelSoft = Rgb(0x2A2E32);
        public static readonly uint TitleBar = Rgb(0x33383D);
        public static readonly uint TitleBarHi = Rgb(0x3E4348);
        public static readonly uint FooterHi = Rgb(0x2B2F33);
        public static readonly uint EdgeDim = Rgb(0x4E5358);
        public static readonly uint Hairline = Rgb(0x34383C);

        // --- controls ---
        public static readonly uint Control = Rgb(0x3A3F45);
        public static readonly uint Control2 = Rgb(0x2E3338);
        public static readonly uint ControlEdge = Rgb(0x666C72);
        public static readonly uint ControlHover = Rgb(0x464C53);
        public static readonly uint ButtonTop = Rgb(0x4A5057);
        public static readonly uint ButtonBottom = Rgb(0x383D42);
        public static readonly uint Input = Rgb(0x1C1F22);

        // --- nav ---
        public static readonly uint NavHover = Rgb(0x21252A);
        public static readonly uint NavSelected = Rgb(0x262B30);
        public static readonly uint NavCard = Rgb(0x24282C);
        public static readonly uint NavCardEdge = Rgb(0x3B4146);

        // --- text ---
        public static readonly uint Ink = Rgb(0xE9E7E0);
        public static readonly uint InkDim = Rgb(0xA6A9AC);
        public static readonly uint InkFaint = Rgb(0x74787C);
        public static readonly uint InkOnGold = Rgb(0x1B1D1F);

        // --- accent: FFXIV's own gold, not a brand of ours ---
        public static readonly uint Gold = Rgb(0xD8B567);
        public static readonly uint GoldHi = Rgb(0xEFD58F);
        public static readonly uint GoldDim = Rgb(0x8D7539);
        public static readonly uint GoldSwitchTrack = Rgb(0x4B421F);

        /// <summary>The window edge: warm gold, subtle, one pixel, drawn as the topmost layer.</summary>
        public static readonly uint WindowEdge = Rgb(0xB9A06A);

        // --- slider ---
        // FFXIV fills its own sliders green rather than in the gold it uses for ticks and
        // arrows, and WispUI follows that. These greens are read by eye from the game and
        // are the first values to correct if they sit wrong next to it.
        public static readonly uint SliderFill = Rgb(0x7FA84A);
        public static readonly uint SliderFillHi = Rgb(0x9DC85F);
        public static readonly uint SliderGrab = Rgb(0xE4E7EA);
        public static readonly uint SliderGrabHover = Rgb(0xFFFFFF);

        // --- scrollbar ---
        public static readonly uint ScrollTrack = Rgb(0x1B1E21);
        public static readonly uint ScrollGrab = Rgb(0x52585E);
        public static readonly uint ScrollGrabHover = Rgb(0x63696F);

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
        public static float Window => Px(6f);
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
        public const GameFontFamilyAndSize Title = GameFontFamilyAndSize.Axis14;
        public const GameFontFamilyAndSize Body = GameFontFamilyAndSize.Axis12;
        public const GameFontFamilyAndSize Small = GameFontFamilyAndSize.Axis96;
    }

    /// <summary>Window and chrome measurements, all stated at scale 1.0.</summary>
    public static class Metric
    {
        public static float WindowWidth => Px(920f);
        public static float WindowHeight => Px(640f);

        /// <summary>The window edge. Two pixels, to sit at the weight FFXIV's own frames have.</summary>
        public static float WindowBorder => Line(2f);

        public static float TitleBarHeight => Px(42f);
        public static float TitleButton => Px(20f);
        public static float FooterHeight => Px(50f);

        public static float NavWidth => Px(205f);
        public static float NavItemHeight => Px(35f);
        public static float NavIndent => Px(16f);
        public static float NavAccent => Line(2f);
        public static float NavCardHeight => Px(64f);
        public static float NavButtonHeight => Px(34f);
        public static float NavVersionHeight => Px(24f);

        public static float TabBarHeight => Px(42f);
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

        public static float BadgeHeight => Px(18f);
        public static float BadgePaddingX => Px(6f);

        public static float ScrollbarWidth => Px(11f);
        public static float SectionPaddingX => Px(16f);
        public static float SectionPaddingY => Px(16f);
    }
}
