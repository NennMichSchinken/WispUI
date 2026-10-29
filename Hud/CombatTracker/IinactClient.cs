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
    private const string SubscriptionMessage = "{\"call\":\"subscribe\",\"events\":[\"CombatData\",\"LogLine\"]}";
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
            // Every network log line arrives here too, many per second in a pull, so it is
            // told apart before anything is deserialised.
            if (data.TryGetValue("type", out JToken? kind) && (string?)kind == "LogLine")
            {
                if (s_dumpLinesUntil != 0)
                {
                    SampleLogLine(data);
                }

                return true;
            }

            if (s_dumpCombat)
            {
                s_dumpCombat = false;
                DumpCombatKeys(data);
            }

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

    /// <summary>
    /// Drops the line to IINACT after it went away, so the next start is subscribed to afresh
    /// rather than trusted to still be listening.
    /// </summary>
    public void Forget() => this.Connected = false;

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

    // ---- Diagnostic: "/wisp status" ------------------------------------------------------
    //
    // Asks the running IINACT what it actually sends, before the meter details are built on
    // it: the keys of one real CombatData push (are DirectHit / CritDirectHit / Parry / Block
    // there?) and a sample of the log lines the per-skill numbers will come from. Static,
    // because the command handler has no line to the meter, and a diagnostic is not worth
    // wiring one.

    private const int DumpSeconds = 40;

    // Line types worth seeing, and how many of each: 01 zone change, 03 add combatant (job,
    // owner of a pet), 04 remove combatant, 21/22 ability (single / multi target), 24 DoT/HoT
    // tick, 33 director (fight start / victory / wipe), 37 effect result (HP after a hit).
    // First run (2026-10-29, Dancer on a dummy) settled 21 and 37; 03 and 24 never came.
    private static readonly string[] DumpTypes = { "01", "03", "04", "21", "22", "24", "33", "37" };
    private static readonly int[] DumpBudget = { 3, 12, 4, 14, 6, 14, 6, 0 };

    private static volatile bool s_dumpCombat;
    private static long s_dumpLinesUntil;
    private static readonly int[] s_dumpLeft = new int[8];
    private static readonly Dictionary<string, int> s_dumpSeen = new();

    /// <summary>Starts a dump: the next CombatData push and <see cref="DumpSeconds"/> of log lines.</summary>
    public static void RequestDump()
    {
        lock (s_dumpSeen)
        {
            s_dumpSeen.Clear();
            Array.Copy(DumpBudget, s_dumpLeft, DumpBudget.Length);
        }

        s_dumpCombat = true;
        System.Threading.Interlocked.Exchange(ref s_dumpLinesUntil, Environment.TickCount64 + (DumpSeconds * 1000));
        Services.Log.Information(
            "Combat tracker dump: armed for {Seconds} s. Nothing below means IINACT sent nothing (not connected, or no fight).",
            DumpSeconds);
    }

    private static void DumpCombatKeys(JObject data)
    {
        if (data["Encounter"] is JObject encounter)
        {
            Services.Log.Information("Combat tracker dump: Encounter has {Count} keys.", encounter.Count);
            foreach (JProperty p in encounter.Properties())
            {
                Services.Log.Information("  Encounter.{Key} = {Value}", p.Name, p.Value.ToString());
            }
        }

        if (data["Combatant"] is not JObject combatants)
        {
            Services.Log.Information("Combat tracker dump: no Combatant block.");
            return;
        }

        int shown = 0;
        foreach (JProperty c in combatants.Properties())
        {
            if (c.Value is not JObject fields)
            {
                continue;
            }

            Services.Log.Information("Combat tracker dump: Combatant \"{Name}\" has {Count} keys.", c.Name, fields.Count);
            foreach (JProperty p in fields.Properties())
            {
                Services.Log.Information("  {Name}.{Key} = {Value}", c.Name, p.Name, p.Value.ToString());
            }

            if (++shown >= 2)
            {
                break;
            }
        }
    }

    private static void SampleLogLine(JObject data)
    {
        if (Environment.TickCount64 > System.Threading.Interlocked.Read(ref s_dumpLinesUntil))
        {
            System.Threading.Interlocked.Exchange(ref s_dumpLinesUntil, 0);
            lock (s_dumpSeen)
            {
                foreach (KeyValuePair<string, int> seen in s_dumpSeen)
                {
                    Services.Log.Information("Combat tracker dump: line type {Type} seen {Count}x.", seen.Key, seen.Value);
                }
            }

            Services.Log.Information("Combat tracker dump: done.");
            return;
        }

        string type = data["line"] is JArray line && line.Count > 0 ? (string?)line[0] ?? "?" : "?";
        bool show = false;

        lock (s_dumpSeen)
        {
            s_dumpSeen[type] = s_dumpSeen.TryGetValue(type, out int n) ? n + 1 : 1;

            int slot = Array.IndexOf(DumpTypes, type);
            if (slot >= 0 && s_dumpLeft[slot] > 0)
            {
                s_dumpLeft[slot]--;
                show = true;
            }
        }

        if (show)
        {
            Services.Log.Information("Combat tracker dump: line {Type}: {Raw}", type, (string?)data["rawLine"] ?? data.ToString(Newtonsoft.Json.Formatting.None));
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
