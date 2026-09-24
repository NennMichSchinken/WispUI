using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace WispUI.Core;

/// <summary>
/// Uses an action on somebody, for a binding that asked for one.
/// <para>
/// 🔴 This and <see cref="MouseoverCasting"/> are the only two places in the suite that change
/// what happens rather than what is shown. The difference between them is worth keeping
/// straight: mouseover casting <em>redirects</em> an action the player pressed, while this
/// <em>starts</em> one they asked for with a mouse button. Nothing here hooks anything — it
/// calls the same function the game calls when a hotbar slot is pressed.
/// </para>
/// <para>
/// The rule for whether to go ahead is the game's own answer, not a table of ours: the action
/// is offered to the member and only used if the game says it could be. So a heal bound to a
/// side button does nothing on a frame it cannot help, rather than throwing an error at the
/// player — and there is no list of friendly and hostile actions to keep current.
/// </para>
/// </summary>
internal static unsafe class ActionUse
{
    /// <summary>
    /// Uses an action on a party member. Does nothing, quietly, when the game says it cannot
    /// be used on them.
    /// </summary>
    /// <param name="actionId">The Action row id the binding holds.</param>
    /// <param name="targetId">The member, as the game counts objects.</param>
    /// <param name="targetAddress">The same member as a pointer, for the game's own check.</param>
    public static void On(uint actionId, ulong targetId, nint targetAddress)
    {
        if (actionId == 0 || targetId == 0 || targetAddress == 0)
        {
            return;
        }

        try
        {
            ActionManager* manager = ActionManager.Instance();

            if (manager is null)
            {
                return;
            }

            // The adjusted id, because that is what a combo or a job gauge has turned the
            // action into, and it is what the game would have used from a hotbar.
            uint adjusted = manager->GetAdjustedActionId(actionId);

            if (!ActionManager.CanUseActionOnTarget(adjusted, (GameObject*)targetAddress))
            {
                return;
            }

            manager->UseAction(ActionType.Action, adjusted, targetId);
        }
        catch (Exception ex)
        {
            // A binding that cannot fire is a binding that does nothing. It is never a reason
            // to take the frame down with it.
            Services.Log.Error(ex, "A bound action could not be used: {Action}.", actionId);
        }
    }
}
