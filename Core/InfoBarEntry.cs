using System;
using Dalamud.Game.Gui.Dtr;
using WispUI.Interface;
using WispUI.Localization;

namespace WispUI.Core;

/// <summary>
/// An entry in the server info bar, next to the clock, that opens the settings window.
/// <para>
/// This is the closest thing to a button in the game's own system menu that a plugin can
/// have: Dalamud's system-menu integration is internal, so only Dalamud itself may put
/// entries there. The info bar is the supported route, and it is one click either way.
/// </para>
/// </summary>
internal sealed class InfoBarEntry : IDisposable
{
    private readonly ConfigWindow m_window;
    private IDtrBarEntry? m_entry;

    public InfoBarEntry(ConfigWindow window)
    {
        m_window = window;
    }

    /// <summary>Creates or removes the entry to match the user's preference.</summary>
    public void Apply(bool shown)
    {
        if (!shown)
        {
            this.Dispose();
            return;
        }

        if (m_entry is not null)
        {
            m_entry.Shown = true;
            return;
        }

        try
        {
            m_entry = Services.DtrBar.Get(Strings.PluginName, Strings.PluginName);
            m_entry.Tooltip = Strings.InfoBarTooltip;
            m_entry.OnClick = _ => m_window.Toggle();
            m_entry.Shown = true;
        }
        catch (Exception ex)
        {
            // The info bar is a convenience. If it cannot be had, the plugin carries on.
            Services.Log.Warning(ex, "Could not create the server info bar entry.");
            m_entry = null;
        }
    }

    public void Dispose()
    {
        m_entry?.Remove();
        m_entry = null;
    }
}
