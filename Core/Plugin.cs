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

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Services.Initialize(pluginInterface);

        m_config = Configuration.Load();

        // Before anything asks for a face by name. Reading the folder touches the disk, so it
        // happens once here and again only when the player asks for it.
        FontLibrary.Refresh();

        Scaling.Commit(m_config.Scale);
        Scaling.LogGameScaleReadings();

        m_configWindow = new ConfigWindow(m_config);
        m_windows.AddWindow(m_configWindow);
        m_commands = new CommandHandler(m_configWindow);

        m_infoBar = new InfoBarEntry(m_configWindow);
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
        m_configWindow.InfoBarPreferenceChanged += this.OnInfoBarPreferenceChanged;

        m_hud.Add(new PartyFramesElement(m_config));

        // Made now, put in place only if the player has asked for it. The hook it owns is the
        // suite's one reach into what a key press does, so it is never installed on spec.
        m_mouseover = new MouseoverCasting();
        m_mouseover.Sync(m_config.PartyFrames.MouseoverCasting);

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
        m_configWindow.InfoBarPreferenceChanged -= this.OnInfoBarPreferenceChanged;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenMainUi -= m_configWindow.Toggle;
        Services.PluginInterface.UiBuilder.Draw -= this.OnDraw;

        m_mouseover.Dispose();
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

        // Two booleans compared. The hook goes in and comes out with the setting rather than
        // sitting installed and inert, so a player who never turns it on never carries it.
        m_mouseover.Sync(m_config.PartyFrames.MouseoverCasting);

        this.SyncHudFonts();
    }

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

        // On the stack, so the tick allocates nothing. These are the three texts a frame can
        // carry; two of them are usually the same size, and SyncHud drops the duplicate.
        Span<float> sizes = stackalloc float[3];
        sizes[0] = Tokens.Px(cfg.NameSize);
        sizes[1] = Tokens.Px(cfg.HpTextSize);
        sizes[2] = Tokens.Px(cfg.PartyNumberSize);

        Fonts.SyncHud(!m_config.HasPendingChanges, cfg.FontName, HudText.WeightAt(cfg.TextWeight), sizes);
    }

    private void OnInfoBarPreferenceChanged()
    {
        m_infoBar.Apply(m_config.ShowInfoBarEntry);
    }
}
