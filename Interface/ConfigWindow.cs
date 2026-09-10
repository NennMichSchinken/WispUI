using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using WispUI.Core;
using WispUI.Interface.Screens;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface;

/// <summary>
/// The settings window: title bar, navigation tree, module header, tabs, footer.
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
    private const string IdNews = "##wisp-news";
    private const string IdEditMode = "##wisp-editmode";
    private const string IdModuleSwitch = "##wisp-module-switch";
    private const string IdCopy = "##wisp-copy";
    private const string IdPaste = "##wisp-paste";
    private const string IdDefaults = "##wisp-defaults";
    private const string IdApply = "##wisp-apply";
    private const string IdFooterClose = "##wisp-footer-close";

    private const uint Transparent = 0x00000000u;

    /// <summary>Tab hit boxes only need to be unique while their screen is on show.</summary>
    private static readonly string[] TabIds = { "##wisp-tab0", "##wisp-tab1", "##wisp-tab2" };

    private static readonly string[] TabsGlobal = { Strings.TabBase };
    private static readonly string[] TabsProfile = { Strings.TabBase };
    private static readonly string[] TabsPartyFrames = { Strings.TabBase, Strings.TabLayout, Strings.TabAuras };

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
    private readonly GlobalScreen m_global;

    /// <summary>Built once — the version never changes while the plugin is loaded.</summary>
    private readonly string m_versionChip;
    private readonly string m_versionBadge;

    /// <summary>One remembered tab per screen, so switching modules does not lose your place.</summary>
    private readonly int[] m_tabIndex = new int[Enum.GetValues<Screen>().Length];

    private Screen m_screen = Screen.PartyFrames;

    // The placeholder heading is built from two parts. Rebuilding it every frame would
    // allocate a string per frame, so it is cached and only rebuilt when the pair changes.
    private string m_heading = string.Empty;
    private Screen m_headingScreen = (Screen)(-1);
    private int m_headingTab = -1;

    public ConfigWindow(Configuration config)
        : base(
            Strings.WindowId,
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse)
    {
        m_config = config;
        m_global = new GlobalScreen(config);
        m_global.InfoBarPreferenceChanged += () => this.InfoBarPreferenceChanged?.Invoke();

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

    public override void PreDraw()
    {
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
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(3);
    }

    public override void Draw()
    {
        // Takes this frame's font locks once, so nothing below allocates to write text.
        Ink.BeginFrame();

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 origin = ImGui.GetWindowPos();
        Vector2 size = ImGui.GetWindowSize();

        float border = Tokens.Metric.WindowBorder;
        float left = origin.X + border;
        float right = origin.X + size.X - border;
        float top = origin.Y + border;
        float bottom = origin.Y + size.Y - border;

        float titleHeight = Tokens.Metric.TitleBarHeight;
        float footerHeight = Tokens.Metric.FooterHeight;
        float bodyTop = top + titleHeight;
        float bodyBottom = bottom - footerHeight;

        dl.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Tokens.Col.Panel);

        this.DrawTitleBar(dl, left, right, top, titleHeight);
        this.DrawNav(dl, left, bodyTop, bodyBottom);
        this.DrawModuleArea(dl, left + Tokens.Metric.NavWidth, right, bodyTop, bodyBottom);
        this.DrawFooter(dl, left, right, bodyBottom, footerHeight);

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
    /// The top and bottom rings run the full width and the side rings fill in between them,
    /// so each corner takes the colour of the horizontal edge. That is a simplification: the
    /// game draws its corners as artwork, which a rectangle cannot reproduce.
    /// </para>
    /// </summary>
    private static void DrawWindowEdge(ImDrawListPtr dl, Vector2 origin, Vector2 size)
    {
        float ring = Tokens.Line(1f);
        int rings = Tokens.Col.EdgeTop.Length;

        for (int i = 0; i < rings; i++)
        {
            float inset = i * ring;
            float left = origin.X + inset;
            float right = origin.X + size.X - inset;
            float top = origin.Y + inset;
            float bottom = origin.Y + size.Y - inset;

            dl.AddRectFilled(new Vector2(left, top), new Vector2(right, top + ring), Tokens.Col.EdgeTop[i]);
            dl.AddRectFilled(new Vector2(left, bottom - ring), new Vector2(right, bottom), Tokens.Col.EdgeBottom[i]);

            uint side = Tokens.Col.EdgeSide[i];
            dl.AddRectFilled(new Vector2(left, top + ring), new Vector2(left + ring, bottom - ring), side);
            dl.AddRectFilled(new Vector2(right - ring, top + ring), new Vector2(right, bottom - ring), side);
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

    private void DrawTitleBar(ImDrawListPtr dl, float left, float right, float top, float height)
    {
        Vector2 min = new(left, top);
        Vector2 max = new(right, top + height);
        // Lit at the very top and fading down into the surface colour, as measured.
        Chrome.VerticalFill(dl, min, max, Tokens.Col.TitleBarTop, Tokens.Col.TitleBar);

        // The three-pixel rule that closes the title bar: dark, surface, light. It fades out
        // towards the corners rather than running into the frame.
        float ruleY = max.Y - Tokens.Metric.TitleRuleHeight;
        float step = Tokens.Line(1f);
        for (int i = 0; i < Tokens.Col.TitleRule.Length; i++)
        {
            Chrome.FadingHairline(
                dl,
                left,
                right,
                ruleY + (i * step),
                Tokens.Col.TitleRule[i],
                Tokens.Metric.TitleRuleFade);
        }

        float x = left + Tokens.Metric.SectionPaddingX;
        Ink.Draw(dl, Ink.Role.Title, new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Title)), Tokens.Col.Ink, Strings.WindowTitle);
        x += MathF.Round(Ink.Measure(Ink.Role.Title, Strings.WindowTitle).X) + Tokens.Space.Lg;

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Body)),
            Tokens.Col.InkFaint,
            ScreenLabel(m_screen));

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

        dl.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Tokens.Col.Rail);
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

        // Off until there is a HUD element to move. A disabled control owes a reason.
        float alpha = Tokens.Col.DisabledAlpha;
        Chrome.VerticalFill(
            dl,
            min,
            max,
            Tokens.Col.Faded(Tokens.Col.Control, alpha),
            Tokens.Col.Faded(Tokens.Col.Control2, alpha),
            Tokens.Radius.Control);
        dl.AddRect(min, max, Tokens.Col.Faded(Tokens.Col.ControlEdge, alpha), Tokens.Radius.Control, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        float textX = MathF.Round(x + ((width - Ink.Measure(Ink.Role.Body, Strings.EditMode).X) * 0.5f));
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(textX, Chrome.CenterY(y, height, Ink.Role.Body)),
            Tokens.Col.Faded(Tokens.Col.Ink, alpha),
            Strings.EditMode);

        Chrome.TooltipOnHover(Strings.EditModeDisabled);
    }

    private void DrawNewsCard(ImDrawListPtr dl, float x, float y, float width)
    {
        float height = Tokens.Metric.NavCardHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(IdNews, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();

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

        if (m_screen == Screen.PartyFrames)
        {
            y = this.DrawModuleHeader(dl, left, right, y);
        }

        this.DrawContent(left, right, y, bottom);
    }

    /// <summary>Draws the tab row and returns the y just below it.</summary>
    private float DrawTabs(ImDrawListPtr dl, float left, float right, float top)
    {
        float barHeight = Tokens.Metric.TabBarHeight;
        float lineY = top + barHeight - Tokens.Line(1f);
        Chrome.Hairline(dl, left, right, lineY, Tokens.Col.EdgeDim);

        string[] tabs = TabsFor(m_screen);
        int screenIndex = (int)m_screen;
        if (m_tabIndex[screenIndex] >= tabs.Length)
        {
            m_tabIndex[screenIndex] = 0;
        }

        float x = left + Tokens.Metric.SectionPaddingX;
        float tabTop = lineY - Tokens.Metric.TabHeight;
        for (int i = 0; i < tabs.Length && i < TabIds.Length; i++)
        {
            float width = Chrome.MeasureTab(tabs[i]);
            if (Chrome.Tab(TabIds[i], tabs[i], x, tabTop, width, m_tabIndex[screenIndex] == i))
            {
                m_tabIndex[screenIndex] = i;
            }

            x += width + Tokens.Metric.TabGap;
        }

        return top + barHeight;
    }

    /// <summary>
    /// The module header: the switch on the left, the appearance clipboard on the right.
    /// One central spot per module, reachable from every tab — the same header will carry
    /// Player Bars, Target and Target-of-Target without any extra work.
    /// </summary>
    private float DrawModuleHeader(ImDrawListPtr dl, float left, float right, float top)
    {
        float height = Tokens.Metric.ModuleHeaderHeight;
        dl.AddRectFilled(new Vector2(left, top), new Vector2(right, top + height), Tokens.Col.PanelSoft);
        Chrome.Hairline(dl, left, right, top + height - Tokens.Line(1f), Tokens.Col.Hairline);

        float x = left + Tokens.Metric.SectionPaddingX;
        float switchY = MathF.Round(top + ((height - Tokens.Metric.SwitchHeight) * 0.5f));
        if (Chrome.Switch(IdModuleSwitch, x, switchY, m_config.PartyFramesEnabled, true))
        {
            m_config.PartyFramesEnabled = !m_config.PartyFramesEnabled;
            m_config.MarkDirty();
        }

        x += Tokens.Metric.SwitchWidth + Tokens.Space.Md;
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Body)),
            Tokens.Col.Ink,
            Strings.NavPartyFrames);
        x += MathF.Round(Ink.Measure(Ink.Role.Body, Strings.NavPartyFrames).X) + Tokens.Space.Md;

        string state = m_config.PartyFramesEnabled ? Strings.StateOn : Strings.StateOff;
        Ink.Draw(dl, Ink.Role.Small, new Vector2(x, Chrome.CenterY(top, height, Ink.Role.Small)), Tokens.Col.InkFaint, state);

        float buttonY = MathF.Round(top + ((height - Tokens.Metric.ButtonHeight) * 0.5f));
        float pasteWidth = Chrome.MeasureButton(Strings.PasteAppearance);
        float copyWidth = Chrome.MeasureButton(Strings.CopyAppearance);
        float pasteX = right - Tokens.Metric.SectionPaddingX - pasteWidth;
        float copyX = pasteX - Tokens.Space.Sm - copyWidth;

        Chrome.Button(IdCopy, Strings.CopyAppearance, copyX, buttonY, false, Strings.ClipboardDisabled);
        Chrome.Button(IdPaste, Strings.PasteAppearance, pasteX, buttonY, false, Strings.ClipboardDisabled);

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

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Tokens.Col.PanelSoft);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Tokens.Col.ScrollTrack);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Tokens.Col.ScrollGrab);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, Tokens.Col.ScrollGrabHover);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, Tokens.Metric.ScrollbarWidth);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, Tokens.Radius.Small);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Tokens.Metric.SectionPaddingX, Tokens.Metric.SectionPaddingY));

        ImGui.SetCursorScreenPos(new Vector2(left, top));
        if (ImGui.BeginChild(IdContent, new Vector2(width, height)))
        {
            if (m_screen == Screen.Global)
            {
                m_global.Draw(width);
            }
            else
            {
                this.DrawScreenPlaceholder(width);
            }
        }

        ImGui.EndChild();

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(5);
    }

    /// <summary>
    /// Stands in for the real screens. It names the tab you are on so the frame can be
    /// checked without pretending there are controls behind it.
    /// </summary>
    private void DrawScreenPlaceholder(float width)
    {
        string[] tabs = TabsFor(m_screen);
        int tabIndex = Math.Clamp(m_tabIndex[(int)m_screen], 0, tabs.Length - 1);
        if (m_headingScreen != m_screen || m_headingTab != tabIndex)
        {
            m_headingScreen = m_screen;
            m_headingTab = tabIndex;
            m_heading = ScreenLabel(m_screen) + " · " + tabs[tabIndex];
        }

        Ink.Push(Ink.Role.Title);
        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.Heading);
        ImGui.TextUnformatted(m_heading);
        ImGui.PopStyleColor();
        Ink.Pop(Ink.Role.Title);

        ImGui.Dummy(new Vector2(0f, Tokens.Space.Md));

        Ink.Push(Ink.Role.Body);
        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.InkDim);
        ImGui.TextUnformatted(Strings.NothingHereYet);
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.InkFaint);
        ImGui.PushTextWrapPos(width - (Tokens.Metric.SectionPaddingX * 2f));
        ImGui.TextUnformatted(Strings.SkeletonNote);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        Ink.Pop(Ink.Role.Body);
    }

    private void DrawFooter(ImDrawListPtr dl, float left, float right, float top, float height)
    {
        Vector2 min = new(left, top);
        Vector2 max = new(right, top + height);
        dl.AddRectFilled(min, max, Tokens.Col.Footer);
        Chrome.Hairline(dl, left, right, top, Tokens.Col.EdgeDim);

        float y = MathF.Round(top + ((height - Tokens.Metric.ButtonHeight) * 0.5f));
        Chrome.Button(IdDefaults, Strings.Defaults, left + Tokens.Space.Lg, y, false, Strings.DefaultsDisabled);

        float closeWidth = Chrome.MeasureButton(Strings.Close);
        float applyWidth = Chrome.MeasureButton(Strings.Apply);
        float closeX = right - Tokens.Space.Lg - closeWidth;
        float applyX = closeX - Tokens.Space.Md - applyWidth;

        Chrome.Button(IdApply, Strings.Apply, applyX, y, false, Strings.ApplyDisabled);
        if (Chrome.Button(IdFooterClose, Strings.Close, closeX, y, true))
        {
            this.IsOpen = false;
        }
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
