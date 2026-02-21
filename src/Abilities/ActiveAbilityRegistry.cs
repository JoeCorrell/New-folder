using System;
using System.Collections.Generic;

namespace StartingClassMod
{
    /// <summary>
    /// Central registry for all active (GP-triggered) abilities.
    /// Abilities register once at startup; all routing, cleanup, HUD, and
    /// icon loading is handled automatically via the registry.
    /// </summary>
    public static class ActiveAbilityRegistry
    {
        public class Entry
        {
            public string PowerId;
            public string ClassName;
            public int AbilityIndex;
            public string DisplayName;

            // Core callbacks
            public Func<Player, bool> TryActivate;
            public Action<Player> ForceDeactivate;
            public Action<Player> RestoreIfActive;
            public Action Update;
            public Action OnLogout;

            // HUD state queries
            public Func<Player, bool> IsActive;
            public Func<Player, float> GetDurationRemaining;
            public Func<Player, float> GetCooldownRemaining;
            public Func<Player, string> GetExtraHudText; // optional
        }

        private static readonly Dictionary<string, Entry> _registry = new Dictionary<string, Entry>();

        public static void Register(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.PowerId)) return;
            _registry[entry.PowerId] = entry;
        }

        public static Entry Get(string powerId)
        {
            if (string.IsNullOrEmpty(powerId)) return null;
            _registry.TryGetValue(powerId, out Entry entry);
            return entry;
        }

        public static List<Entry> GetForClass(string className)
        {
            var result = new List<Entry>();
            foreach (var entry in _registry.Values)
            {
                if (entry.ClassName == className)
                    result.Add(entry);
            }
            return result;
        }

        public static string GetPowerIdForAbility(string className, int abilityIndex)
        {
            foreach (var entry in _registry.Values)
            {
                if (entry.ClassName == className && entry.AbilityIndex == abilityIndex)
                    return entry.PowerId;
            }
            return null;
        }

        public static string[] GetIconNames(string className)
        {
            var entries = GetForClass(className);
            var names = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                names[i] = entries[i].PowerId;
            return names;
        }

        public static void ForceDeactivateAll(Player player)
        {
            foreach (var entry in _registry.Values)
                entry.ForceDeactivate?.Invoke(player);
        }

        public static void UpdateAll()
        {
            foreach (var entry in _registry.Values)
                entry.Update?.Invoke();
        }

        public static void LogoutAll()
        {
            foreach (var entry in _registry.Values)
                entry.OnLogout?.Invoke();
        }
    }
}
