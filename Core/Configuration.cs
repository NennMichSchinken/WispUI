using System;
using Dalamud.Configuration;

namespace WispUI.Core;

/// <summary>
/// The saved state of the whole suite. The <see cref="Version"/> field and the migration
/// chain exist from the first commit on, so a later format change never needs a reset.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Bump this whenever the stored shape changes, and add a step to <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 1;

    /// <summary>How long the configuration may sit unsaved before it is written to disk.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

    private DateTime m_dirtySince = DateTime.MaxValue;

    public int Version { get; set; } = CurrentVersion;

    // --- Global -------------------------------------------------------------
    /// <summary>Smallest and largest interface scale the slider offers.</summary>
    public const float MinScale = 0.75f;
    public const float MaxScale = 1.75f;

    /// <summary>The interface scale. The window has a fixed size, and this is what sets it.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Show an entry in the server info bar that opens the settings window.</summary>
    public bool ShowInfoBarEntry { get; set; } = true;

    // --- Modules ------------------------------------------------------------
    public bool PartyFramesEnabled { get; set; } = true;

    internal static Configuration Load()
    {
        Configuration config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (config.Version < CurrentVersion)
        {
            Migrate(config);
            config.Version = CurrentVersion;
            config.Write();
        }

        return config;
    }

    /// <summary>
    /// Marks the configuration as changed. The write itself is debounced, so dragging a
    /// slider or clicking through an arrow selector never touches the disk per frame.
    /// </summary>
    internal void MarkDirty()
    {
        if (m_dirtySince == DateTime.MaxValue)
        {
            m_dirtySince = DateTime.UtcNow;
        }
    }

    /// <summary>Called once per framework tick. Writes only after the changes have gone quiet.</summary>
    internal void Tick()
    {
        if (m_dirtySince != DateTime.MaxValue && DateTime.UtcNow - m_dirtySince >= SaveDelay)
        {
            this.Write();
        }
    }

    /// <summary>Writes a pending change right now, ignoring the debounce. Used on unload.</summary>
    internal void FlushPending()
    {
        if (m_dirtySince != DateTime.MaxValue)
        {
            this.Write();
        }
    }

    /// <summary>Writes immediately, whether or not the debounce has elapsed.</summary>
    internal void Write()
    {
        m_dirtySince = DateTime.MaxValue;
        Services.PluginInterface.SavePluginConfig(this);
    }

    private static void Migrate(Configuration config)
    {
        // Version 1 is the first shipped format, so there is nothing to convert yet.
        // Future steps go here one at a time and in order, for example:
        //   if (config.Version < 2) { ...; }
        _ = config;
    }
}
