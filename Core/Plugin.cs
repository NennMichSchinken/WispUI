using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using WispUI.Hud.PartyFrames;
using WispUI.Interface;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Core;

/// <summary>
/// The entry point. It wires the pieces together and takes them apart again — no drawing,
/// no game logic, so the lifecycle stays readable in one screen.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private readonly Configuration m_config;
    private readonly WindowSystem m_windows = new(Strings.PluginName);
    private readonly ConfigWindow m_configWindow;
    private readonly CommandHandler m_commands;
    private readonly InfoBarEntry m_infoBar;
    private readonly HudManager m_hud = new();

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Services.Initialize(pluginInterface);

        m_config = Configuration.Load();
        Scaling.Commit(m_config.Scale);
        Scaling.LogGameScaleReadings();

        m_configWindow = new ConfigWindow(m_config);
        m_windows.AddWindow(m_configWindow);
        m_commands = new CommandHandler(m_configWindow);

        m_infoBar = new InfoBarEntry(m_configWindow);
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
        m_configWindow.InfoBarPreferenceChanged += this.OnInfoBarPreferenceChanged;

        m_hud.Add(new PartyFramesElement(m_config));

        // Dalamud only keeps the game's cursor away from a plugin window while this is on. It
        // is the default, but it is a single switch shared by everything running in the game,
        // and anything that turns it off leaves our window taking its pointer from whatever
        // door happens to be behind it. Asserted once at load rather than fought for per frame.
        Services.PluginInterface.UiBuilder.OverrideGameCursor = true;

        Services.PluginInterface.UiBuilder.Draw += this.OnDraw;
        Services.PluginInterface.UiBuilder.OpenMainUi += m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenConfigUi += m_configWindow.Toggle;
        Services.Framework.Update += this.OnUpdate;
    }

    public void Dispose()
    {
        Services.Framework.Update -= this.OnUpdate;
        m_configWindow.InfoBarPreferenceChanged -= this.OnInfoBarPreferenceChanged;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenMainUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.Draw -= this.OnDraw;

        m_configWindow.ReleaseCursor();
        m_infoBar.Dispose();
        m_commands.Dispose();
        m_windows.RemoveAllWindows();

        // A pending change must not be lost just because the plugin is going away.
        m_config.FlushPending();
        Fonts.Dispose();
    }

    /// <summary>
    /// One frame of everything WispUI draws. The font locks are taken here rather than inside
    /// a window, because the HUD writes text too and they must be taken exactly once — twice
    /// would allocate twice, which is the trap from session 2.
    /// <para>
    /// The HUD goes first. It paints into the background draw list, so a settings window is
    /// never hidden behind the element it configures.
    /// </para>
    /// </summary>
    private void OnDraw()
    {
        Ink.BeginFrame();
        m_hud.Draw();
        m_windows.Draw();
    }

    /// <summary>
    /// Kept deliberately thin: the debounced configuration write is all that belongs on the
    /// tick. Heavy work goes neither here nor into the draw path.
    /// </summary>
    private void OnUpdate(IFramework framework)
    {
        m_config.Tick();
    }

    private void OnInfoBarPreferenceChanged()
    {
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
    }
}
