using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace WispUI.Core;

/// <summary>
/// Every Dalamud service WispUI uses, in one place. Nothing else asks Dalamud for a
/// service: when a new one is needed it is added here, so the list of things we touch
/// stays readable at a glance.
/// </summary>
internal sealed class Services
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;

    /// <summary>
    /// Read and written: the game keeps its own key buffer, and a key we act on has to be
    /// taken out of it, or the game acts on it as well. See the escape handling in the
    /// configuration window.
    /// </summary>
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
    }
}
