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

    /// <summary>The party, read exactly once per frame into a snapshot — never in a draw path.</summary>
    [PluginService] internal static IPartyList Party { get; private set; } = null!;

    /// <summary>Logged in, in PvP, which zone — the state of the session, not of the player.</summary>
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    /// <summary>
    /// Where the player themself comes from. NOT <c>IClientState</c> in this Dalamud — the
    /// local player moved to the object table, and the party list is empty when you are alone,
    /// so this is the only way to draw a frame for yourself.
    /// </summary>
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;

    /// <summary>What the player is doing right now, so nothing is drawn over a cutscene.</summary>
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    /// <summary>Asked one thing only: whether the player has hidden the game's interface.</summary>
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
    }
}
