using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Hud;
using WispUI.Interface.Screens;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface;

/// <summary>
/// The settings window: title bar, navigation tree, tab chips and a per-screen header.
/// The screens themselves are mostly empty — this is the frame they will hang in.
/// <para>
/// The window frame is the topmost layer, so a scrollbar or a pinned header can never sit
/// on top of it. That is enforced by geometry rather than by draw order: everything inside
/// is inset by the frame width, so those pixels belong to the frame alone. Child windows
/// carry their own draw lists and would otherwise paint over a border drawn "last" by the
/// parent.
/// </para>
/// </summary>
internal sealed class ConfigWindow : Window
{
    private const string IdClose = "##wisp-close";
    private const string IdContent = "##wisp-content";
    private const string IdPreview = "##wisp-preview";
    private const string IdPreviewToggle = "##wisp-previewtoggle";
    private const string IdPreviewEye = "##wisp-previeweye";
    private const string IdPreviewEyeMenu = "##wisp-previeweyemenu";
    private const string IdPreviewShowAll = "##wisp-previewshowall";
    private const string IdNews = "##wisp-news";
    private const string IdEditMode = "##wisp-editmode";
    private const string IdModuleSwitch = "##wisp-module-switch";
    private const string IdDefaults = "##wisp-defaults";

    private const uint Transparent = 0x00000000u;

    /// <summary>
    /// Tab hit boxes. They only need to be unique while their screen is on show, so one set
    /// serves every screen.
    /// <para>
    /// 🔴 Kept well ahead of the longest tab list on purpose. The loop below stops at whichever
    /// of the two runs out, so a list longer than this array does not fail — it silently drops
    /// the tabs past the end, which is how a fourth tab once went missing without a word
    /// (Florian, 2026-09-12). Adding a tab means checking this line.
    /// </para>
    /// </summary>
    private static readonly string[] TabIds =
    {
        "##wisp-tab0",
        "##wisp-tab1",
        "##wisp-tab2",
        "##wisp-tab3",
        "##wisp-tab4",
        "##wisp-tab5",
        "##wisp-tab6",
        "##wisp-tab7",
    };

    /// <summary>
    /// The party sizes the preview offers. Not a slider from one to eight: these are the
    /// three arrangements anybody actually plays in, and the ones in between tell you nothing
    /// a full party does not.
    /// </summary>
    private static readonly int[] PreviewCounts = { 1, 4, 8 };

    private static readonly string[] PreviewCountLabels =
    {
        Strings.PreviewSolo,
        Strings.PreviewLight,
        Strings.PreviewFull,
    };

    private static readonly string[] PreviewCountIds = { "##wisp-pv1", "##wisp-pv4", "##wisp-pv8" };

    /// <summary>
    /// What each part of the preview is called in the menu, in the same order as
    /// <see cref="PreviewMask.All"/>. Two lists rather than one of pairs, because the enum
    /// lives beside the drawing and the words live in the strings file.
    /// </summary>
    private static readonly string[] PreviewPartLabels =
    {
        Strings.GroupNameText,
        Strings.GroupHealthText,
        Strings.GroupMana,
        Strings.GroupShield,
        Strings.GroupJobIcon,
        Strings.GroupLeader,
        Strings.GroupPartyNumber,
        Strings.GroupAuras,
        Strings.GroupBuffs,
        Strings.GroupOthers,
        Strings.GroupCleanse,
        Strings.GroupRaiseMark,
        Strings.GroupRescue,
    };

    private static readonly string[] PreviewPartIds =
    {
        "##wisp-eye0", "##wisp-eye1", "##wisp-eye2", "##wisp-eye3", "##wisp-eye4",
        "##wisp-eye5", "##wisp-eye6", "##wisp-eye7", "##wisp-eye8", "##wisp-eye9",
        "##wisp-eye10", "##wisp-eye11", "##wisp-eye12",
    };

    private static readonly string[] TabsGlobal = { Strings.TabBase };
    private static readonly string[] TabsProfile = { Strings.TabBase };
    /// <summary>
    /// Split by what KIND of thing a setting is, not by subject (§3.1): the bar · what is
    /// written on it · the badges on it · where the frames go · what is lying on the person ·
    /// what needs doing about it · what the mouse does.
    /// <para>
    /// 🔴 That rule was written on 2026-09-12 because Base was carrying seven groups — and
    /// Base went on carrying seven groups for another six days. Rewritten on 2026-09-18 with
    /// every tab at three, which is a two by two grid with room left and nothing scrolling.
    /// </para>
    /// <para>
    /// The price is seven chips instead of five, and the price is real: §3.1 says the tab
    /// count is itself a cost, and that answering a crowded tab with more tabs is a road worth
    /// refusing to go far down. Seven is where Florian drew the line, with tabs of three
    /// rather than tabs of seven.
    /// </para>
    /// </summary>
    private static readonly string[] TabsPartyFrames =
    {
        Strings.TabBase,
        Strings.TabText,
        Strings.TabIcons,
        Strings.TabLayout,
        Strings.TabAuras,
        Strings.TabMarks,
        Strings.TabBindings,
    };

    /// <summary>
    /// Which chip is the bindings tab, asked of the list rather than written down. Two places
    /// far apart act on it, and a literal in both is the pair that drifts when a tab is
    /// inserted ahead of them.
    /// </summary>
    private static readonly int TabIndexBindings = Array.IndexOf(TabsPartyFrames, Strings.TabBindings);

    /// <summary>
    /// The navigation tree. Suite-wide entries first, then a separator, then the HUD
    /// modules. Modules that are not built yet stay in the list, dimmed, so the tree
    /// still says what is coming.
    /// </summary>
    private static readonly NavRow[] NavRows =
    {
        new("##wisp-nav-global", Strings.NavGlobal, Screen.Global),
        new("##wisp-nav-profile", Strings.NavProfile, Screen.Profile),
        NavRow.Separator(),
        new("##wisp-nav-party", Strings.NavPartyFrames, Screen.PartyFrames),
        NavRow.NotYet("##wisp-nav-playerbars", Strings.NavPlayerBars),
        NavRow.NotYet("##wisp-nav-gauges", Strings.NavJobGauges),
    };

    private readonly Configuration m_config;

    /// <summary>
    /// The element the preview band shows, or null for a screen with nothing to show.
    /// <para>
    /// Handed in rather than looked up: the window knows about screens, not about what is
    /// being drawn on the world. When the second module wants a band, this becomes a lookup
    /// from screen to element and nothing else here changes.
    /// </para>
    /// </summary>
    private readonly Hud.HudElement? m_previewOf;
    private readonly GlobalScreen m_global;
    private readonly PartyFramesScreen m_partyFrames;

    /// <summary>
    /// One buffer for the whole suite, and one strip that offers it. Both are built here and
    /// handed to whichever module is on screen — the clipboard belongs to the suite, the
    /// appearance it holds belongs to an element.
    /// </summary>
    private readonly AppearanceClipboard m_clipboard = new();

    private readonly AppearanceBar m_appearance;

    /// <summary>Built once — the version never changes while the plugin is loaded.</summary>
    private readonly string m_versionChip;
    private readonly string m_versionBadge;

    /// <summary>One remembered tab per screen, so switching modules does not lose your place.</summary>
    private readonly int[] m_tabIndex = new int[Enum.GetValues<Screen>().Length];

    private Screen m_screen = Screen.PartyFrames;

    /// <summary>Whether a list or panel of ours is up — worked out once, in <see cref="PreDraw"/>.</summary>
    private bool m_popupOpen;

    /// <summary>Whether the pointer is currently ours to speak for.</summary>
    private bool m_ownsCursor;

    public ConfigWindow(Configuration config, Hud.HudElement? previewOf)
        : base(
            Strings.WindowId,
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse)
    {
        m_config = config;
        m_previewOf = previewOf;
        m_global = new GlobalScreen(config);
        m_global.InfoBarPreferenceChanged += () => this.InfoBarPreferenceChanged?.Invoke();

        m_partyFrames = new PartyFramesScreen(config);
        m_appearance = new AppearanceBar(m_clipboard);

        string version = ReadVersion();
        m_versionChip = Strings.PluginName + " " + version;
        m_versionBadge = "v" + version;
    }

    private enum Screen
    {
        Global,
        Profile,
        PartyFrames,
    }

    /// <summary>Raised when the user turns the server info bar entry on or off.</summary>
    public event Action? InfoBarPreferenceChanged;

    /// <summary>
    /// Closing the window puts the appearance clipboard's step back out of reach. Undo is
    /// meant for the moment right after a paste, not for whenever you happen to look again.
    /// </summary>
    public override void OnClose()
    {
        m_clipboard.ForgetUndo();
        Chrome.CancelValueEdit();

        // The eye switches are a way of looking at the preview, not settings. Everything is
        // back on the next time the window opens, which is the whole reason they are allowed
        // to be as many as they are.
        PreviewMask.ShowAll();

        this.ReleaseCursor();
    }

    /// <summary>
    /// Hands the pointer back to the game. Called when the window closes and when the plugin
    /// goes away: the switch it turns off is shared by everything running in the game, so it
    /// must never be left lying the way we wanted it.
    /// </summary>
    public void ReleaseCursor()
    {
        m_ownsCursor = false;
        NativeUi.ReleaseCursor();
    }

    /// <summary>
    /// Takes over the pointer while the mouse is on this window, and lets the game keep
    /// drawing it.
    /// <para>
    /// Dalamud's own answer is to replace the game's pointer with a Windows one over a plugin
    /// window. That leaves a WispUI button wearing a different pointer from every other thing
    /// in the game, so we do the opposite: the game keeps its pointer and we tell it which of
    /// its shapes to wear (Florian, 2026-09-12). <see cref="NativeCursor"/> carries the shape;
    /// this only decides when the pointer is ours to speak for.
    /// </para>
    /// <para>
    /// Away from the window the game gets its pointer back untouched, which is the half of
    /// this that must not be broken.
    /// </para>
    /// </summary>
    private void TakeCursor()
    {
        bool ours = m_popupOpen || ImGui.IsWindowHovered(
            ImGuiHoveredFlags.RootAndChildWindows
            | ImGuiHoveredFlags.AllowWhenBlockedByPopup
            | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        m_ownsCursor = ours;

        // Said rather than written: the party frames ask for the same thing when the mouse is
        // on them, and one switch shared by the whole game gets exactly one writer. NativeUi
        // settles it once the frame is done.
        if (ours)
        {
            NativeUi.KeepGameCursor();
        }
    }

    public override void PreDraw()
    {
        // While a list or panel is open, escape belongs to it. Without this the key reaches
        // the window first and shuts the whole suite instead of the popup in front of it.
        // A number being typed into holds escape for the same reason: the key has to be able
        // to abandon the entry without taking the window with it.
        m_popupOpen = ImGui.IsPopupOpen(
            string.Empty,
            ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
            || Chrome.IsEditingValue;

        this.RespectCloseHotkey = !m_popupOpen;
        this.HandleEscape(m_popupOpen);

        // The window has a fixed size and is not resizable by hand: dragging an ImGui corner
        // is fiddly, and a settings window that can be pulled to any width never looks right.
        // Its size follows the interface scale in Global, so it is re-applied every frame.
        this.Size = new Vector2(Tokens.Metric.WindowWidth, Tokens.Metric.WindowHeight);
        this.SizeCondition = ImGuiCond.Always;

        // The whole window is painted by hand, so ImGui contributes nothing but the box.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Tokens.Radius.Window);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Tokens.Col.Panel);

        // ImGui paints the resize grip after the window body, which would put it on top of
        // the gold edge in the corner. Made invisible rather than removed: resizing still
        // works, and the cursor still announces it.
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Transparent);
    }

    public override void PostDraw()
    {
        // The shape itself is set once for the whole frame by NativeUi, after the HUD has had
        // its say too — this window is no longer the only thing the mouse can be over.
        Chrome.EndFrame();
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(3);
    }

    /// <summary>
    /// Escape closes the open list or panel, and nothing else.
    /// <para>
    /// The game does not get the key through a window message; it reads its own key buffer,
    /// which is why the system menu came up behind the popup no matter what the window did
    /// about the close hotkey. So the key is taken out of that buffer for as long as a popup
    /// is open — while one is up it belongs to the popup — and the popup itself is asked to
    /// close, because only a popup's own body may call ImGui's close.
    /// </para>
    /// </summary>
    private void HandleEscape(bool popupOpen)
    {
        if (!popupOpen)
        {
            return;
        }

        // Read from ImGui rather than from the game's key buffer: while a search box has the
        // keyboard, Dalamud keeps the key away from the game, so the buffer never shows the
        // press at all and the popup sat there until a second one. ImGui sees every press,
        // and asking it also means the field and the popup both go on the same one.
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            Chrome.RequestClosePopups();
        }

        // Taken out of the game's buffer anyway, for the presses it does see.
        Services.KeyState[VirtualKey.ESCAPE] = false;
    }

    public override void Draw()
    {
        this.TakeCursor();

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 origin = ImGui.GetWindowPos();
        Vector2 size = ImGui.GetWindowSize();

        float border = Tokens.Metric.WindowBorder;
        float left = origin.X + border;
        float right = origin.X + size.X - border;
        float top = origin.Y + border;
        float bottom = origin.Y + size.Y - border;

        float titleHeight = Tokens.Metric.TitleBarHeight;
        float bodyTop = top + titleHeight;

        // Painted over the whole window, frame inset included. If it stopped at the inset,
        // the pixels the frame normally covers would show through whenever the window loses
        // focus and the frame is not drawn. Rounded to the same radius as the frame: every
        // surface inside has to stop at the curve, or the corner fills itself back in.
        dl.AddRectFilled(
            origin,
            new Vector2(origin.X + size.X, origin.Y + size.Y),
            Tokens.Col.Panel,
            Tokens.Radius.Window,
            ImDrawFlags.RoundCornersAll);

        this.DrawTitleBar(dl, origin.X, origin.X + size.X, origin.Y, top, titleHeight);
        this.DrawNav(dl, left, bodyTop, bottom);
        this.DrawModuleArea(dl, left + Tokens.Metric.NavWidth, right, bodyTop, bottom);

        if (this.IsFocused)
        {
            DrawWindowEdge(dl, origin, size);
        }
    }

    private static string ReadVersion()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "0.0.0"
            : version.Major + "." + version.Minor + "." + version.Build;
    }

    /// <summary>
    /// The window frame, rebuilt ring by ring from the pixels measured off the game's own:
    /// four rings deep, with a different colour sequence on the top edge, the sides and the
    /// bottom. Drawn only while the window has focus — the game drops its frames when a
    /// window goes to the back.
    /// <para>
    /// Each ring is stroked as three paths: the top edge carrying both of its corner arcs,
    /// the bottom edge carrying its own, and the two sides as straight lines between them. So
    /// the corner takes the colour of the horizontal edge, and the measured sequence survives
    /// the rounding intact — the game draws real artwork there, which no path reproduces, but
    /// this is the same approximation the square version already made.
    /// </para>
    /// <para>
    /// Strokes sit on half-pixel centres, because a one-pixel line centred on a whole
    /// coordinate lands half in each neighbouring pixel. Along the arcs that cannot be helped:
    /// four one-pixel rings blur into one another around a curve. FFXIV's own corners read
    /// darker for the same reason.
    /// </para>
    /// </summary>
    private static void DrawWindowEdge(ImDrawListPtr dl, Vector2 origin, Vector2 size)
    {
        float ring = Tokens.Line(1f);
        float radius = Tokens.Radius.Window;
        int rings = Tokens.Col.EdgeTop.Length;

        for (int i = 0; i < rings; i++)
        {
            // Whole-pixel bounds, because the straight runs are filled rectangles rather than
            // strokes: a one-pixel stroke is antialiased across two pixels, and four of them
            // side by side average into one another — the near-white second ring ends up
            // mixed into its dark neighbours and the whole edge reads dark and thin. Filled
            // rectangles on whole pixels keep each ring its own colour, the way the game's
            // frame is drawn.
            float inset = i * ring;
            float left = MathF.Round(origin.X + inset);
            float right = MathF.Round(origin.X + size.X - inset);
            float top = MathF.Round(origin.Y + inset);
            float bottom = MathF.Round(origin.Y + size.Y - inset);
            float r = MathF.Max(0f, radius - inset);

            uint topColour = Tokens.Col.EdgeTop[i];
            uint sideColour = Tokens.Col.EdgeSide[i];
            uint bottomColour = Tokens.Col.EdgeBottom[i];

            // The straight runs, each in its own colour.
            dl.AddRectFilled(new Vector2(left + r, top), new Vector2(right - r, top + ring), topColour);
            dl.AddRectFilled(new Vector2(left + r, bottom - ring), new Vector2(right - r, bottom), bottomColour);
            dl.AddRectFilled(new Vector2(left, top + r), new Vector2(left + ring, bottom - r), sideColour);
            dl.AddRectFilled(new Vector2(right - ring, top + r), new Vector2(right, bottom - r), sideColour);

            if (r <= 0f)
            {
                continue;
            }

            // The arcs still have to be stroked, so they run down the middle of the ring band
            // the rectangles just filled: half a pixel in, with the radius taken in to match.
            float half = ring * 0.5f;
            float arc = r - half;
            left += half;
            right -= half;
            top += half;
            bottom -= half;
            r = arc;

            // The corners carry one run into the next. Stroked as short segments with the
            // colour walked across them, because a corner that simply swaps colours where the
            // arc ends puts a visible step at the very place the eye follows the curve.
            BlendedArc(dl, new Vector2(left + r, top + r), r, MathF.PI, MathF.PI * 1.5f, sideColour, topColour, ring);
            BlendedArc(dl, new Vector2(right - r, top + r), r, MathF.PI * 1.5f, MathF.PI * 2f, topColour, sideColour, ring);
            BlendedArc(dl, new Vector2(right - r, bottom - r), r, 0f, MathF.PI * 0.5f, sideColour, bottomColour, ring);
            BlendedArc(dl, new Vector2(left + r, bottom - r), r, MathF.PI * 0.5f, MathF.PI, bottomColour, sideColour, ring);
        }
    }

    /// <summary>
    /// One quarter-circle whose colour walks from <paramref name="from"/> to
    /// <paramref name="to"/>. Drawn segment by segment: a draw list strokes a path in a single
    /// colour, and a single colour is exactly what leaves the seam at the corners.
    /// </summary>
    private static void BlendedArc(
        ImDrawListPtr dl,
        Vector2 centre,
        float radius,
        float from,
        float to,
        uint colourFrom,
        uint colourTo,
        float thickness)
    {
        // Enough segments that the arc reads as a curve at the radii we use, few enough that
        // four rings on four corners stay a rounding error in the frame.
        const int Segments = 8;

        float step = (to - from) / Segments;
        Vector2 previous = new(
            centre.X + (MathF.Cos(from) * radius),
            centre.Y + (MathF.Sin(from) * radius));

        for (int s = 1; s <= Segments; s++)
        {
            float angle = from + (step * s);
            Vector2 point = new(
                centre.X + (MathF.Cos(angle) * radius),
                centre.Y + (MathF.Sin(angle) * radius));

            dl.AddLine(previous, point, Tokens.Col.Mix(colourFrom, colourTo, (s - 0.5f) / Segments), thickness);
            previous = point;
        }
    }

    private static string ScreenLabel(Screen screen) => screen switch
    {
        Screen.Global => Strings.NavGlobal,
        Screen.Profile => Strings.NavProfile,
        _ => Strings.NavPartyFrames,
    };

    private static string[] TabsFor(Screen screen) => screen switch
    {
        Screen.Global => TabsGlobal,
        Screen.Profile => TabsProfile,
        _ => TabsPartyFrames,
    };

    /// <summary>
    /// The title bar. Its fill runs from the very top of the window rather than from inside
    /// the frame inset: the bar is lighter than the surface, so leaving those few pixels to
    /// the surface colour drew a dark line across the top whenever the window lost focus and
    /// the frame that normally covers them was not there. The text still sits inside the inset.
    /// </summary>
    private void DrawTitleBar(ImDrawListPtr dl, float outerLeft, float outerRight, float outerTop, float top, float height)
    {
        Vector2 min = new(outerLeft, outerTop);
        Vector2 max = new(outerRight, top + height);
        float left = outerLeft + Tokens.Metric.WindowBorder;
        float right = outerRight - Tokens.Metric.WindowBorder;

        // Lit at the very top and fading down into the surface colour, as measured. Its own
        // top corners are rounded to the window radius, since it reaches the window edge.
        Chrome.VerticalFill(
            dl,
            min,
            max,
            Tokens.Col.TitleBarTop,
            Tokens.Col.TitleBar,
            Tokens.Radius.Window,
            ImDrawFlags.RoundCornersTop,
            Tokens.Metric.TitleBarFade);

        // The three-pixel rule that closes the title bar: dark, surface, light. It fades out
        // towards the corners rather than running into the frame.
        Chrome.Rule(dl, left, right, max.Y - Tokens.Metric.TitleRuleHeight);

        // Just the product name. The screen you are on is named by the header below, and
        // saying it twice only makes the title bar busier.
        float x = left + Tokens.Metric.SectionPaddingX;
        Ink.Draw(dl, Ink.Role.Title, new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Title)), Tokens.Col.Ink, Strings.WindowTitle);

        float button = Tokens.Metric.TitleButton;
        if (Chrome.CloseButton(IdClose, right - Tokens.Space.Lg - button, MathF.Round(top + ((height - button) * 0.5f))))
        {
            this.IsOpen = false;
        }
    }

    private void DrawNav(ImDrawListPtr dl, float left, float top, float bottom)
    {
        float width = Tokens.Metric.NavWidth;
        float right = left + width;

        // The rail reaches the bottom-left of the window, so that corner follows the curve —
        // what is left of the window radius once the frame has taken its four pixels.
        dl.AddRectFilled(
            new Vector2(left, top),
            new Vector2(right, bottom),
            Tokens.Col.Rail,
            Tokens.Radius.WindowInner,
            ImDrawFlags.RoundCornersBottomLeft);
        dl.AddRectFilled(new Vector2(right - Tokens.Line(1f), top), new Vector2(right, bottom), Tokens.Col.EdgeDim);

        float y = top + Tokens.Space.Md;
        foreach (NavRow row in NavRows)
        {
            if (row.IsSeparator)
            {
                Chrome.Hairline(dl, left + Tokens.Space.Lg, right - Tokens.Space.Lg, y + Tokens.Space.Md, Tokens.Col.Hairline);
                y += Tokens.Space.Xl;
                continue;
            }

            bool selected = !row.Soon && row.Target == m_screen;
            if (Chrome.NavItem(row.Id, row.Label, left, y, width - Tokens.Line(1f), selected, row.Soon))
            {
                m_screen = row.Target;
            }

            y += Tokens.Metric.NavItemHeight;
        }

        this.DrawNavFooter(dl, left, right, bottom);
    }

    /// <summary>Patch-notes card, edit-mode button, version chip — bottom of the rail, in that order.</summary>
    private void DrawNavFooter(ImDrawListPtr dl, float left, float right, float bottom)
    {
        float pad = Tokens.Space.Md;
        float innerLeft = left + Tokens.Space.Md;
        float innerRight = right - Tokens.Space.Md;

        float y = bottom - pad - Tokens.Metric.NavVersionHeight;
        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(left + Tokens.Space.Lg, Chrome.CenterY(y, Tokens.Metric.NavVersionHeight, Ink.Role.Small)),
            Tokens.Col.InkFaint,
            m_versionChip);

        y -= Tokens.Metric.NavButtonHeight + pad;
        this.DrawEditModeButton(dl, innerLeft, y, innerRight - innerLeft);

        y -= Tokens.Metric.NavCardHeight + pad;
        this.DrawNewsCard(dl, innerLeft, y, innerRight - innerLeft);
    }

    private void DrawEditModeButton(ImDrawListPtr dl, float x, float y, float width)
    {
        float height = Tokens.Metric.NavButtonHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(IdEditMode, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);
        if (ImGui.IsItemClicked())
        {
            EditMode.Toggle();

            // 🔴 Turning it on closes this window. Arranging means dragging the very things
            // the window is covering, and a settings panel in the middle of the screen is the
            // largest obstacle there is to placing something on that screen (Florian,
            // 2026-09-12, following LumenUI). Turning it off does not reopen it: that is a
            // second window appearing without being asked for.
            if (EditMode.IsActive)
            {
                this.IsOpen = false;
            }
        }

        // While it is on the button carries the accent, the way a switch that is doing
        // something does. It is the one control here that changes what the world looks like.
        bool active = EditMode.IsActive;
        Chrome.VerticalFill(
            dl,
            min,
            max,
            active ? Tokens.Col.Gold : hovered ? Tokens.Col.ButtonTop : Tokens.Col.Control,
            active ? Tokens.Col.GoldDim : hovered ? Tokens.Col.ButtonBottom : Tokens.Col.Control2,
            Tokens.Radius.Control);
        dl.AddRect(
            min,
            max,
            active ? Tokens.Col.GoldHi : Tokens.Col.ControlEdge,
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        string label = active ? Strings.EditModeOn : Strings.EditMode;
        float textX = MathF.Round(x + ((width - Ink.Measure(Ink.Role.Body, label).X) * 0.5f));
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(textX, Chrome.CenterY(y, height, Ink.Role.Body)),
            active ? Tokens.Col.InkOnGold : Tokens.Col.Ink,
            label);

        Chrome.TooltipOnHover(Strings.EditModeHint);
    }

    private void DrawNewsCard(ImDrawListPtr dl, float x, float y, float width)
    {
        float height = Tokens.Metric.NavCardHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(IdNews, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);

        dl.AddRectFilled(min, max, Tokens.Col.NavCard, Tokens.Radius.Control);
        dl.AddRect(
            min,
            max,
            hovered ? Tokens.Col.GoldDim : Tokens.Col.NavCardEdge,
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        float innerX = x + Tokens.Space.Md;
        float rowY = y + Tokens.Space.Md;

        Vector2 badgeText = Ink.Measure(Ink.Role.Small, Strings.NewBadge);
        float badgeHeight = Tokens.Metric.BadgeHeight;
        float badgeWidth = badgeText.X + (Tokens.Metric.BadgePaddingX * 2f);
        Vector2 badgeMin = new(innerX, rowY);
        Vector2 badgeMax = new(innerX + badgeWidth, rowY + badgeHeight);
        float badgeTextY = MathF.Round(badgeMin.Y + ((badgeHeight - badgeText.Y) * 0.5f));

        dl.AddRectFilled(badgeMin, badgeMax, Tokens.Col.Gold, Tokens.Radius.Small);
        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(badgeMin.X + Tokens.Metric.BadgePaddingX, badgeTextY),
            Tokens.Col.InkOnGold,
            Strings.NewBadge);
        Ink.Draw(dl, Ink.Role.Small, new Vector2(badgeMax.X + Tokens.Space.Sm, badgeTextY), Tokens.Col.Ink, m_versionBadge);

        float line = Ink.LineHeight(Ink.Role.Small);
        float textY = MathF.Round(rowY + badgeHeight + Tokens.Space.Xs);
        Ink.Draw(dl, Ink.Role.Small, new Vector2(innerX, textY), Tokens.Col.InkFaint, Strings.PatchNotesLine1);
        Ink.Draw(dl, Ink.Role.Small, new Vector2(innerX, MathF.Round(textY + line)), Tokens.Col.InkFaint, Strings.PatchNotesLine2);

        Chrome.TooltipOnHover(Strings.PatchNotesUnavailable);
    }

    private void DrawModuleArea(ImDrawListPtr dl, float left, float right, float top, float bottom)
    {
        float y = this.DrawTabs(dl, left, right, top);
        y += Chrome.Rule(dl, left, right, y);
        y = this.DrawScreenHeader(dl, left, right, y);
        y = this.DrawPreviewBand(dl, left, right, y);
        this.DrawContent(left, right, y, bottom);
    }

    /// <summary>
    /// The element as it will look, under the header and above the settings that change it.
    /// <para>
    /// 🔴 It draws the element's own drawing code with stand-in people, not a picture of it.
    /// A preview built from a second, simpler renderer is one that can disagree with the
    /// game, and it would disagree exactly while somebody is relying on it.
    /// </para>
    /// <para>
    /// Fixed height, and the block scrolls inside it. The frames can be set to 150 tall and
    /// arranged in four lines, so a band that grew to fit would take the window away from the
    /// settings it exists to serve (Florian, 2026-09-19, describing LumenUI's: never more
    /// than a third of the screen, scroll for the rest).
    /// </para>
    /// </summary>
    private float DrawPreviewBand(ImDrawListPtr dl, float left, float right, float top)
    {
        // Only where there is something to show. The suite-wide screens configure nothing
        // that can be drawn, and an empty band on them would be furniture.
        if (m_screen != Screen.PartyFrames || m_previewOf is null || !m_previewOf.HasPreview)
        {
            return top;
        }

        // Bindings is the one party tab the band says nothing about: it sets what a key and a
        // pointer do on a frame, and none of that changes how the frame looks. A preview that
        // cannot answer the question on screen is just height taken from the settings
        // (Florian, 2026-09-19).
        if (m_tabIndex[(int)m_screen] == TabIndexBindings)
        {
            return top;
        }

        float x = left + Tokens.Metric.SectionPaddingX;
        float wide = right - x - Tokens.Metric.SectionPaddingX;
        float barHeight = Tokens.Metric.PreviewBarHeight;
        float y = top;

        bool open = m_config.PreviewOpen;

        // The caret and the word are one target: a five-pixel triangle is not something to
        // ask anybody to hit.
        string title = Strings.Preview;
        float caret = Tokens.Metric.PreviewCaret;
        float titleWidth = MathF.Round(Ink.Measure(Ink.Role.Small, title).X);
        float headWidth = caret + Tokens.Space.Sm + titleWidth;

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(IdPreviewToggle, new Vector2(headWidth, barHeight));
        bool hovered = ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);

        if (ImGui.IsItemClicked())
        {
            m_config.PreviewOpen = !m_config.PreviewOpen;
            m_config.MarkDirty();
            open = m_config.PreviewOpen;
        }

        uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.Heading;
        float mid = MathF.Round(y + (barHeight * 0.5f));
        float half = caret * 0.5f;

        // Down when it is open, right when it is shut — the direction it will move in.
        if (open)
        {
            dl.AddTriangleFilled(
                new Vector2(x, mid - (half * 0.6f)),
                new Vector2(x + caret, mid - (half * 0.6f)),
                new Vector2(x + half, mid + (half * 0.8f)),
                ink);
        }
        else
        {
            dl.AddTriangleFilled(
                new Vector2(x, mid - half),
                new Vector2(x + (caret * 0.8f), mid),
                new Vector2(x, mid + half),
                ink);
        }

        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(MathF.Round(x + caret + Tokens.Space.Sm), Chrome.CenterY(y, barHeight, Ink.Role.Small)),
            ink,
            title);

        // How many stand-ins. The three party sizes somebody actually plays, as chips rather
        // than a selector: they are one tap each and all three are worth seeing at a glance.
        float chipX = x + headWidth + Tokens.Space.Lg;

        for (int i = 0; i < PreviewCounts.Length; i++)
        {
            float width = Chrome.MeasureTab(PreviewCountLabels[i]);

            if (Chrome.Tab(PreviewCountIds[i], PreviewCountLabels[i], chipX, y, width, m_config.PreviewCount == PreviewCounts[i]))
            {
                m_config.PreviewCount = PreviewCounts[i];
                m_config.MarkDirty();
            }

            chipX += width + Tokens.Metric.TabGap;
        }

        this.DrawPreviewEyeMenu(dl, x + wide, y, barHeight);

        y += barHeight + Tokens.Space.Sm;

        if (!open)
        {
            return y + Tokens.Space.Sm;
        }

        float height = Tokens.Metric.PreviewHeight;
        this.DrawPreviewViewport(dl, x, y, wide, height);

        return y + height + Tokens.Space.Md;
    }

    /// <summary>
    /// The eye at the right of the band, and the list of everything the preview can be asked
    /// to leave out.
    /// <para>
    /// The same switches as the eyes on the cards, in one place. The eye on a card is the
    /// quick way while you are already there; this is the way to take four things out at once
    /// without hunting across seven tabs for them.
    /// </para>
    /// </summary>
    private void DrawPreviewEyeMenu(ImDrawListPtr dl, float rightEdge, float y, float barHeight)
    {
        float size = Tokens.Metric.EyeGlyph;
        float eyeX = MathF.Round(rightEdge - size);
        float eyeY = MathF.Round(y + ((barHeight - size) * 0.5f));

        // The button says whether anything is hidden at all, which is the one thing somebody
        // needs to know without opening it — a preview missing a name for no visible reason
        // is a bug report waiting to happen.
        if (Chrome.EyeButton(IdPreviewEye, eyeX, eyeY, size, !PreviewMask.AnyHidden))
        {
            ImGui.OpenPopup(IdPreviewEyeMenu);
        }

        Chrome.TooltipOnHover(Strings.PreviewHideTooltip);

        string label = Strings.PreviewHide;
        float labelWidth = MathF.Round(Ink.Measure(Ink.Role.Small, label).X);
        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(eyeX - Tokens.Space.Sm - labelWidth, Chrome.CenterY(y, barHeight, Ink.Role.Small)),
            Tokens.Col.InkFaint,
            label);

        float pad = Tokens.Metric.PopupPadding;

        // 🔴 Menu rows, not settings rows, and two columns. Thirteen settings rows in one
        // column ran off the bottom of the window and still read as a form rather than as a
        // list (Florian, 2026-09-19, twice). A checklist is read down in one go.
        float pitch = Tokens.Metric.MenuRowHeight;
        float column = Tokens.Px(160f);
        float gutter = Tokens.Space.Xl;
        float width = (column * 2f) + gutter;
        int perColumn = (PreviewMask.All.Length + 1) / 2;

        // Right edge under the right edge of the eye, so the menu hangs off the button that
        // opened it. The window is the content plus its padding on both sides.
        ImGui.SetNextWindowPos(new Vector2(
            MathF.Round(eyeX + size - width - (pad * 2f)),
            y + barHeight + Tokens.Metric.PopupGap));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, Tokens.Line(1f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tokens.Col.PopupBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.PopupEdge);

        if (ImGui.BeginPopup(IdPreviewEyeMenu))
        {
            if (Chrome.ClosePopupRequested)
            {
                ImGui.CloseCurrentPopup();
            }

            Vector2 origin = ImGui.GetCursorScreenPos();

            for (int i = 0; i < PreviewMask.All.Length; i++)
            {
                PreviewPart part = PreviewMask.All[i];
                int col = i / perColumn;
                int row = i - (col * perColumn);
                float rowX = origin.X + (col * (column + gutter));
                float rowY = origin.Y + (row * pitch);

                if (Chrome.MenuTickRow(PreviewPartIds[i], PreviewPartLabels[i], rowX, rowY, column, PreviewMask.Shows(part)))
                {
                    PreviewMask.Toggle(part);
                }
            }

            float below = origin.Y + (perColumn * pitch) + Tokens.Space.Sm;

            // Ticked means shown, so this is the way back rather than a reset of settings —
            // nothing here was ever written down. It keeps its place whether or not it can be
            // pressed, so the menu does not change height as things are switched off.
            if (PreviewMask.AnyHidden
                && Chrome.PillButton(IdPreviewShowAll, Strings.PreviewShowAll, origin.X, below))
            {
                PreviewMask.ShowAll();
            }

            // 🔴 Back to the corner first. Every row above placed itself with
            // SetCursorScreenPos, and each of those invisible buttons pushed ImGui's own
            // cursor further down — so a Dummy from wherever it ended up measured the list
            // twice over and left the popup half empty (Florian, 2026-09-19: "immer noch zu
            // lang"). The rows are drawn at absolute positions; the size has to be stated the
            // same way.
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, below - origin.Y + Tokens.Metric.ButtonHeight));
            ImGui.EndPopup();
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    /// <summary>
    /// The dark area the frames are drawn into: clipped to its own rectangle, scrolled when
    /// the block is larger than it, and centred when it is smaller.
    /// </summary>
    private void DrawPreviewViewport(ImDrawListPtr dl, float x, float y, float width, float height)
    {
        // Behind the frames rather than the window's own surface. What a frame really sits on
        // is the game, which is anything at all, so the honest backdrop here is a neutral
        // dark that neither flatters nor fights the colours being set.
        dl.AddRectFilled(new Vector2(x, y), new Vector2(x + width, y + height), Tokens.Col.Input, Tokens.Radius.Control);
        dl.AddRect(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            Tokens.Col.Hairline,
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Transparent);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Tokens.Col.ScrollTrack);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Tokens.Col.ScrollGrab);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, Tokens.Metric.ScrollbarWidth);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, Tokens.Radius.Small);

        ImGui.SetCursorScreenPos(new Vector2(x, y));

        if (ImGui.BeginChild(IdPreview, new Vector2(width, height), false, ImGuiWindowFlags.HorizontalScrollbar))
        {
            float pad = Tokens.Metric.PreviewPadding;
            Vector2 block = m_previewOf!.PreviewSize(m_config.PreviewCount);
            Vector2 content = block + new Vector2(pad * 2f, pad * 2f);

            // Centred while it fits, hard against the padding once it does not — a block that
            // stayed centred while it overflowed would put its middle in view and cut both
            // ends, which is the one arrangement that shows neither edge.
            float offsetX = content.X < width ? MathF.Round((width - content.X) * 0.5f) : 0f;
            float offsetY = content.Y < height ? MathF.Round((height - content.Y) * 0.5f) : 0f;

            ImGui.SetCursorPos(new Vector2(offsetX + pad, offsetY + pad));

            // Read after the cursor is placed, so it already carries the scroll.
            Vector2 origin = ImGui.GetCursorScreenPos();

            // What the scrollbars measure themselves against. Nothing is drawn by it — the
            // frames go straight into the draw list, which ImGui cannot size a window from.
            ImGui.Dummy(block);

            m_previewOf.DrawPreview(ImGui.GetWindowDrawList(), origin, m_config.PreviewCount);
        }

        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }

    /// <summary>
    /// The tab row: free-standing chips on the surface, with no line tying them to anything.
    /// The rule beneath is drawn by the caller and is what separates them from the content.
    /// Returns the y just below the chips.
    /// </summary>
    private float DrawTabs(ImDrawListPtr dl, float left, float right, float top)
    {
        _ = dl;
        _ = right;

        string[] tabs = TabsFor(m_screen);
        int screenIndex = (int)m_screen;
        if (m_tabIndex[screenIndex] >= tabs.Length)
        {
            m_tabIndex[screenIndex] = 0;
        }

        float tabTop = top + Tokens.Space.Md;
        float x = left + Tokens.Metric.SectionPaddingX;
        for (int i = 0; i < tabs.Length && i < TabIds.Length; i++)
        {
            float width = Chrome.MeasureTab(tabs[i]);
            if (Chrome.Tab(TabIds[i], tabs[i], x, tabTop, width, m_tabIndex[screenIndex] == i))
            {
                m_tabIndex[screenIndex] = i;
            }

            x += width + Tokens.Metric.TabGap;
        }

        return tabTop + Tokens.Metric.TabHeight + Tokens.Space.Md;
    }

    /// <summary>
    /// One central strip per screen: what you are looking at on the left, the actions that
    /// apply to the whole screen on the right. HUD modules add their on/off switch and the
    /// appearance clipboard here — the same strip will carry Player Bars, Target and
    /// Target-of-Target without any extra work.
    /// </summary>
    private float DrawScreenHeader(ImDrawListPtr dl, float left, float right, float top)
    {
        bool isModule = m_screen == Screen.PartyFrames;
        float height = Tokens.Metric.ModuleHeaderHeight;
        float x = left + Tokens.Metric.SectionPaddingX;

        // Told every frame, not only on the frames where the strip is drawn — that is what
        // makes the step back disappear when you leave the module.
        m_appearance.NoteOwner(isModule ? m_partyFrames : null);

        if (isModule)
        {
            float switchY = MathF.Round(top + ((height - Tokens.Metric.SwitchHeight) * 0.5f));
            if (Chrome.Switch(IdModuleSwitch, x, switchY, m_config.PartyFramesEnabled, true))
            {
                m_config.PartyFramesEnabled = !m_config.PartyFramesEnabled;
                m_config.MarkDirty();
            }

            x += Tokens.Metric.SwitchWidth + Tokens.Space.Md;
        }

        string title = ScreenLabel(m_screen);
        Ink.Draw(
            dl,
            Ink.Role.ScreenTitle,
            new Vector2(x, Chrome.CenterY(top, height, Ink.Role.ScreenTitle)),
            Tokens.Col.Heading,
            title);

        if (isModule)
        {
            x += MathF.Round(Ink.Measure(Ink.Role.ScreenTitle, title).X) + Tokens.Space.Md;
            string state = m_config.PartyFramesEnabled ? Strings.StateOn : Strings.StateOff;
            Ink.Draw(dl, Ink.Role.Small, new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Small)), Tokens.Col.InkFaint, state);
        }

        // Right to left: Defaults sits outermost, so it stays in the same place on every
        // screen whether or not a clipboard is present.
        float buttonY = MathF.Round(top + ((height - Tokens.Metric.ButtonHeight) * 0.5f));
        float cursor = right - Tokens.Metric.SectionPaddingX - Chrome.MeasureButton(Strings.Defaults);
        Chrome.Button(IdDefaults, Strings.Defaults, cursor, buttonY, false, Strings.DefaultsDisabled);

        if (isModule)
        {
            m_appearance.Draw(m_partyFrames, cursor - Tokens.Space.Md, buttonY);
        }

        return top + height;
    }

    private void DrawContent(float left, float right, float top, float bottom)
    {
        float width = right - left;
        float height = bottom - top;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        // Transparent rather than filled: the surface behind it is already the same colour, and
        // a filled child would paint a square corner back over the window's rounded bottom right.
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Transparent);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Tokens.Col.ScrollTrack);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Tokens.Col.ScrollGrab);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, Tokens.Metric.ScrollbarWidth);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, Tokens.Radius.Small);

        ImGui.SetCursorScreenPos(new Vector2(left, top));
        if (ImGui.BeginChild(IdContent, new Vector2(width, height)))
        {
            // ImGui forces WindowPadding to zero on a child window without a border, so the
            // padding is applied here by hand. Without it the content sits flush against the
            // nav rail while everything outside the child keeps its margin.
            float padX = Tokens.Metric.SectionPaddingX;
            float padY = Tokens.Metric.SectionPaddingY;
            ImGui.SetCursorPos(new Vector2(padX, padY));

            float inner = width - (padX * 2f);
            int tab = m_tabIndex[(int)m_screen];

            if (m_screen == Screen.Global)
            {
                m_global.Draw(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 0)
            {
                m_partyFrames.Draw(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 1)
            {
                m_partyFrames.DrawText(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 2)
            {
                m_partyFrames.DrawIcons(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 3)
            {
                m_partyFrames.DrawLayout(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 4)
            {
                m_partyFrames.DrawAuras(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == 5)
            {
                m_partyFrames.DrawMarks(inner);
            }
            else if (m_screen == Screen.PartyFrames && tab == TabIndexBindings)
            {
                m_partyFrames.DrawBindings(inner);
            }
            else
            {
                this.DrawScreenPlaceholder(inner);
            }
        }

        ImGui.EndChild();

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }

    /// <summary>
    /// Stands in for the screens that have no controls yet. The screen is already named by
    /// the header above, so this only says that there is nothing behind the tab.
    /// </summary>
    private void DrawScreenPlaceholder(float width)
    {
        // Drawn as a section head rather than as loose text, so an empty screen sits at the
        // same sizes and on the same rhythm as one that has controls. Two type scales that
        // nearly match are worse than one used twice.
        Vector2 origin = ImGui.GetCursorScreenPos();
        float used = Chrome.SectionHeader(Strings.NothingHereYet, Strings.SkeletonNote, origin.X, origin.Y);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, used + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>One row of the navigation tree, described rather than drawn.</summary>
    private readonly struct NavRow
    {
        public readonly string Id;
        public readonly string Label;
        public readonly Screen Target;
        public readonly bool Soon;
        public readonly bool IsSeparator;

        public NavRow(string id, string label, Screen target)
        {
            this.Id = id;
            this.Label = label;
            this.Target = target;
            this.Soon = false;
            this.IsSeparator = false;
        }

        private NavRow(string id, string label, Screen target, bool soon, bool separator)
        {
            this.Id = id;
            this.Label = label;
            this.Target = target;
            this.Soon = soon;
            this.IsSeparator = separator;
        }

        public static NavRow Separator() => new(string.Empty, string.Empty, Screen.Global, false, true);

        public static NavRow NotYet(string id, string label) => new(id, label, Screen.Global, true, false);
    }
}
