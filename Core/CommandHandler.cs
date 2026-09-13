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
        // One argument, and it is a diagnostic rather than a feature: "/wisp status" writes
        // what the game's own data says about every effect currently on the player.
        //
        // 🔴 It exists to settle a question rather than to be used. Whether the status sheet
        // can tell a mitigation from any other benefit decides whether the mitigation row can
        // be data-driven or has to be a hand-kept list of ids, and that is not answerable
        // outside the game. Guessing it would be the fourth guess in a row (CLAUDE.md: after
        // three tries the assumption is wrong, not the attempt).
        if (arguments.Trim().Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Data.StatusData.DumpPlayerStatuses();
            return;
        }

        m_window.Toggle();
    }
}
