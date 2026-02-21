using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Assassin active ability: Blade Dance.
    /// When activated, all dagger/knife attacks deal double damage for 1 minute.
    /// 10 minute cooldown. Activated via GP button when selected as active power.
    /// Damage doubling is handled in AssassinPatches.Character_RPC_Damage_Patch.
    /// </summary>
    public static class BladeDance
    {
        private const float Duration = 60f; // 1 minute
        private const float Cooldown = 600f; // 10 minutes
        private const string CooldownKey = "StartingClassMod_BladeDance_CD";
        private const string DurationKey = "StartingClassMod_BladeDance_End";


        /// <summary>Whether the damage buff is currently active.</summary>
        public static bool IsActive()
        {
            var player = Player.m_localPlayer;
            if (player == null) return false;
            return GetTimeRemaining(player) > 0f;
        }

        /// <summary>Seconds remaining on the buff, or 0 if inactive.</summary>
        public static float GetTimeRemaining() => GetTimeRemaining(Player.m_localPlayer);

        /// <summary>Seconds remaining on the buff for a specific player, or 0 if inactive.</summary>
        public static float GetTimeRemaining(Player player)
        {
            if (player == null) return 0f;
            if (!player.m_customData.TryGetValue(DurationKey, out string val)) return 0f;
            if (!double.TryParse(val, out double endTime)) return 0f;
            if (ZNet.instance == null) return 0f;
            double now = ZNet.instance.GetTimeSeconds();
            float remaining = (float)(endTime - now);
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>Seconds remaining on cooldown, or 0 if ready.</summary>
        public static float GetCooldownRemaining(Player player)
        {
            if (player == null) return 0f;
            // No cooldown while the ability is still active
            if (GetTimeRemaining() > 0f) return 0f;
            if (!player.m_customData.TryGetValue(CooldownKey, out string val)) return 0f;
            if (!double.TryParse(val, out double cdEnd)) return 0f;
            if (ZNet.instance == null) return 0f;
            double remaining = cdEnd - ZNet.instance.GetTimeSeconds();
            return remaining > 0 ? (float)remaining : 0f;
        }

        /// <summary>Restore HUD status effect if the ability is still active (e.g. after login).</summary>
        public static void RestoreIfActive(Player player)
        {
            if (player == null || !IsActive()) return;
            AddHudStatusEffect(player);
        }

        /// <summary>Try to activate Blade Dance. Called from GP intercept via registry.</summary>
        public static bool TryActivate(Player player)
        {
            if (player == null) return false;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Assassin") return false;
            if (!AbilityManager.IsAbilityUnlocked(player, "Assassin", 5)) return false;

            if (IsActive()) return false;

            if (GetCooldownRemaining(player) > 0f)
            {
                player.Message(MessageHud.MessageType.Center, "Blade Dance is not ready");
                return false;
            }

            if (ZNet.instance == null) return false;
            double now = ZNet.instance.GetTimeSeconds();
            player.m_customData[DurationKey] = (now + Duration).ToString("F0");
            // Cooldown starts after duration ends
            player.m_customData[CooldownKey] = (now + Duration + Cooldown).ToString("F0");

            AddHudStatusEffect(player);
            StartingClassPlugin.Log("Blade Dance activated.");
            return true;
        }

        /// <summary>Called each frame from plugin Update to handle expiry.</summary>
        public static void UpdateBladeDance()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            if (!IsActive() && player.m_customData.ContainsKey(DurationKey))
            {
                player.m_customData.Remove(DurationKey);
                player.Message(MessageHud.MessageType.Center, "Blade Dance expired");
            }
        }

        /// <summary>
        /// Full deactivation: removes m_customData keys so the buff immediately stops.
        /// Use on class switch/reset. On logout, call Reset() instead (keeps cooldown persisted).
        /// </summary>
        public static void ForceDeactivate(Player player)
        {
            if (player != null)
            {
                player.m_customData.Remove(DurationKey);
                player.m_customData.Remove(CooldownKey);
            }
        }

        /// <summary>Called on logout — state lives in m_customData, nothing to clean up.</summary>
        public static void Reset() { }

        public static void Register()
        {
            ActiveAbilityRegistry.Register(new ActiveAbilityRegistry.Entry
            {
                PowerId = "BladeDance",
                ClassName = "Assassin",
                AbilityIndex = 5,
                DisplayName = "Blade Dance",
                TryActivate = TryActivate,
                ForceDeactivate = ForceDeactivate,
                RestoreIfActive = RestoreIfActive,
                Update = UpdateBladeDance,
                OnLogout = Reset,
                IsActive = (p) => IsActive(),
                GetDurationRemaining = (p) => GetTimeRemaining(p),
                GetCooldownRemaining = GetCooldownRemaining,
                GetExtraHudText = null
            });
        }

        private static void AddHudStatusEffect(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            int hash = SE_BladeDance.SEName.GetStableHashCode();
            if (seman.HaveStatusEffect(hash)) return;

            var se = ScriptableObject.CreateInstance<SE_BladeDance>();
            se.name = SE_BladeDance.SEName;
            se.m_name = "Blade Dance";
            se.m_ttl = Duration;
            se.m_icon = TextureLoader.LoadAbilitySprite("BladeDance");
            seman.AddStatusEffect(se);
        }
    }
}
