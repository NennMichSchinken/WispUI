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

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
    }
}
