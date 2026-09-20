using System;
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

    /// <summary>The one feature that hooks the game. Owned here so it is always disposed.</summary>
    private readonly MouseoverCasting m_mouseover;

    /// <summary>Watches the job and puts the matching profile on. Two numbers when idle.</summary>
    private readonly ProfileWatch m_profiles;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Services.Initialize(pluginInterface);

        m_config = Configuration.Load();

        // Before anything asks for a face by name. Reading the folder touches the disk, so it
        // happens once here and again only when the player asks for it.
        FontLibrary.Refresh();
        Data.JobList.Load();

        // The status sheet, flattened once. Every frame asks it about every effect on every
        // member, and a sheet read never belongs in that path (CLAUDE.md §7.3).
        Data.StatusData.Prime(Hud.PartyFrames.PartySnapshot.MaxAuras);

        Scaling.CommitAtLoad(m_config.Scale);
        Scaling.LogGameScaleReadings();

        // Kept in a local, because the settings window draws this same element as its
        // preview. One object, one set of drawing code, two places it appears.
        var frames = new PartyFramesElement(m_config);
        m_hud.Add(frames);

        m_configWindow = new ConfigWindow(m_config, frames);
        m_windows.AddWindow(m_configWindow);
        m_commands = new CommandHandler(m_configWindow);

        m_infoBar = new InfoBarEntry(m_configWindow);
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
        m_configWindow.InfoBarPreferenceChanged += this.OnInfoBarPreferenceChanged;

        // Coming out of edit mode puts the settings window back. Going in closed it, and
        // finishing on an empty screen with nothing to return to is a dead end (Florian,
        // 2026-09-12). Subscribed here rather than in the window, because this is where it
        // can be released again.
        EditMode.Finished += this.OnEditModeFinished;

        // Made now, put in place only if the player has asked for it. The hook it owns is the
        // suite's one reach into what a key press does, so it is never installed on spec.
        //
        // 🔴 Made, and left alone. Asking which job is being played reads the object table,
        // and the object table may only be read on the main thread — which the constructor is
        // not (verified the hard way: the plugin failed to load, 2026-09-19). Nothing is lost
        // by waiting: the hook starts out uninstalled, which is the right state until a tick
        // says otherwise, and the first tick is a few milliseconds away.
        m_mouseover = new MouseoverCasting();

        // Same reason as the hook above: it wants to know which job is being played, and
        // the constructor is not the main thread. It works that out on its first tick.
        m_profiles = new ProfileWatch(m_config);

        // The pointer switch is shared by everything running in the game, so its state is put
        // back to the game's at load rather than assumed. From here on it has one writer and
        // is settled once per frame.
        NativeUi.ReleaseCursor();

        Services.PluginInterface.UiBuilder.Draw += this.OnDraw;
        Services.PluginInterface.UiBuilder.OpenMainUi += m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenConfigUi += m_configWindow.Toggle;
        Services.Framework.Update += this.OnUpdate;
    }

    public void Dispose()
    {
        Services.Framework.Update -= this.OnUpdate;
        EditMode.Finished -= this.OnEditModeFinished;
        m_configWindow.InfoBarPreferenceChanged -= this.OnInfoBarPreferenceChanged;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenMainUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.Draw -= this.OnDraw;

        m_mouseover.Dispose();

        // A piece of the player's interface must never stay hidden by something that has
        // stopped running. Nothing happens here if we never hid it.
        NativeUi.RestoreNativePartyList();

        m_configWindow.ReleaseCursor();
        m_infoBar.Dispose();
        m_commands.Dispose();
        m_windows.RemoveAllWindows();

        // A pending change must not be lost just because the plugin is going away. The
        // write puts the live settings back into the profile they belong to on its own.
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

        // After everything has said whether the mouse is on it. One writer, one decision, and
        // the switch goes back to the game's the moment nothing of ours is under the pointer.
        NativeUi.SettleCursor();
    }

    /// <summary>
    /// Kept deliberately thin: the debounced configuration write is all that belongs on the
    /// tick. Heavy work goes neither here nor into the draw path.
    /// </summary>
    private void OnUpdate(IFramework framework)
    {
        m_config.Tick();

        // 🔴 Before anything else on the tick, and never from a drawing path: applying a
        // scale rebuilds the font atlas, and doing that mid-frame leaves every piece of
        // text already drawn pointing into a freed one. See Scaling.Request.
        Scaling.Settle();

        // Before anything reads a setting this frame: a job change means the settings the
        // rest of the tick is about may be the wrong ones.
        m_profiles.Tick();

        // The hook goes in and comes out with the list rather than sitting installed and
        // inert, so a player who has never named a spell never carries it.
        this.SyncMouseover();

        // On the tick rather than in the draw, because the game puts its own list back up on
        // its own — a zone change, a duty, any rebuild of the interface — and the tick runs
        // through all of that while drawing does not. Only a difference is ever written.
        //
        // The list goes only while there is something of ours in its place: switching the
        // module off gives it back without the player having to remember a second tick.
        NativeUi.SettleNativePartyList(
            m_config.PartyFramesEnabled && m_config.PartyFrames.HideNativePartyList);

        this.SyncHudFonts();

        // Work that has to keep running while nothing is drawn, or that costs too much to do
        // per frame. Each element throttles its own.
        m_hud.Tick();
    }

    /// <summary>
    /// Hands the hook the spells the job being played redirects, which is also what decides
    /// whether the hook is in place at all.
    /// <para>
    /// Asked of the job rather than kept: changing job changes the list, and there is no
    /// event for it that is cheaper than the lookup.
    /// </para>
    /// </summary>
    private void SyncMouseover() =>
        m_mouseover.Sync(m_config.PartyFrames.Mouseover.For(Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u));

    /// <summary>
    /// Keeps the HUD's font handles in step with the face and the text sizes in use.
    /// <para>
    /// On the tick rather than in the draw, because building these throws the font atlas away
    /// and makes a new one. Doing that between the frame's font locks and the text they were
    /// taken for would pull a face out from under something already drawing with it.
    /// </para>
    /// <para>
    /// And only once the settings have gone quiet. A size comes from a slider, and rebuilding
    /// on every pixel of a drag would make the drag unusable — so this waits for the same
    /// pause the configuration is written on. The cost is that a size change shows up about a
    /// second after the drag ends, which is the honest price of a face that is sharp at any
    /// size (Florian, 2026-09-12).
    /// </para>
    /// </summary>
    private void SyncHudFonts()
    {
        Configuration.PartyFramesConfig cfg = m_config.PartyFrames;

        // 🔴 WorldPx, matching what the draw path asks for. These two have to agree: the
        // handles are built for exactly the sizes in use, and a size built at one scale and
        // asked for at another falls back to a stretched glyph nobody chose.
        // On the stack, so the tick allocates nothing. These are the three texts a frame can
        // carry; two of them are usually the same size, and SyncHud drops the duplicate.
        Span<float> sizes = stackalloc float[3];
        sizes[0] = Tokens.WorldPx(cfg.NameSize);
        sizes[1] = Tokens.WorldPx(cfg.HpTextSize);
        sizes[2] = Tokens.WorldPx(cfg.PartyNumberSize);

        Fonts.SyncHud(!m_config.HasPendingChanges, cfg.FontName, HudText.WeightAt(cfg.TextWeight), sizes);
    }

    /// <summary>Puts the settings window back when arranging ends, however it ended.</summary>
    private void OnEditModeFinished() => m_configWindow.IsOpen = true;

    private void OnInfoBarPreferenceChanged()
    {
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
    }
}
