using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Assassin active ability: AoE scan that marks all enemies within 50m.
    /// Marked enemies glow red and appear on the minimap.
    /// Continuously scans for the full duration, marking new enemies that enter range.
    /// 10 minute cooldown. Activated via GP button when selected as active power.
    /// </summary>
    public static class MarkedByFate
    {
        private const float ScanRange = 50f;
        private const float ExpireRange = 100f;
        private const float Duration = 600f; // 10 minutes active
        private const float Cooldown = 600f; // 10 minutes cooldown
        private const float ScanInterval = 1f; // Re-scan every 1 second
        private const string CooldownKey = "StartingClassMod_MarkedByFate_CD";
        private const string DurationKey = "StartingClassMod_MarkedByFate_End";

        private static float _nextScanTime;

        private static readonly Color GlowColor = new Color(1f, 0.15f, 0.1f, 1f);

        private struct Mark
        {
            public Character Target;
            public Minimap.PinData Pin;
        }

        private static readonly List<Mark> _activeMarks = new List<Mark>();

        // Reflection fields for activation effects
        private static readonly System.Reflection.FieldInfo ZanimField =
            AccessTools.Field(typeof(Character), "m_zanim");
        private static readonly System.Reflection.FieldInfo GuardianSEField =
            AccessTools.Field(typeof(Player), "m_guardianSE");

        /// <summary>Number of enemies currently marked.</summary>
        public static int GetActiveMarkCount() => _activeMarks.Count;

        /// <summary>Whether the ability is currently active (scanning).</summary>
        public static bool IsActive(Player player)
        {
            if (player == null) return false;
            return GetDurationRemaining(player) > 0f;
        }

        /// <summary>Seconds remaining on the active scan duration.</summary>
        public static float GetDurationRemaining(Player player)
        {
            if (player == null) return 0f;
            if (!player.m_customData.TryGetValue(DurationKey, out string val)) return 0f;
            if (!double.TryParse(val, out double endTime)) return 0f;
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0;
            float remaining = (float)(endTime - now);
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>Get cooldown remaining in seconds. Returns 0 while ability is active (duration running).</summary>
        public static float GetCooldownRemaining(Player player)
        {
            if (player == null) return 0f;
            // No cooldown while the ability is still active
            if (GetDurationRemaining(player) > 0f) return 0f;
            if (!player.m_customData.TryGetValue(CooldownKey, out string val)) return 0f;
            if (!double.TryParse(val, out double endTime)) return 0f;
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0;
            float remaining = (float)(endTime - now);
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>Activate: begin continuous scanning. Called from GP intercept.</summary>
        public static void TryActivate(Player player)
        {
            if (player == null) return;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Assassin") return;
            if (!AbilityManager.IsAbilityUnlocked(player, "Assassin", 1)) return;

            // Block if already active
            if (IsActive(player)) return;

            // Check cooldown
            if (GetCooldownRemaining(player) > 0f)
            {
                player.Message(MessageHud.MessageType.Center, "Marked by Fate is not ready");
                return;
            }

            // Clear existing marks from previous activation
            ClearAllMarks();

            // Set duration end time
            if (ZNet.instance != null)
            {
                double now = ZNet.instance.GetTimeSeconds();
                player.m_customData[DurationKey] = (now + Duration).ToString("F0");
                // Cooldown starts after duration ends
                player.m_customData[CooldownKey] = (now + Duration + Cooldown).ToString("F0");
            }

            // Do initial scan immediately
            _nextScanTime = 0f;
            ScanAndMark(player);

            // Play activation effects
            PlayActivateEffects(player);

            // Add HUD status effect with icon + duration timer
            AddHudStatusEffect(player);

            player.Message(MessageHud.MessageType.Center, "Marked by Fate activated");
        }

        /// <summary>Scan for enemies within range and mark any new ones.</summary>
        private static int ScanAndMark(Player player)
        {
            List<Character> allChars = Character.GetAllCharacters();
            int newMarks = 0;
            Vector3 playerPos = player.transform.position;

            foreach (var character in allChars)
            {
                if (character == null || character.IsDead()) continue;
                if (character.IsPlayer()) continue;
                if (character.IsTamed()) continue;
                if (IsMarked(character)) continue; // Already tracked

                float dist = Vector3.Distance(playerPos, character.transform.position);
                if (dist > ScanRange) continue;

                var pin = Minimap.instance?.AddPin(
                    character.transform.position,
                    Minimap.PinType.Icon3,
                    character.m_name,
                    false, false, 0L);

                if (pin != null)
                {
                    _activeMarks.Add(new Mark { Target = character, Pin = pin });
                    ApplyGlow(character);
                    newMarks++;
                }
            }
            return newMarks;
        }

        private static void PlayActivateEffects(Player player)
        {
            // Trigger gpower animation (raises hands)
            var zanim = ZanimField?.GetValue(player) as ZSyncAnimation;
            if (zanim != null)
                zanim.SetTrigger("gpower");

            // Play the forsaken power's start effects (sound + visuals)
            var guardianSE = GuardianSEField?.GetValue(player) as StatusEffect;
            if (guardianSE != null && guardianSE.m_startEffects != null)
                guardianSE.m_startEffects.Create(player.GetCenterPoint(), player.transform.rotation, player.transform);
        }

        /// <summary>Check if a specific character is currently marked.</summary>
        public static bool IsMarked(Character character)
        {
            if (character == null) return false;
            foreach (var mark in _activeMarks)
                if (mark.Target == character) return true;
            return false;
        }

        /// <summary>Update mark positions, glow, and continuous scanning each frame.</summary>
        public static void UpdateMarks()
        {
            var player = Player.m_localPlayer;

            // While active, continuously scan for new enemies entering range
            if (player != null && IsActive(player))
            {
                if (Time.time >= _nextScanTime)
                {
                    _nextScanTime = Time.time + ScanInterval;
                    ScanAndMark(player);
                }
            }
            else if (player != null && _activeMarks.Count > 0 && GetDurationRemaining(player) <= 0f
                     && player.m_customData.ContainsKey(DurationKey))
            {
                // Duration just expired — clear all marks
                ClearAllMarks();
                player.m_customData.Remove(DurationKey);
                player.Message(MessageHud.MessageType.Center, "Marked by Fate expired");
                return;
            }

            if (_activeMarks.Count == 0) return;

            for (int i = _activeMarks.Count - 1; i >= 0; i--)
            {
                var mark = _activeMarks[i];

                if (mark.Target == null || mark.Target.IsDead())
                {
                    if (mark.Target != null)
                        RemoveGlow(mark.Target);
                    RemoveMark(i);
                    continue;
                }

                if (player != null)
                {
                    float dist = Vector3.Distance(player.transform.position, mark.Target.transform.position);
                    if (dist > ExpireRange)
                    {
                        RemoveGlow(mark.Target);
                        RemoveMark(i);
                        continue;
                    }
                }

                if (mark.Pin != null)
                    mark.Pin.m_pos = mark.Target.transform.position;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                Color pulsed = GlowColor * (0.6f + pulse * 0.4f);
                if (MaterialMan.instance != null)
                    MaterialMan.instance.SetValue(mark.Target.gameObject, ShaderProps._EmissionColor, pulsed);
            }
        }

        /// <summary>Clear all active marks.</summary>
        public static void ClearAllMarks()
        {
            for (int i = _activeMarks.Count - 1; i >= 0; i--)
            {
                if (_activeMarks[i].Target != null)
                    RemoveGlow(_activeMarks[i].Target);
                RemoveMark(i);
            }
        }

        private static void RemoveMark(int index)
        {
            var mark = _activeMarks[index];
            if (mark.Pin != null && Minimap.instance != null)
                Minimap.instance.RemovePin(mark.Pin);
            _activeMarks.RemoveAt(index);
        }

        private static void ApplyGlow(Character target)
        {
            if (target == null || MaterialMan.instance == null) return;
            MaterialMan.instance.SetValue(target.gameObject, ShaderProps._EmissionColor, GlowColor);
        }

        private static void RemoveGlow(Character target)
        {
            if (target == null || MaterialMan.instance == null) return;
            MaterialMan.instance.ResetValue(target.gameObject, ShaderProps._EmissionColor);
        }

        private static void AddHudStatusEffect(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            int hash = SE_MarkedByFate.SEName.GetStableHashCode();
            if (seman.HaveStatusEffect(hash)) return;

            var se = ScriptableObject.CreateInstance<SE_MarkedByFate>();
            se.name = SE_MarkedByFate.SEName;
            se.m_name = "Marked by Fate";
            se.m_ttl = Duration;
            se.m_icon = TextureLoader.LoadAbilitySprite("MarkedByFate");
            seman.AddStatusEffect(se);
        }
    }
}
