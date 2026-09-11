using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace WispUI.Core;

/// <summary>
/// Sends an action to whoever the mouse is over instead of to the selected target.
/// <para>
/// 🔴 This is the one thing WispUI does that changes what a key press <em>does</em>, rather
/// than what the screen shows. Everything else in the suite reads the game and draws; this
/// reaches into the middle of using an action. It therefore lives on its own, is off until
/// the player asks for it, and is unhooked the moment they take it back.
/// </para>
/// <para>
/// Why it has to exist at all: the game has no setting for it. Writing the mouseover target
/// is enough for <c>&lt;mo&gt;</c> macros, and that is all it is enough for — a hotbar key
/// still goes to the selected target, which the first build of step 6 proved in game
/// (Florian, 2026-09-12). Every plugin that offers this redirects the action.
/// </para>
/// <para>
/// The rule for <em>whether</em> to redirect is the game's own answer, not a list of ours:
/// the action is offered to the hovered member and only redirected if the game says it could
/// be used on them. A heal finds the party member under the cursor; a damage action does not,
/// and goes to the enemy that is selected, untouched. No table of friendly and hostile
/// actions to keep current, and nothing to get wrong when a job changes.
/// </para>
/// </summary>
internal sealed unsafe class MouseoverCasting : IDisposable
{
    /// <summary>
    /// Who the mouse is over, as the game counts objects, or zero for nobody. Written by the
    /// element that owns the frames, once per frame, and read by the detour in between.
    /// </summary>
    private static ulong s_target;

    /// <summary>
    /// The same member as a pointer, which is what the game's own "can this be used on them"
    /// wants. Kept beside the id rather than looked up again: finding an object walks the
    /// table, and this runs inside the call that uses an action.
    /// </summary>
    private static nint s_targetAddress;

    private readonly Hook<UseActionDelegate>? m_hook;

    private bool m_wanted;

    public MouseoverCasting()
    {
        try
        {
            m_hook = Services.Interop.HookFromAddress<UseActionDelegate>(
                (nint)ActionManager.MemberFunctionPointers.UseAction,
                this.UseActionDetour);
        }
        catch (Exception ex)
        {
            // A signature the current patch has moved is a reason for this one feature to be
            // unavailable, not for the plugin to fail to load. Everything else carries on.
            Services.Log.Error(ex, "Mouseover casting is unavailable: the action call could not be found.");
            m_hook = null;
        }
    }

    private delegate bool UseActionDelegate(
        ActionManager* manager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted);

    /// <summary>Whether the hook could be made at all. False means a patch moved the function.</summary>
    public bool Available => m_hook is not null;

    /// <summary>
    /// Says who is under the mouse, or nothing. Called every frame by whoever owns the frames
    /// — including the frames where nobody is hovered, because a stale answer here would send
    /// an action to somebody the player stopped pointing at.
    /// </summary>
    public static void PointAt(ulong gameObjectId, nint address)
    {
        s_target = gameObjectId;
        s_targetAddress = address;
    }

    /// <summary>
    /// Puts the hook in or takes it out to match the setting. Called on the framework tick, so
    /// the common case is a comparison of two booleans.
    /// </summary>
    public void Sync(bool wanted)
    {
        if (m_hook is null || wanted == m_wanted)
        {
            return;
        }

        m_wanted = wanted;

        if (wanted)
        {
            m_hook.Enable();
        }
        else
        {
            m_hook.Disable();
            PointAt(0ul, 0);
        }
    }

    public void Dispose()
    {
        PointAt(0ul, 0);
        m_hook?.Dispose();
    }

    private bool UseActionDetour(
        ActionManager* manager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        // Wrapped whole. A throw in here happens inside the game's own call to use an action,
        // and there is no frame to recover on: whatever goes wrong, the action still has to
        // be used the way the player asked for it.
        try
        {
            targetId = Redirect(manager, actionType, actionId, targetId);
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Mouseover casting left the action alone.");
        }

        return m_hook!.Original(manager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    private static ulong Redirect(ActionManager* manager, ActionType actionType, uint actionId, ulong targetId)
    {
        ulong over = s_target;
        nint address = s_targetAddress;

        // Only real actions. Items, mounts, general actions and the rest are either not aimed
        // at anybody or are aimed by something other than a target id.
        if (over == 0 || address == 0 || over == targetId || actionType != ActionType.Action || manager is null)
        {
            return targetId;
        }

        // 🔴 The game's own answer to the one question that matters: can this action be used
        // on them. A first attempt asked GetActionStatus with its recast and cast checks
        // switched off, and that turned out to answer a wider question than target validity —
        // a damage action came back usable on a party member and the game then refused it as
        // an invalid target (Florian, 2026-09-12). This asks about the target and nothing else.
        //
        // The adjusted id, because that is the action a combo or a job gauge has turned the
        // pressed one into, and it is the adjusted one the game would have used.
        return ActionManager.CanUseActionOnTarget(manager->GetAdjustedActionId(actionId), (GameObject*)address)
            ? over
            : targetId;
    }
}
