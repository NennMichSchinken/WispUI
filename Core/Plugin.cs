using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

        Services.PluginInterface.UiBuilder.Draw += m_windows.Draw;
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
        Services.PluginInterface.UiBuilder.Draw -= m_windows.Draw;

        m_infoBar.Dispose();
        m_commands.Dispose();
        m_windows.RemoveAllWindows();

        // A pending change must not be lost just because the plugin is going away.
        m_config.FlushPending();
        Fonts.Dispose();
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
