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

    /// <summary>
    /// Game icons. Dalamud keeps the textures itself, so nothing here loads or creates one —
    /// an icon is asked for by id, once, and the handle is kept.
    /// </summary>
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    /// <summary>
    /// The game's own data sheets. Read at load and cached — never from a draw path, where a
    /// sheet lookup is one of the things the performance rules forbid outright (§7.3).
    /// </summary>
    [PluginService] internal static IDataManager Data { get; private set; } = null!;

    /// <summary>
    /// Who the player has selected, and what the game thinks the mouse is over. Both are
    /// settable, which is what lets a frame of ours behave like the game's own party list —
    /// and it means no writing into game memory for either (verified 2026-09-12).
    /// </summary>
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;

    /// <summary>
    /// Hooking. Used by exactly one feature — sending an action to whoever the mouse is over —
    /// and by nothing else. Everything in the suite reads the game and draws; that one thing
    /// reaches into what a key press does, which is why it is named here and kept to itself.
    /// </summary>
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;

    /// <summary>
    /// The chat log — written to for exactly one thing: the echo line IINACT reads as "close
    /// this fight". Never used to tell the player anything (CLAUDE.md §5.1a: never nag).
    /// </summary>
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
    }
}
