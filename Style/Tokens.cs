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
    /// A value drawn on the game world, snapped to a whole pixel and <b>deliberately not
    /// scaled</b>.
    /// <para>
    /// 🔴 The scale under Global is the SUITE's scale — the settings window and nothing else.
    /// It used to reach the party frames too, and the clearest way to see why that is wrong
    /// is the slider itself: it says "170 px", and at 125 % the frame came out 212 wide. A
    /// number a player set in pixels, against a game interface that has its own scale, has to
    /// be that many pixels (Florian, 2026-09-21).
    /// </para>
    /// <para>
    /// So there are two worlds and one rule: the window uses <see cref="Px"/> and
    /// <see cref="Line"/>, anything drawn on the world uses this. A HUD element that reaches
    /// for <see cref="Px"/> is a bug, and it is a quiet one — it only shows up for somebody
    /// who moved the slider.
    /// </para>
    /// </summary>
    public static float WorldPx(float value) => MathF.Round(value);

    /// <summary>The same for a line on the world: never thinner than one pixel, never scaled.</summary>
    public static float WorldLine(float value) => MathF.Max(1f, MathF.Round(value));

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
        /// Lifted twice (0x2E2C2E, then 0x343234): a divider that only just clears the surface
        /// is gone on a bright screen, and a line nobody can see is not a quiet line, it is a
        /// missing one. The second lift came with the group's own lighter surface, which ate
        /// most of the first one — the step is over the GROUP colour now, not the panel's.
        /// </para>
        /// </summary>
        public static readonly uint RowDivider = Rgb(0x383638);

        // --- controls (derived: the old values neutralised onto the measured hue) ---
        public static readonly uint Control = Rgb(0x3A383A);
        public static readonly uint Control2 = Rgb(0x2E2C2E);
        public static readonly uint ControlEdge = Rgb(0x6B676B);

        /// <summary>The same edge, lit, for a control the mouse is on.</summary>
        public static readonly uint ControlEdgeHover = Rgb(0x9A949A);

        /// <summary>
        /// The two greys of the chequerboard behind a colour swatch. Without it a colour the
        /// user has made half transparent reads as a darker colour rather than as a see-through
        /// one, and they set it twice.
        /// </summary>
        public static readonly uint ChequerLight = Rgb(0x4A474A);

        public static readonly uint ChequerDark = Rgb(0x2E2C2E);
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

        /// <summary>
        /// A row in a list that is read rather than set — the release notes. The group
        /// surface, because a note is one thing on the page the same way a settings card is.
        /// </summary>
        public static readonly uint RowRest = Rgb(0x282728);

        /// <summary>
        /// The same row under the pointer. 🔴 Measured against RowRest and not against the
        /// page: the old hover colour was one step off the page behind it, which is
        /// invisible once the row has a surface of its own.
        /// </summary>
        public static readonly uint RowHover = Rgb(0x322F32);

        /// <summary>The pill on a release note: one step above the row it sits on.</summary>
        public static readonly uint PillRest = Rgb(0x332F33);

        /// <summary>The same pill while its row is hovered, so the whole row lifts together.</summary>
        public static readonly uint PillHover = Rgb(0x3D383D);

        /// <summary>Body text one step quieter than Ink, for a sentence nobody is hovering.</summary>
        public static readonly uint InkSoft = Rgb(0xA9A9A9);
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
        /// A frame in the world. Darker and more opaque than anything in the window: it lies
        /// over the game, not over a panel of ours, and has to hold its own against whatever
        /// is behind it.
        /// </summary>
        public static readonly uint FrameBg = 0xC8141314u;

        public static readonly uint FrameEdge = 0xC80A0A0Au;

        /// <summary>
        /// The ring around the frame under the mouse. White, because it has to read against a
        /// bar in any of the role or job colours and against the world behind an empty one —
        /// there is no tint that stays legible over all of them. The game's own party list
        /// answers the same question by brightening; a ring keeps the bar's colour honest,
        /// which matters more here because ours is a colour the player chose.
        /// </summary>
        public static readonly uint FrameHover = 0xD2FFFFFFu;

        /// <summary>
        /// The empty part of a bar that carries one of its own — the mana bar in its full
        /// shape. Darker than the frame behind it, so an empty bar still reads as a bar.
        /// </summary>
        public static readonly uint BarTrack = 0xC80D0C0Du;

        /// <summary>
        /// Mana. ⚠️ NOT MEASURED — picked by hand as a calm blue that stays out of the way of
        /// the three role colours. The game's own mana gauge is there to be pipetted, and this
        /// value is to be replaced by that reading rather than tuned by eye.
        /// </summary>
        public static readonly uint Mana = Rgb(0x4C6FD0);

        /// <summary>
        /// The party number's plate, the shape the game's own list gives a position: a white
        /// rounded square with a black edge and the figure dark in the middle of it.
        /// <para>
        /// White and fully opaque, and deliberately NOT the body ink at nine tenths: over the
        /// game, a grey at less than full cover picks up whatever is behind it and reads as
        /// dirty rather than as light (Florian, 2026-09-12). This is the one place in the
        /// suite where a pure white belongs, because it is a plate and not a surface.
        /// </para>
        /// </summary>
        public static readonly uint NumberPlate = 0xFFFFFFFFu;

        public static readonly uint NumberEdge = 0xFF000000u;

        public static readonly uint NumberInk = Rgb(0x1B1A1B);

        /// <summary>
        /// Under every piece of text a HUD element writes. The frames lie over the world, and
        /// a bright name on a bright bar is unreadable without something behind it.
        /// <para>
        /// Two layers, not one. A single hard copy at three quarters black read as a second,
        /// dirty letter offset from the first rather than as a shadow (Florian, 2026-09-12);
        /// a near one and a fainter far one fall off instead of stopping, which is what makes
        /// it read as shade.
        /// </para>
        /// </summary>
        public static readonly uint HudTextShadow = 0x8C000000u;

        /// <summary>The second, wider and fainter layer of the same shadow.</summary>
        public static readonly uint HudTextShadowFar = 0x46000000u;

        /// <summary>
        /// The outline, which is a different job from the shadow and therefore a different
        /// colour. A shadow suggests depth and may be soft; an outline cuts the letter out of
        /// whatever is behind it and has to be hard and fully black to do that at all
        /// (Florian, 2026-09-12).
        /// </summary>
        public static readonly uint HudTextOutline = 0xFF000000u;

        /// <summary>
        /// Text on a HUD element. Plain white, and the reason it is its own token is that the
        /// window's <see cref="Ink"/> was used here first and read as grubby over the world
        /// (Florian, 2026-09-12).
        /// <para>
        /// 🔴 The rule behind it, which is the second time it has cost a fix: a window colour
        /// is not a HUD colour. #C3C3C3 is measured off the game's own panel and is right on a
        /// panel, where it sits against one known dark surface. Over the world there is no
        /// known surface — the text crosses grass, stone, sky and a health bar in one line —
        /// and anything short of white reads as dirty white rather than as grey. The party
        /// number's plate was the same lesson in Sitzung 7.
        /// </para>
        /// </summary>
        public static readonly uint HudInk = 0xFFFFFFFFu;

        /// <summary>
        /// Text on a HUD element that is not the thing to read: a name on a frame that has
        /// stepped back, and the note that says why it has.
        /// <para>
        /// A darker grey rather than white at lower opacity. White stays white however faint
        /// it is drawn — on a dimmed frame the name was still the loudest thing on it, and
        /// "Offline" in white read as an alarm rather than as an explanation (Florian,
        /// 2026-09-12). Opacity says how present something is; colour says how important.
        /// </para>
        /// </summary>
        public static readonly uint HudInkQuiet = Rgb(0x9A9A9A);

        // --- edit mode ---------------------------------------------------------

        /// <summary>
        /// The wash over the world while the HUD is being arranged. Dark and gentle: it has to
        /// quiet the world enough that an edge can be judged, without hiding what the frames
        /// will actually sit against.
        /// </summary>
        public static readonly uint EditWash = 0x8C0A0A0Au;

        /// <summary>The two centre lines, and the outline of an element at rest.</summary>
        public static readonly uint EditAxis = 0x70FFFFFFu;

        /// <summary>A line that is holding a drag, and the outline of the element under the hand.</summary>
        public static readonly uint EditGuide = 0xFFD8B567u;

        /// <summary>Behind an element's name while it is being arranged.</summary>
        public static readonly uint EditLabelBg = 0xD2141314u;

        /// <summary>
        /// The three roles, MEASURED off the game's own role markers (Florian, 2026-09-11).
        /// Not to be "improved" by eye: a player reads these three before they read a name,
        /// and any drift from the game's own blue, green and red costs exactly that.
        /// </summary>
        public static readonly uint RoleTank = Rgb(0x006EFF);

        public static readonly uint RoleHealer = Rgb(0x6EF54D);
        public static readonly uint RoleDps = Rgb(0xFF6C6C);

        /// <summary>
        /// ⚠️ NOT MEASURED. The mark for "this can be cleansed", picked to sit beside the
        /// three role colours without being mistaken for one of them — a violet, which is the
        /// one hue the roles do not occupy, and light enough to read on a filled bar.
        /// <para>
        /// A candidate for the pipette, and one of the harder ones: it has to hold against a
        /// blue, a green and a red bar, not against a panel (session 8: a window colour is not
        /// a HUD colour).
        /// </para>
        /// </summary>
        public static readonly uint Cleanse = Rgb(0xB382DD);

        /// <summary>
        /// ⚠️ NOT MEASURED. Somebody a raise is already on its way to — "this one is handled",
        /// said to the second healer.
        /// <para>
        /// A spring green, and deliberately NOT the healer role green <c>#6EF54D</c>. The mark
        /// washes over a bar that may already be that exact colour, so taking it would make the
        /// mark vanish on the job most likely to be casting the raise. Far enough from the
        /// cleanse purple beside it to never be confused with it, which is the other job a
        /// mark colour has.
        /// </para>
        /// </summary>
        public static readonly uint Raise = Rgb(0x5BD98A);

        /// <summary>
        /// ⚠️ NOT MEASURED. The slide window on the cast bar, before the server has taken the
        /// cast: "not yet". A warm red, after the screenshot Florian brought (2026-09-25).
        /// </summary>
        public static readonly uint SlideWait = Rgb(0xFF6A5A);

        /// <summary>
        /// ⚠️ NOT MEASURED. The slide window once the cast is taken: "move now". The same
        /// green as the raise mark on purpose — both say "this is handled".
        /// </summary>
        public static readonly uint SlideReady = Rgb(0x5BD98A);

        /// <summary>
        /// ⚠️ NOT MEASURED. The Legacy preview's stand-in of the game's party list: its bar's
        /// empty track and its health fill, the pale green the game uses, by eye.
        /// </summary>
        public static readonly uint LegacyTrack = Rgb(0x2C2B2B);

        public static readonly uint LegacyHealth = Rgb(0xCFE8C9);

        /// <summary>
        /// ⚠️ NOT MEASURED. Somebody who cannot be killed right now — the amber the game uses
        /// on its own invulnerability effects, by eye.
        /// </summary>
        public static readonly uint Invulnerable = Rgb(0xF0C060);

        /// <summary>
        /// ⚠️ NOT MEASURED. Damage that will not land, by eye: a pale warm white, deliberately
        /// neither a role nor a job colour so a shield can never be mistaken for one. The
        /// setting under Base is what it is really for — this is only where it starts.
        /// </summary>
        public static readonly uint Shield = Rgb(0xFFBF22);

        /// <summary>The wedge that sweeps an affliction icon as it runs out.</summary>
        public static readonly uint AuraSwipe = 0x96000000u;

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
        // arrows, and WispUI follows that. Set by eye rather than with a dropper, then held
        // against the game's own slider in-game and accepted (Florian, 2026-09-11): SETTLED,
        // not provisional. Treat these like a measured value — do not re-tune them by feel.
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
        /// The same colour, darker, at the alpha it already had.
        /// <para>
        /// 🔴 The way anything drawn over the game world steps back. Taking a colour's alpha
        /// away lets the world through it, and grass, stone and sky drag every colour towards
        /// the same grey — so a frame faded that way loses the one thing it was saying. Made
        /// darker instead, it stays solid and a dark green is still recognisably green
        /// (Florian, 2026-09-13; the rule was written down after session 8 and this is the
        /// first place it is enforced in code).
        /// </para>
        /// <para>
        /// Not a blend towards black: the channels keep their ratios, so the hue is exactly
        /// the hue it was and only the brightness moves.
        /// </para>
        /// </summary>
        public static uint Darker(uint colour, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            uint r = (uint)MathF.Round(((colour >> 0) & 0xFFu) * amount);
            uint g = (uint)MathF.Round(((colour >> 8) & 0xFFu) * amount);
            uint b = (uint)MathF.Round(((colour >> 16) & 0xFFu) * amount);

            return (colour & 0xFF000000u) | (b << 16) | (g << 8) | r;
        }

        /// <summary>
        /// The same colour at a share of the alpha it already had.
        /// <para>
        /// Multiplies rather than sets, so it can be laid over a colour that has already been
        /// faded by a setting — the health bar's own opacity, say — without throwing that away.
        /// </para>
        /// </summary>
        public static uint Softer(uint colour, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            uint alpha = (uint)MathF.Round(((colour >> 24) & 0xFFu) * amount);
            return (colour & 0x00FFFFFFu) | (alpha << 24);
        }

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
        /// <summary>
        /// Wider than it was (920, then 1020) because the rows are single-line now: a label and
        /// a control share one row. At 1020 the selector clipped its own name to "Gradie" —
        /// in-game, 2026-09-11 — so both the window and the control column grew again.
        /// </summary>
        public static float WindowWidth => Px(1080f);
        // Grown from 700 when the preview band arrived (Florian, 2026-09-19). The band takes
        // 200 off the content, which left the fuller tabs scrolling; 860 still fits a 1080p
        // screen with room to spare.
        public static float WindowHeight => Px(860f);

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

        /// <summary>
        /// How far a profile row indents its name, to leave the active dot its own column.
        /// </summary>
        public static float ProfileDotColumn => Px(22f);

        /// <summary>The radius of the dot that marks the chosen row.</summary>
        public static float RadioDot => Px(5f);

        /// <summary>The arrow at the end of a note that leads somewhere.</summary>
        public static float NewsChevron => Px(10f);

        /// <summary>
        /// The air between two release notes. Small on purpose: they are a stack of related
        /// lines, and a wide gap would read as separate cards rather than as a list.
        /// </summary>
        public static float NewsRowGap => Px(3f);

        public static float NewsRowPaddingX => Px(10f);
        public static float NewsRowPaddingY => Px(7f);

        /// <summary>The pill at the start of a release note, saying where the line leads.</summary>
        public static float NewsPillHeight => Px(18f);

        public static float NewsPillPaddingX => Px(9f);

        /// <summary>Above a section heading, which is where a release actually breathes.</summary>
        public static float NewsSectionGap => Px(22f);

        /// <summary>Either side of the rule between two releases.</summary>
        public static float NewsReleaseGap => Px(26f);

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

        // --- the preview band ---------------------------------------------
        // The strip under the module header that shows the element as it will look. Its
        // height is fixed and the frames scroll inside it: a band that grew with the layout
        // would take the window away from the settings it exists to serve, and the frame
        // height alone can be set to 150.

        public static float PreviewBarHeight => Px(30f);
        public static float PreviewHeight => Px(200f);
        public static float PreviewPadding => Px(12f);
        public static float PreviewCaret => Px(9f);
        public static float EyeGlyph => Px(18f);

        /// <summary>
        /// One line of a checklist in a popup. Tighter than a settings row on purpose: a menu
        /// is read down in one go, where a settings row is a decision with air around it.
        /// </summary>
        public static float MenuRowHeight => Px(26f);

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

        /// <summary>
        /// How wide a popup list may get. A list of names needs the width of the longest name,
        /// not the width of whatever opened it — a full-width row would otherwise put six short
        /// entries in a very large box (Florian, 2026-09-12).
        /// </summary>
        public static float PopupMaxWidth => Px(300f);

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
        /// <summary>
        /// Head rule to the first row.
        /// </summary>
        public static float GroupRuleGap => Px(16f);

        // --- the vertical rhythm of a settings screen ---
        // Two values now — everything between rows is the ladder. The head gap stays its own
        // number: a section head is set off from its controls by space rather than by a line,
        // because the window has exactly two kinds of divider and neither belongs there.

        /// <summary>Section head to its first row.</summary>
        public static float SectionHeadGap => Px(20f);

        /// <summary>
        /// Between two rows. One value for every kind of row, because every row is now built
        /// the same way: label left, control right, one height.
        /// </summary>
        public static float RowGap => Px(16f);

        /// <summary>
        /// The control column at the right of every row — the same width whatever stands in it,
        /// so controls line up down a group AND across the two columns. Sized off the widest
        /// thing we have: the arrow selector with its preview and counter.
        /// </summary>
        public static float ControlWidth => Px(230f);

        /// <summary>Room kept at the right edge of a slider row for its number.</summary>
        public static float ValueWidth => Px(46f);

        /// <summary>
        /// The field a slider's number turns into when it is clicked. Shorter than a row, so
        /// it reads as something that opened inside the row rather than as a control that was
        /// always standing there.
        /// </summary>
        public static float ValueEditHeight => Px(22f);

        /// <summary>Air under the last row, so a scrolled screen does not end flush with the edge.</summary>
        public static float ContentPaddingBottom => Px(24f);

        /// <summary>
        /// Inside a party frame, between its edge and what it holds. NOT an option: what a
        /// player actually wants to move is where the name and the numbers sit, and that is
        /// coming as its own setting — a padding slider would be a second way to say it
        /// (Florian, 2026-09-11).
        /// </summary>
        public static float FramePadding => WorldPx(5f);

        /// <summary>The line a party frame is outlined with, and the gap a second bar sits behind.</summary>
        public static float FrameBorder => WorldLine(1f);

        /// <summary>
        /// The ring on the frame under the mouse. Three pixels rather than the frame's own
        /// one: at two it was there but had to be looked for, and a highlight you look for is
        /// not doing its job (Florian, 2026-09-12). Thicker than the frame edge on purpose —
        /// it has to read as something arriving, not as the edge having changed colour.
        /// </summary>
        public static float FrameHoverRing => WorldLine(3f);

        /// <summary>
        /// The ring on the frame of whoever is targeted. Thinner than the hover ring on
        /// purpose: it is there all the time, so it only has to be findable, and when the
        /// mouse is on the target the hover ring still has to read as the one arriving.
        /// </summary>
        public static float FrameTargetRing => WorldLine(2f);

        // --- the Legacy preview: a stand-in of the game's party list -------------
        // In screen pixels like everything drawn as the HUD looks. ⚠️ By eye from the game's
        // list at 100 %, not measured; the real marks sit on the real list.

        public static float LegacyRowWidth => WorldPx(220f);

        public static float LegacyRowHeight => WorldPx(42f);

        public static float LegacyRowGap => WorldPx(4f);

        public static float LegacyRowPad => WorldPx(6f);

        public static float LegacyIcon => WorldPx(30f);

        public static float LegacyBarHeight => WorldPx(6f);

        public static float LegacyNameGap => WorldPx(2f);

        /// <summary>The mark round a row (variant D): its outline, its corners, and how strong its wash is at full fill strength.</summary>
        public static float LegacyMarkEdge => WorldLine(2f);

        public static float LegacyMarkRadius => WorldPx(6f);

        public const float LegacyWash = 0.18f;

        // --- Quick Dispel ---------------------------------------------------------
        // On the world, so screen pixels (WorldPx), never the suite scale.

        /// <summary>Between two squares. Enough that two job colours side by side read as two edges.</summary>
        public static float DispelGap => WorldPx(4f);

        /// <summary>The job-colour edge of a square. Two, so the colour reads at a glance on a 16 px square.</summary>
        public static float DispelEdge => WorldLine(2f);

        /// <summary>
        /// The Alt handle above the first square: a smaller square with no edge, as a share of
        /// a real one, and the air under it. A square rather than a dot, because a dot did not
        /// read as something to take hold of (Florian, 2026-09-25).
        /// </summary>
        public const float DispelHandleShare = 0.5f;

        public static float DispelHandleGap => WorldPx(8f);

        /// <summary>How far the handle brightens towards white under the pointer.</summary>
        public const float DispelHandleLift = 0.25f;

        /// <summary>
        /// The ring round the square under the pointer. Thinner and half as strong as the
        /// frames' own (Florian, 2026-09-25: too loud on a square this small) — on a square the
        /// hand already says "clickable", the ring only says which one.
        /// </summary>
        public static float DispelHoverRing => WorldLine(2f);

        public const float DispelHoverAlpha = 0.5f;

        /// <summary>
        /// A square whose member is out of reach or not here. Fainter than a frame out of
        /// range: a frame still has health to say, a square
        /// only says "you can click this", and here that is not true.
        /// </summary>
        public const float DispelAwayAlpha = 0.4f;

        /// <summary>The party number and the seconds, as a share of the square's side.</summary>
        public const float DispelNumberShare = 0.5f;

        // --- the slide window on the game's cast bar ----------------------------

        /// <summary>
        /// The see-through lead-in on the left of the game's gauge art, in the gauge's own
        /// units. The art is placed this far before the slide point so that its visible left
        /// edge lands on it. The same margin the gauge carries (measured 2026-09-25: the
        /// visible rim starts 7 to 8 units inside the 160-unit gauge).
        /// </summary>
        public const float SlideArtLeadIn = 8f;

        /// <summary>
        /// How strongly the green fill covers the game's pink fill once the server has taken
        /// the cast. Strong, because it is the one moment the window exists for (Florian,
        /// 2026-09-25: a green frame round a bar that stayed pink was hard to read); short of
        /// solid, so the bar's own progress still shows through underneath.
        /// </summary>
        public const float SlideReadyAlpha = 0.85f;

        /// <summary>
        /// The slide window the game allows, in seconds: the last half second of a cast.
        /// Where the window starts on the bar. Which colour it wears is the game's own word.
        /// </summary>
        public const float SlideSeconds = 0.5f;

        // --- the combat meter ---------------------------------------------------
        // It wears the window's look but lives on the world, so every size here is in screen
        // pixels (WorldPx), never in the suite scale — the same two-worlds rule as the frames.

        /// <summary>The lit part at the top of the meter's title bar, like the window's own.</summary>
        public static float MeterTitleFade => WorldPx(12f);

        /// <summary>The four buttons in the meter's title bar, and the room between them.</summary>
        public static float MeterIcon => WorldPx(16f);

        public static float MeterIconGap => WorldPx(10f);

        /// <summary>Between the meter's edge and its card, and around the title text.</summary>
        public static float MeterPad => WorldPx(8f);

        /// <summary>Inside the card, between its edge and the bars.</summary>
        public static float MeterCardPad => WorldPx(4f);

        /// <summary>The meter's own corners (the window's 8), its card's, and each bar's.</summary>
        public static float MeterRadius => WorldPx(8f);

        public static float MeterCardRadius => WorldPx(6f);

        public static float MeterBarRadius => WorldPx(3f);

        /// <summary>Between a bar's edge and its first and last text, and between the texts.</summary>
        public static float MeterBarInset => WorldPx(6f);

        /// <summary>The corner that resizes the meter, and the lock beside it.</summary>
        public static float MeterGrip => WorldPx(14f);

        /// <summary>How far a HUD text's shadow is offset. One pixel, at whatever the scale is.</summary>
        public static float HudTextShadow => WorldLine(1f);

        /// <summary>
        /// What a frame's opacity is multiplied by when the game has no numbers for that
        /// member — out of range, another instance, or offline.
        /// <para>
        /// Deliberately gentle. It has to say "no reading right now" without saying
        /// "unimportant": these are the people you are about to run back to, and a frame faded
        /// to a ghost is one you stop checking. Applied to the WHOLE frame — bar, background,
        /// name, icons — because dimming only the bar left everything else as loud as a member
        /// who is actually there (Florian, 2026-09-12).
        /// </para>
        /// </summary>
        /// <para>
        /// 🔴 Was 0.85, which read as no dimming at all once the class colour came back onto
        /// these frames — "sie wirken noch als ob es 100% sind" (Florian, 2026-09-13). The
        /// gentleness above is still right in kind; it was simply set too high to be seen.
        /// </para>
        public const float OutOfRangeDim = 0.7f;

        /// <summary>
        /// How solid an absent member's frame stays, on top of being darker.
        /// <para>
        /// A little transparency, asked for on top of the darkening (Florian, 2026-09-13) —
        /// enough that the world shows faintly through and the frame reads as set aside
        /// rather than merely dim.
        /// </para>
        /// <para>
        /// 🔴 Only a little, and only alongside the darkening. Transparency alone is what
        /// destroyed the class colour twice: at any real amount the world behind the frame
        /// drags every colour to the same grey. Fifteen percent is below where that starts.
        /// The writing on the frame keeps its own alpha and is not softened at all — a name
        /// you can see through is a name you read twice.
        /// </para>
        /// </summary>
        public const float AbsentAlpha = 0.85f;

        /// <summary>
        /// How solid the frame of somebody merely too far away stays.
        /// <para>
        /// Lower than the others and WITHOUT any darkening: they are standing right there and
        /// will be back in a moment, so the frame has to stay as readable as everybody else's.
        /// Making it darker would be saying something about the person rather than about the
        /// distance (Florian, 2026-09-13).
        /// </para>
        /// </summary>
        public const float OutOfRangeAlpha = 0.7f;

        /// <summary>
        /// A member in another zone entirely.
        /// <para>
        /// Its own step, between out of range and offline, because it is its own news. Out of
        /// range is a pause: they are running back, and the frame stays easy to check. Another
        /// zone is not this pull, and that frame may properly step back.
        /// </para>
        /// </summary>
        public const float AwayDim = 0.45f;

        /// <summary>
        /// The same, for a member who has logged out.
        /// <para>
        /// The deepest of the three. Out of range is a pause and another zone is a wait;
        /// logged out is over, and that frame may recede furthest (Florian, 2026-09-12).
        /// </para>
        /// </summary>
        public const float OfflineDim = 0.35f;

        /// <summary>
        /// How thick the line is that marks where a shield begins over the health fill, before
        /// the interface scale is applied. Two, because one disappears against a busy bar
        /// style and three starts reading as a piece of the shield rather than its edge.
        /// </summary>
        public const float ShieldMark = 2f;

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
