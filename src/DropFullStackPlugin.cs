using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace StonewardsDropFullStack
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DropFullStackPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "thereisnouser.stonewards.dropfullstack";
        public const string PluginName = "Stonewards Drop Full Stack";
        public const string PluginVersion = "0.1.0";

        // A client may still display the old inventory count while the server processes
        // the first request. Refuse repeat bulk drops of the same unchanged slot.
        // A time limit prevents a stuck lock if a server never acknowledges the drop.
        private const float PendingTimeoutSeconds = 5f;

        private sealed class PendingDrop
        {
            internal string ItemId;
            internal int Count;
            internal float ExpiresAt;
        }

        private static readonly Dictionary<long, PendingDrop> Pending =
            new Dictionary<long, PendingDrop>();

        private static ConfigEntry<bool> _enabled;
        private Harmony _harmony;
        private float _nextCleanup;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Replace the normal one-item drop with a drop of the entire selected inventory slot.");

            MethodInfo target = AccessTools.Method(typeof(FirstPersonController), "CmdReleaseItem");
            if (target == null)
            {
                Logger.LogError("Drop Full Stack disabled: FirstPersonController.CmdReleaseItem not found.");
                return;
            }

            // Verified against known Stonewards mod usage. If an update changes
            // this command, do not blindly patch the wrong arguments.
            ParameterInfo[] p = target.GetParameters();
            if (p.Length != 5 ||
                p[0].ParameterType != typeof(FirstPersonController) ||
                p[1].ParameterType != typeof(string) ||
                p[2].ParameterType != typeof(int) ||
                p[3].ParameterType != typeof(int) ||
                p[4].ParameterType != typeof(Vector3))
            {
                Logger.LogError("Drop Full Stack disabled: CmdReleaseItem signature changed.");
                return;
            }

            try
            {
                _harmony = new Harmony(PluginGuid);
                MethodInfo prefix = AccessTools.Method(typeof(DropFullStackPlugin), nameof(BeforeRelease));
                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Logger.LogInfo("Drop Full Stack v" + PluginVersion + " active. Normal drop now releases a complete slot.");
            }
            catch (Exception error)
            {
                Logger.LogError("Drop Full Stack patch failed: " + error);
                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                    _harmony = null;
                }
            }
        }

        // Harmony positional argument names avoid dependency on parameter names
        // in the game's DLL:
        // __0 = player, __1 = itemId, __2 = count, __3 = selected slot,
        // __4 = drop position (unchanged).
        // Patch the CLIENT command wrapper, not the server UserCode or an inventory
        // removal function. The game remains authoritative over actual removal.
        private static bool BeforeRelease(
            FirstPersonController __instance,
            FirstPersonController __0,
            string __1,
            ref int __2,
            int __3)
        {
            try
            {
                if (_enabled == null || !_enabled.Value ||
                    __instance == null || !__instance.isLocalPlayer ||
                    !ReferenceEquals(__0, __instance) ||
                    __2 != 1 || string.IsNullOrEmpty(__1))
                    return true;

                var inventory = __instance.InventorySystem;
                if (inventory == null ||
                    __3 < 0 || __3 >= inventory.InventoryEntries.Count)
                    return true;

                var entry = inventory.InventoryEntries[__3];
                if (!string.Equals(entry.itemID, __1, StringComparison.Ordinal) ||
                    entry.count <= 0)
                    return true;

                long key = ((long)(uint)__instance.GetInstanceID() << 32) | (uint)__3;
                int count = entry.count;
                float now = Time.unscaledTime;

                if (Pending.TryGetValue(key, out var pending))
                {
                    if (now < pending.ExpiresAt &&
                        pending.Count == count &&
                        string.Equals(pending.ItemId, __1, StringComparison.Ordinal))
                    {
                        // No server inventory change yet. Avoid issuing a second
                        // identical command using the still-stale client stack.
                        return false;
                    }
                    Pending.Remove(key);
                }

                if (count <= 1)
                    return true; // Single items behave exactly as in vanilla.

                // This is the only gameplay change: the existing Mirror command
                // sends a server-validated drop request for the complete slot.
                __2 = count;
                Pending[key] = new PendingDrop
                {
                    ItemId = __1,
                    Count = count,
                    ExpiresAt = now + PendingTimeoutSeconds
                };
                return true;
            }
            catch (Exception)
            {
                // A mod must not break the vanilla drop action if a game update
                // changes the inventory data model unexpectedly.
                return true;
            }
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
            if (_harmony != null)
                _harmony.UnpatchSelf();
            Pending.Clear();
        }
    }
}
