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
        // what the game's own data says about the party and about every effect on the player
        // and on the target.
        //
        // 🔴 The question it was written for is settled — the sheet CANNOT tell a mitigation
        // from any other benefit (measured 2026-09-18, see DumpPlayerStatuses). It stays
        // anyway, because asking the running game beat guessing three times over. Reach for
        // this before assuming anything about live data (CLAUDE.md: after three tries the
        // assumption is wrong, not the attempt).
        if (arguments.Trim().Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Data.StatusData.DumpPlayerStatuses();

            // The cast bar's nodes, for when a patch moves the one the slide window sits on.
            NativeUi.DumpCastBar();
            return;
        }

        m_window.Toggle();
    }
}
