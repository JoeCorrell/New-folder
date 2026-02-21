using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Hunter active ability: AoE scan that marks all enemies within 50m.
    /// Near-clone of MarkedByFate with orange glow instead of red.
    /// Marked enemies glow orange and appear on the minimap.
    /// 10 minute duration, 10 minute cooldown. Activated via GP button.
    /// </summary>
    public static class HuntersInstinct
    {
        private const float ScanRange = 50f;
        private const float ExpireRange = 100f;
        private const float Duration = 600f;
        private const float Cooldown = 600f;
        private const float ScanInterval = 1f;
        private const string CooldownKey = "StartingClassMod_HuntersInstinct_CD";
        private const string DurationKey = "StartingClassMod_HuntersInstinct_End";

        private static float _nextScanTime;

        private static readonly Color GlowColor = new Color(1f, 0.7f, 0.15f, 1f);

        private struct Mark
        {
            public Character Target;
            public Minimap.PinData Pin;
        }

        private static readonly List<Mark> _activeMarks = new List<Mark>();


        public static int GetActiveMarkCount() => _activeMarks.Count;

        public static bool IsActive(Player player)
        {
            if (player == null) return false;
            return GetDurationRemaining(player) > 0f;
        }

        public static float GetDurationRemaining(Player player)
        {
            if (player == null) return 0f;
            if (ZNet.instance == null) return 0f;
            if (!player.m_customData.TryGetValue(DurationKey, out string val)) return 0f;
            if (!double.TryParse(val, out double endTime)) return 0f;
            double now = ZNet.instance.GetTimeSeconds();
            float remaining = (float)(endTime - now);
            return remaining > 0f ? remaining : 0f;
        }

        public static float GetCooldownRemaining(Player player)
        {
            if (player == null) return 0f;
            if (GetDurationRemaining(player) > 0f) return 0f;
            if (!player.m_customData.TryGetValue(CooldownKey, out string val)) return 0f;
            if (!double.TryParse(val, out double endTime)) return 0f;
            if (ZNet.instance == null) return 0f;
            double now = ZNet.instance.GetTimeSeconds();
            float remaining = (float)(endTime - now);
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>Restore HUD status effect if the ability is still active (e.g. after login).</summary>
        public static void RestoreIfActive(Player player)
        {
            if (player == null || !IsActive(player)) return;
            AddHudStatusEffect(player);
        }

        public static bool TryActivate(Player player)
        {
            if (player == null) return false;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Hunter") return false;
            if (!AbilityManager.IsAbilityUnlocked(player, "Hunter", 1)) return false;

            if (IsActive(player)) return false;

            if (GetCooldownRemaining(player) > 0f)
            {
                player.Message(MessageHud.MessageType.Center, "Hunter's Instinct is not ready");
                return false;
            }

            ClearAllMarks();

            if (ZNet.instance != null)
            {
                double now = ZNet.instance.GetTimeSeconds();
                player.m_customData[DurationKey] = (now + Duration).ToString("F0");
                player.m_customData[CooldownKey] = (now + Duration + Cooldown).ToString("F0");
            }

            _nextScanTime = 0f;
            ScanAndMark(player);
            AddHudStatusEffect(player);

            player.Message(MessageHud.MessageType.Center, "Hunter's Instinct activated");
            return true;
        }

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
                if (IsMarked(character)) continue;

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

        public static void Register()
        {
            ActiveAbilityRegistry.Register(new ActiveAbilityRegistry.Entry
            {
                PowerId = "HuntersInstinct",
                ClassName = "Hunter",
                AbilityIndex = 1,
                DisplayName = "Hunter's Instinct",
                TryActivate = TryActivate,
                ForceDeactivate = ForceDeactivate,
                RestoreIfActive = RestoreIfActive,
                Update = UpdateMarks,
                OnLogout = ClearAllMarks,
                IsActive = IsActive,
                GetDurationRemaining = GetDurationRemaining,
                GetCooldownRemaining = GetCooldownRemaining,
                GetExtraHudText = (p) => $"({GetActiveMarkCount()})"
            });
        }

        public static bool IsMarked(Character character)
        {
            if (character == null) return false;
            foreach (var mark in _activeMarks)
                if (mark.Target == character) return true;
            return false;
        }

        public static void UpdateMarks()
        {
            var player = Player.m_localPlayer;

            if (player != null && IsActive(player))
            {
                if (Time.time >= _nextScanTime)
                {
                    _nextScanTime = Time.time + ScanInterval;
                    ScanAndMark(player);
                }
            }
            else if (player != null && GetDurationRemaining(player) <= 0f
                     && player.m_customData.ContainsKey(DurationKey))
            {
                if (_activeMarks.Count > 0)
                    ClearAllMarks();
                player.m_customData.Remove(DurationKey);
                player.Message(MessageHud.MessageType.Center, "Hunter's Instinct expired");
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

        /// <summary>
        /// Full deactivation: clears marks and removes m_customData keys so
        /// UpdateMarks() does not re-scan on the next frame. Use on class switch/reset.
        /// </summary>
        public static void ForceDeactivate(Player player)
        {
            ClearAllMarks();
            if (player != null)
            {
                player.m_customData.Remove(DurationKey);
                player.m_customData.Remove(CooldownKey);
            }
        }

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

            int hash = SE_HuntersInstinct.SEName.GetStableHashCode();
            if (seman.HaveStatusEffect(hash)) return;

            var se = ScriptableObject.CreateInstance<SE_HuntersInstinct>();
            se.name = SE_HuntersInstinct.SEName;
            se.m_name = "Hunter's Instinct";
            se.m_ttl = Duration;
            se.m_icon = TextureLoader.LoadAbilitySprite("HuntersInstinct");
            seman.AddStatusEffect(se);
        }
    }
}
