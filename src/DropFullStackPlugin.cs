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
                "Drop the entire selected inventory stack with the normal Release key.");

            var command = AccessTools.Method(typeof(FirstPersonController), "CmdReleaseItem");
            if (!HasExpectedSignature(command))
            {
                Logger.LogError("CmdReleaseItem not found or its signature has changed. Mod disabled.");
                return;
            }

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    command,
                    prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(DropFullStackPlugin), nameof(BeforeRelease))));
                Logger.LogInfo("Drop Full Stack " + PluginVersion + " active (native Release action).");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to hook native release command: " + ex);
                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                    _harmony = null;
                }
            }
        }

        private static bool HasExpectedSignature(MethodInfo method)
        {
            if (method == null)
                return false;
            var args = method.GetParameters();
            return args.Length == 5 &&
                   args[0].ParameterType == typeof(FirstPersonController) &&
                   args[1].ParameterType == typeof(string) &&
                   args[2].ParameterType == typeof(int) &&
                   args[3].ParameterType == typeof(int) &&
                   args[4].ParameterType == typeof(Vector3);
        }

        // Native order: CmdReleaseItem(owner, itemId, selectedSlot, count, position).
        // The tested fix is important: __2 is the slot, and __3 is the quantity.
        // Keep server inventory validation and the original drop position intact.
        private static bool BeforeRelease(
            FirstPersonController __instance,
            FirstPersonController __0,
            string __1,
            int __2,
            ref int __3)
        {
            try
            {
                if (_enabled == null || !_enabled.Value ||
                    __instance == null ||
                    (!__instance.isLocalPlayer && (__0 == null || !__0.isLocalPlayer)) ||
                    __3 <= 0 || string.IsNullOrEmpty(__1))
                    return true;

                var player = __0 != null ? __0 : __instance;
                var inventory = player.InventorySystem;
                if (inventory == null || __2 < 0 || __2 >= inventory.InventoryEntries.Count)
                    return true;

                var entry = inventory.InventoryEntries[__2];
                if (!string.Equals(entry.itemID, __1, StringComparison.Ordinal))
                    return true;

                int held = entry.count;
                if (held <= 0 || __3 >= held)
                    return true;

                long key = ((long)(uint)player.GetInstanceID() << 32) | (uint)__2;
                float now = Time.unscaledTime;
                if (Pending.TryGetValue(key, out var previous))
                {
                    // Ignore only repeated requests while the client still has
                    // the same unacknowledged slot count from the previous drop.
                    if (now < previous.ExpiresAt && previous.Count == held &&
                        string.Equals(previous.ItemId, __1, StringComparison.Ordinal))
                        return false;
                    Pending.Remove(key);
                }

                __3 = held;
                Pending[key] = new PendingDrop
                {
                    ItemId = __1,
                    Count = held,
                    ExpiresAt = now + PendingTimeoutSeconds
                };
                return true;
            }
            catch (Exception)
            {
                // Let the original drop behavior run on any unforeseen mismatch.
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
                if (Time.unscaledTime >= item.Value.ExpiresAt)
                    expired.Add(item.Key);
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
