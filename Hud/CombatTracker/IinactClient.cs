using System;
using System.Collections.Generic;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json.Linq;
using WispUI.Core;
using WispUI.Data;

namespace WispUI.Hud.CombatTracker;

/// <summary>
/// The line to IINACT: subscribes over Dalamud IPC, takes each "CombatData" push, and keeps
/// the fight in progress plus the ones already finished.
/// <para>
/// Carried over from HamMeter, which is our own published plugin — so this is a move, not a
/// reimplementation, and the rule against copying foreign plugins does not apply. The IPC
/// handshake is the part of that plugin worth the most and the part nobody should have to
/// work out twice.
/// </para>
/// <para>
/// 🔴 The subscription endpoint carries OUR name, not the old plugin's. Both can be installed
/// at the same time — the old one is only retired once this module ships (spec §5) — and two
/// plugins registering the same IPC name is a collision, not a shared channel.
/// </para>
/// <para>
/// ⚠️ IINACT pushes on a background thread while the renderer reads on the frame thread, so
/// every field below is touched under the lock. Nothing here is called from a draw path:
/// the module takes one reading per frame in its collect step, the way the party snapshot
/// does (CLAUDE.md §7.2).
/// </para>
/// </summary>
internal sealed class IinactClient : IDisposable
{
    private const string SubscriptionEndpoint = "WispUI.SubscriptionReceiver";
    private const string ListeningEndpoint = "IINACT.Server.Listening";
    private const string SubscribeEndpoint = "IINACT.CreateSubscriber";
    private const string UnsubscribeEndpoint = "IINACT.Unsubscribe";
    private const string ProviderEditEndpoint = "IINACT.IpcProvider." + SubscriptionEndpoint;
    private const string SubscriptionMessage = "{\"call\":\"subscribe\",\"events\":[\"CombatData\"]}";
    internal const int MaxHistory = 50;

    private readonly ICallGateProvider<JObject, bool> m_receiver;

    private CombatEvent? m_lastCommitted;
    private bool m_suppressing;
    private float m_suppressRefDamage;
    private CombatEvent? m_combined;
    private int m_combinedCount = -1;

    // IINACT pushes data on a background thread while the UI reads it on the render
    // thread, so every access to the fields below must go through this lock.
    private readonly object m_sync = new();
    private CombatEvent? m_current;
    private readonly List<CombatEvent> m_past = new();

    public bool Connected { get; private set; }

    public CombatEvent? Current
    {
        get
        {
            lock (m_sync)
            {
                return m_current;
            }
        }
    }

    public int PastCount
    {
        get
        {
            lock (m_sync)
            {
                return m_past.Count;
            }
        }
    }

    public CombatEvent? GetPast(int index)
    {
        lock (m_sync)
        {
            return index >= 0 && index < m_past.Count ? m_past[index] : null;
        }
    }

    public List<CombatEvent> SnapshotPast()
    {
        lock (m_sync)
        {
            return new List<CombatEvent>(m_past);
        }
    }

    public IinactClient()
    {
        m_receiver = Services.PluginInterface.GetIpcProvider<JObject, bool>(SubscriptionEndpoint);
        m_receiver.RegisterFunc(this.ReceiveMessage);
    }

    public void Connect()
    {
        if (this.Connected)
        {
            return;
        }

        try
        {
            bool listening = Services.PluginInterface.GetIpcSubscriber<bool>(ListeningEndpoint).InvokeFunc();
            if (!listening)
            {
                return;
            }

            Services.PluginInterface.GetIpcSubscriber<string, bool>(SubscribeEndpoint).InvokeFunc(SubscriptionEndpoint);
            Services.PluginInterface
                .GetIpcSubscriber<JObject, bool>(ProviderEditEndpoint)
                .InvokeAction(JObject.Parse(SubscriptionMessage));

            this.Connected = true;
            Services.Log.Information("Combat tracker: connected to IINACT.");
        }
        catch (Exception)
        {
            this.Connected = false;
        }
    }

    private bool ReceiveMessage(JObject data)
    {
        try
        {
            CombatEvent? ev = data.ToObject<CombatEvent>();
            if (ev?.Encounter is null || ev.Combatants is null || ev.Combatants.Count == 0)
            {
                return true;
            }

            // All shared-state access happens under the lock (the UI reads these
            // same fields on the render thread).
            lock (m_sync)
            {
                if (m_suppressing)
                {
                    float dmg = Num.Parse(ev.Encounter.DamageRaw);
                    if (dmg + 1f < m_suppressRefDamage)
                    {
                        m_suppressing = false;
                    }
                    else
                    {
                        if (dmg > m_suppressRefDamage)
                        {
                            m_suppressRefDamage = dmg;
                        }

                        return true;
                    }
                }

                // Commit a finished encounter to history once (de-duped against the last one).
                if (!ev.Active && !ev.SameAs(m_lastCommitted))
                {
                    m_past.Add(ev);
                    m_lastCommitted = ev;
                    m_combined = null;

                    while (m_past.Count > MaxHistory)
                    {
                        m_past.RemoveAt(0);
                    }
                }

                m_current = ev;
            }

            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Combat tracker: could not read a combat push.");
            return false;
        }
    }

    public CombatEvent? GetOverall()
    {
        lock (m_sync)
        {
            if (m_past.Count == 0)
            {
                return m_current;
            }

            if (m_combined is null || m_combinedCount != m_past.Count)
            {
                m_combined = CombatEvent.BuildOverall(m_past);
                m_combinedCount = m_past.Count;
            }

            return m_combined;
        }
    }

    public void ClearData()
    {
        lock (m_sync)
        {
            // Start (or refresh) suppression based on what's currently showing. If nothing
            // is showing (e.g. a second reset while already cleared), keep the existing
            // suppression instead of turning it off.
            if (m_current?.Encounter is not null)
            {
                float refDamage = Num.Parse(m_current.Encounter.DamageRaw);
                if (refDamage > 0f)
                {
                    m_suppressing = true;
                    m_suppressRefDamage = refDamage;
                }
            }

            m_current = null;
            m_past.Clear();
            m_lastCommitted = null;
            m_combined = null;
            m_combinedCount = -1;
        }
    }

    public void Dispose()
    {
        try
        {
            Services.PluginInterface.GetIpcSubscriber<string, bool>(UnsubscribeEndpoint).InvokeFunc(SubscriptionEndpoint);
        }
        catch (Exception)
        {
            // ignore on shutdown
        }
    }
}
