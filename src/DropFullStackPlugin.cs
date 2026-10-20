using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StonewardsDropFullStack
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DropFullStackPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "thereisnouser.stonewards.dropfullstack";
        public const string PluginName = "Stonewards Drop Full Stack";
        public const string PluginVersion = "0.1.2";

        private const float PendingTimeoutSeconds = 5f;
        private const int MaxDiagnosticMessages = 30;

        private sealed class PendingDrop
        {
            internal string ItemId;
            internal int Count;
            internal float ExpiresAt;
        }

        private static readonly Dictionary<long, PendingDrop> Pending =
            new Dictionary<long, PendingDrop>();

        private static ConfigEntry<bool> _enabled;
        private static ManualLogSource _log;
        private static int _clientCalls;
        private static int _serverCalls;
        private Harmony _harmony;
        private float _nextCleanup;

        private void Awake()
        {
            _log = Logger;
            _enabled = Config.Bind("General", "Enabled", true,
                "Drop the complete selected inventory stack with the normal drop button.");

            _harmony = new Harmony(PluginGuid);
            var command = AccessTools.Method(typeof(FirstPersonController), "CmdReleaseItem");
            if (!HasExpectedSignature(command))
            {
                Logger.LogError("[DropFullStack] CmdReleaseItem is missing or its arguments changed. Patch disabled.");
                return;
            }

            try
            {
                _harmony.Patch(
                    command,
                    prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(DropFullStackPlugin), nameof(BeforeRelease))));

                Logger.LogInfo("[DropFullStack] v" + PluginVersion +
                    " client CmdReleaseItem hook active. Enabled=" + _enabled.Value);

                // Observe the server's native handler without altering it.
                // This distinguishes a client-side mismatch from a server-side
                // restriction (the game may ignore bulk counts when spawning).
                var candidates = typeof(FirstPersonController).GetMethods(
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic);
                int hookedServerHandlers = 0;
                foreach (var candidate in candidates)
                {
                    if (!candidate.Name.StartsWith("UserCode_CmdReleaseItem", StringComparison.Ordinal) ||
                        !HasExpectedSignature(candidate))
                        continue;

                    _harmony.Patch(
                        candidate,
                        prefix: new HarmonyMethod(
                            AccessTools.Method(typeof(DropFullStackPlugin), nameof(ObserveServerRelease))));
                    hookedServerHandlers++;
                }
                Logger.LogInfo("[DropFullStack] Server release handlers instrumented: " +
                    hookedServerHandlers + " (diagnostics only).");
            }
            catch (Exception ex)
            {
                Logger.LogError("[DropFullStack] Failed to install patch: " + ex);
                _harmony.UnpatchSelf();
            }
        }

        private static bool HasExpectedSignature(MethodInfo method)
        {
            if (method == null)
                return false;
            var p = method.GetParameters();
            return p.Length == 5 &&
                p[0].ParameterType == typeof(FirstPersonController) &&
                p[1].ParameterType == typeof(string) &&
                p[2].ParameterType == typeof(int) &&
                p[3].ParameterType == typeof(int) &&
                p[4].ParameterType == typeof(Vector3);
        }

        // Harmony index aliases refer to the native command's parameters.
        // __0 = owner, __1 = item ID, __2 = selected inventory slot, __3 = requested amount.
        // Do not replace the command: only alter the amount sent by the game.
        private static bool BeforeRelease(
            FirstPersonController __instance,
            FirstPersonController __0,
            string __1,
            int __2,
            ref int __3)
        {
            int call = ++_clientCalls;
            try
            {
                if (_enabled == null || !_enabled.Value)
                {
                    Diagnostic(call, "disabled; vanilla drop preserved");
                    return true;
                }

                if (__instance == null)
                {
                    Diagnostic(call, "no command instance");
                    return true;
                }

                bool instanceLocal = __instance.isLocalPlayer;
                bool ownerLocal = __0 != null && __0.isLocalPlayer;
                if (!instanceLocal && !ownerLocal)
                {
                    Diagnostic(call, "not local: instanceLocal=false ownerLocal=false; requested=" + __3);
                    return true;
                }

                if (__3 <= 0 || string.IsNullOrEmpty(__1))
                {
                    Diagnostic(call, "empty/invalid request; count=" + __3 +
                        " item=" + (__1 ?? "<null>"));
                    return true;
                }

                FirstPersonController player = __0 != null ? __0 : __instance;
                var inventory = player.InventorySystem;
                if (inventory == null)
                {
                    Diagnostic(call, "inventory missing; item=" + __1 + " slot=" + __2);
                    return true;
                }
                if (__2 < 0 || __2 >= inventory.InventoryEntries.Count)
                {
                    Diagnostic(call, "invalid slot=" + __2 + " totalSlots=" +
                        inventory.InventoryEntries.Count + " item=" + __1);
                    return true;
                }

                var entry = inventory.InventoryEntries[__2];
                if (!string.Equals(entry.itemID, __1, StringComparison.Ordinal))
                {
                    Diagnostic(call, "slot item mismatch: requested=" + __1 +
                        " actual=" + (entry.itemID ?? "<null>") + " slot=" + __2);
                    return true;
                }

                int held = entry.count;
                int originalCount = __3;
                if (held <= 0 || originalCount >= held)
                {
                    Diagnostic(call, "no expansion needed: item=" + __1 +
                        " slot=" + __2 + " requested=" + originalCount + " held=" + held);
                    return true;
                }

                long key = ((long)(uint)player.GetInstanceID() << 32) | (uint)__2;
                float now = Time.unscaledTime;

                if (Pending.TryGetValue(key, out var pending))
                {
                    if (now < pending.ExpiresAt && pending.Count == held &&
                        string.Equals(pending.ItemId, __1, StringComparison.Ordinal))
                    {
                        Diagnostic(call, "duplicate suppressed: item=" + __1 +
                            " slot=" + __2 + " held=" + held);
                        return false;
                    }
                    Pending.Remove(key);
                }

                // The native command takes (owner, itemId, slot, count, position).
                // Previous builds reversed the two int arguments: when dropping
                // STONE from slot 5 with count=1, they inspected slot 1 (HORN).
                // Change ONLY the requested count, preserving the slot index.
                __3 = held;
                Pending[key] = new PendingDrop
                {
                    ItemId = __1,
                    Count = held,
                    ExpiresAt = now + PendingTimeoutSeconds
                };
                Diagnostic(call, "EXPANDED: item=" + __1 + " slot=" + __2 +
                    " count=" + originalCount + " -> " + held);
                return true;
            }
            catch (Exception ex)
            {
                Diagnostic(call, "ERROR: " + ex);
                return true; // On failure preserve the game's native action.
            }
        }

        // Instrumentation only. Running on a host, this reveals the count which
        // reached Mirror's server handler, without changing server behavior.
        private static void ObserveServerRelease(object[] __args)
        {
            int call = ++_serverCalls;
            if (call > MaxDiagnosticMessages)
                return;
            try
            {
                string item = __args != null && __args.Length > 1
                    ? __args[1] as string : null;
                object slot = __args != null && __args.Length > 2
                    ? __args[2] : null;
                object count = __args != null && __args.Length > 3
                    ? __args[3] : null;
                _log?.LogInfo("[DropFullStack] SERVER incoming release #" + call +
                    ": item=" + (item ?? "<null>") +
                    " count=" + (count?.ToString() ?? "<null>") +
                    " slot=" + (slot?.ToString() ?? "<null>"));
            }
            catch (Exception ex)
            {
                _log?.LogWarning("[DropFullStack] Server diagnostic failed: " + ex.Message);
            }
        }

        private static void Diagnostic(int call, string message)
        {
            if (call <= MaxDiagnosticMessages)
                _log?.LogInfo("[DropFullStack] CLIENT release #" + call + ": " + message);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCleanup)
                return;
            _nextCleanup = Time.unscaledTime + 10f;
            if (Pending.Count == 0)
                return;
            var expired = new List<long>();
            foreach (var item in Pending)
            {
                if (Time.unscaledTime >= item.Value.ExpiresAt)
                    expired.Add(item.Key);
            }
            foreach (var key in expired)
                Pending.Remove(key);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Pending.Clear();
        }
    }
}
