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
/// Two questions decide whether an action is redirected, and they are different questions.
/// <em>May</em> it go there is the game's own answer — the action is offered to the hovered
/// member and left alone unless the game says it could be used on them, so a damage action
/// goes to the selected enemy untouched without any table of ours to keep current.
/// <em>Should</em> it go there is the player's, one spell at a time (<see cref="MouseoverSet"/>).
/// </para>
/// <para>
/// 🔴 The second question used to be a single switch covering everything (Florian,
/// 2026-09-19: made it a list). "Can be used on them" is true of every heal, every shield and
/// every raise a job owns, and wanting one of those on the pointer is not wanting all of them
/// — a regen you place deliberately and a raise you had better aim at the right corpse are
/// exactly the two a blanket switch got wrong.
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
    /// 🔴 Only the id is kept, never the address.
    /// <para>
    /// The address used to be cached here beside it, to save walking the object table inside
    /// the call that uses an action. That walk is not what it costs. The detour does not run
    /// in the frame that wrote the address — it runs on a key press, which can land after the
    /// member has despawned (a zone change, a party dissolving, a body cleaned up). The game
    /// has freed that object by then, and handing the pointer to CanUseActionOnTarget is a
    /// read of freed memory: not a plugin fault the log catches, a crash of the game.
    /// </para>
    /// <para>
    /// So the address is resolved from the id at the moment it is used, where the object
    /// table is the authority on whether that member still exists. The walk happens once per
    /// action used, not once per frame, which is why it was never worth caching.
    /// </para>
    /// </summary>
    private static nint AddressOf(ulong gameObjectId) =>
        gameObjectId == 0ul ? 0 : Services.Objects.SearchById(gameObjectId)?.Address ?? 0;

    /// <summary>
    /// The action ids the player has asked to be redirected, for the job being played. A flat
    /// array rather than a set: it holds a handful of entries, it is read inside the game's
    /// own call to use an action, and walking eight numbers beats hashing one.
    /// </summary>
    private static uint[] s_allowed = Array.Empty<uint>();

    /// <summary>
    /// How many of <see cref="s_allowed"/> are in use. Written after the array is filled, so
    /// a read can never see a number larger than the entries behind it.
    /// </summary>
    private static int s_allowedCount;

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
    public static void PointAt(ulong gameObjectId) => s_target = gameObjectId;

    /// <summary>
    /// Takes the spells this job redirects and puts the hook in or out to match. Called on the
    /// framework tick — the same thread the game uses an action on, which is why the list can
    /// be refilled in place without anything having to be locked.
    /// <para>
    /// Refilled every tick rather than when something changes. A cache here would need to
    /// notice a spell being switched off, a row being removed and a change of job, and the
    /// work it would save is a walk over at most a dozen numbers.
    /// </para>
    /// </summary>
    public void Sync(System.Collections.Generic.List<MouseoverSpell> spells)
    {
        int wants = 0;

        for (int i = 0; i < spells.Count; i++)
        {
            if (spells[i].Enabled && spells[i].ActionId != 0)
            {
                wants++;
            }
        }

        if (wants > s_allowed.Length)
        {
            // Only ever on the way up, and in practice once: a list this size is set up and
            // then lived with.
            s_allowed = new uint[wants];
        }

        int written = 0;

        for (int i = 0; i < spells.Count; i++)
        {
            MouseoverSpell spell = spells[i];

            if (spell.Enabled && spell.ActionId != 0)
            {
                s_allowed[written++] = spell.ActionId;
            }
        }

        s_allowedCount = written;
        this.Hook(written > 0);
    }

    /// <summary>
    /// Puts the hook in or takes it out. The common case is a comparison of two booleans.
    /// </summary>
    private void Hook(bool wanted)
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
            PointAt(0ul);
        }
    }

    public void Dispose()
    {
        PointAt(0ul);
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

        // Nobody under the pointer on a frame of ours: then whoever the game's own interface
        // is pointing at — a row of its party list in Legacy mode, where we draw no frames
        // (Florian, 2026-09-25). Ours comes first, because while our frames are up they are
        // what the player is pointing at.
        if (over == 0)
        {
            over = NativeUi.UiMouseOverId();
        }

        // Only real actions. Items, mounts, general actions and the rest are either not aimed
        // at anybody or are aimed by something other than a target id.
        if (over == 0 || over == targetId || actionType != ActionType.Action || manager is null)
        {
            return targetId;
        }

        // The adjusted id, because that is the action a combo or a job gauge has turned the
        // pressed one into, and it is the adjusted one the game would have used.
        uint adjusted = manager->GetAdjustedActionId(actionId);

        // The player's own list, asked first: it is a walk over a handful of numbers, while
        // the question below reaches into the game. Both ids are offered, because the list
        // holds what somebody picked off a menu and the key may carry either — the pressed
        // action when nothing has replaced it, the adjusted one when something has.
        if (!Allowed(actionId, adjusted))
        {
            return targetId;
        }

        // Asked for now, not remembered from the frame that drew the frame — see AddressOf.
        // A member the table no longer has is a member who is gone, and the action goes where
        // the player's selected target is instead.
        nint address = AddressOf(over);

        if (address == 0)
        {
            return targetId;
        }

        // 🔴 The game's own answer to the other question: can this action be used on them. A
        // first attempt asked GetActionStatus with its recast and cast checks switched off,
        // and that turned out to answer a wider question than target validity — a damage
        // action came back usable on a party member and the game then refused it as an
        // invalid target (Florian, 2026-09-12). This asks about the target and nothing else.
        //
        // Still asked even though the player named the action. What they said is that this
        // spell belongs on the pointer, not that it belongs on whatever is under it — and a
        // heal aimed at a hostile would simply fail.
        return ActionManager.CanUseActionOnTarget(adjusted, (GameObject*)address)
            ? over
            : targetId;
    }

    /// <summary>Whether either id is one the player asked to be redirected.</summary>
    private static bool Allowed(uint pressed, uint adjusted)
    {
        uint[] allowed = s_allowed;
        int count = s_allowedCount;

        for (int i = 0; i < count && i < allowed.Length; i++)
        {
            if (allowed[i] == pressed || allowed[i] == adjusted)
            {
                return true;
            }
        }

        return false;
    }
}
