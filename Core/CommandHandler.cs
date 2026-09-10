using System;
using Dalamud.Game.Command;
using WispUI.Interface;
using WispUI.Localization;

namespace WispUI.Core;

/// <summary>Owns the slash commands and hands them back cleanly on unload.</summary>
internal sealed class CommandHandler : IDisposable
{
    private readonly ConfigWindow m_window;

    public CommandHandler(ConfigWindow window)
    {
        m_window = window;

        Services.Commands.AddHandler(Strings.Command, new CommandInfo(this.OnCommand)
        {
            HelpMessage = Strings.CommandHelp,
        });
    }

    public void Dispose()
    {
        Services.Commands.RemoveHandler(Strings.Command);
    }

    private void OnCommand(string command, string arguments)
    {
        m_window.Toggle();
    }
}
