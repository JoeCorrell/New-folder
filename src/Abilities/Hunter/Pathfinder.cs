using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Hunter active ability: Pathfinder.
    /// Spawns glowing particle trails on the ground leading toward all creatures
    /// within 100m. 60 second duration, 10 minute cooldown.
    /// Trails are refreshed every 3 seconds during the active period.
    /// </summary>
    public static class Pathfinder
    {
        private const float Duration = 60f;
        private const float Cooldown = 600f;
        private const float ScanRange = 100f;
        private const float ScanInterval = 3f;
        private const float MarkerSpacing = 5f;
        private const float MarkerLifetime = 4f;
        private const float MarkerScale = 0.25f;
        private const int MaxMarkers = 50;
        private const string CooldownKey = "StartingClassMod_Pathfinder_CD";
        private const string DurationKey = "StartingClassMod_Pathfinder_End";

        private static float _nextScanTime;
        private static readonly List<GameObject> _markers = new List<GameObject>();
        private static readonly Color MarkerColor = new Color(1f, 0.85f, 0.3f, 1f);

        private static readonly int GroundMask = LayerMask.GetMask("terrain", "Default", "static_solid");

        // Shared material to avoid per-marker material cloning
        private static Material _sharedMarkerMaterial;

        private static readonly FieldInfo ZanimField =
            AccessTools.Field(typeof(Character), "m_zanim");
        private static readonly FieldInfo GuardianSEField =
            AccessTools.Field(typeof(Player), "m_guardianSE");

        public static bool IsActive()
        {
            var player = Player.m_localPlayer;
            if (player == null) return false;
            return GetTimeRemaining(player) > 0f;
        }

        public static float GetTimeRemaining() => GetTimeRemaining(Player.m_localPlayer);

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

        public static float GetCooldownRemaining(Player player)
        {
            if (player == null) return 0f;
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

        public static void TryActivate(Player player)
        {
            if (player == null) return;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Hunter") return;
            if (!AbilityManager.IsAbilityUnlocked(player, "Hunter", 5)) return;

            if (IsActive()) return;

            if (GetCooldownRemaining(player) > 0f)
            {
                player.Message(MessageHud.MessageType.Center, "Pathfinder is not ready");
                return;
            }

            if (ZNet.instance == null) return;
            double now = ZNet.instance.GetTimeSeconds();
            player.m_customData[DurationKey] = (now + Duration).ToString("F0");
            player.m_customData[CooldownKey] = (now + Duration + Cooldown).ToString("F0");

            _nextScanTime = 0f;
            SpawnTrailMarkers(player);
            PlayActivateEffects(player);
            AddHudStatusEffect(player);

            player.Message(MessageHud.MessageType.Center, "Pathfinder activated");
        }

        /// <summary>Called each frame from plugin Update.</summary>
        public static void UpdatePathfinder()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            if (IsActive())
            {
                if (Time.time >= _nextScanTime)
                {
                    _nextScanTime = Time.time + ScanInterval;
                    DestroyAllMarkers();
                    SpawnTrailMarkers(player);
                }
            }
            else if (_markers.Count > 0 || player.m_customData.ContainsKey(DurationKey))
            {
                if (GetTimeRemaining() <= 0f && player.m_customData.ContainsKey(DurationKey))
                {
                    DestroyAllMarkers();
                    player.m_customData.Remove(DurationKey);
                    player.Message(MessageHud.MessageType.Center, "Pathfinder expired");
                }
            }
        }

        private static void SpawnTrailMarkers(Player player)
        {
            Vector3 playerPos = player.transform.position;
            List<Character> allChars = Character.GetAllCharacters();
            int totalMarkers = 0;

            foreach (var character in allChars)
            {
                if (totalMarkers >= MaxMarkers) break;
                if (character == null || character.IsDead()) continue;
                if (character.IsPlayer()) continue;
                if (character.IsTamed()) continue;

                float dist = Vector3.Distance(playerPos, character.transform.position);
                if (dist > ScanRange) continue;

                Vector3 targetPos = character.transform.position;
                Vector3 direction = (targetPos - playerPos);
                direction.y = 0f;
                float flatDist = direction.magnitude;
                if (flatDist < MarkerSpacing) continue;
                direction.Normalize();

                int numMarkers = Mathf.Min(
                    Mathf.FloorToInt(flatDist / MarkerSpacing),
                    MaxMarkers - totalMarkers);

                for (int i = 1; i <= numMarkers; i++)
                {
                    Vector3 pos = playerPos + direction * (i * MarkerSpacing);
                    pos = GetGroundPosition(pos);
                    CreateMarker(pos);
                }
                totalMarkers += numMarkers;
            }
        }

        private static Vector3 GetGroundPosition(Vector3 pos)
        {
            Vector3 rayOrigin = new Vector3(pos.x, pos.y + 100f, pos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, GroundMask))
                return hit.point + Vector3.up * 0.15f;
            return pos;
        }

        private static Material GetSharedMaterial()
        {
            if (_sharedMarkerMaterial == null)
            {
                _sharedMarkerMaterial = new Material(Shader.Find("Standard"));
                _sharedMarkerMaterial.color = MarkerColor;
                _sharedMarkerMaterial.SetColor("_EmissionColor", MarkerColor * 3f);
                _sharedMarkerMaterial.EnableKeyword("_EMISSION");
            }
            return _sharedMarkerMaterial;
        }

        private static void CreateMarker(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * MarkerScale;

            // Remove collider so it doesn't interfere
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            // Use shared emissive gold material (avoids per-marker material cloning)
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetSharedMaterial();

            _markers.Add(go);
            Object.Destroy(go, MarkerLifetime);
        }

        private static void PlayActivateEffects(Player player)
        {
            var zanim = ZanimField?.GetValue(player) as ZSyncAnimation;
            if (zanim != null)
                zanim.SetTrigger("gpower");

            var guardianSE = GuardianSEField?.GetValue(player) as StatusEffect;
            if (guardianSE != null && guardianSE.m_startEffects != null)
                guardianSE.m_startEffects.Create(player.GetCenterPoint(), player.transform.rotation, player.transform);
        }

        private static void AddHudStatusEffect(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            int hash = SE_Pathfinder.SEName.GetStableHashCode();
            if (seman.HaveStatusEffect(hash)) return;

            var se = ScriptableObject.CreateInstance<SE_Pathfinder>();
            se.name = SE_Pathfinder.SEName;
            se.m_name = "Pathfinder";
            se.m_ttl = Duration;
            se.m_icon = TextureLoader.LoadAbilitySprite("Pathfinder");
            seman.AddStatusEffect(se);
        }

        /// <summary>
        /// Full deactivation: destroys markers, clears shared material, and removes
        /// m_customData keys so UpdatePathfinder() does not re-spawn trails next frame.
        /// Use on class switch/reset. On logout, call ClearAll() instead.
        /// </summary>
        public static void ForceDeactivate(Player player)
        {
            ClearAll();
            if (player != null)
            {
                player.m_customData.Remove(DurationKey);
                player.m_customData.Remove(CooldownKey);
            }
        }

        /// <summary>Destroy all trail markers and clean up shared resources.</summary>
        public static void ClearAll()
        {
            DestroyAllMarkers();
            if (_sharedMarkerMaterial != null)
            {
                Object.Destroy(_sharedMarkerMaterial);
                _sharedMarkerMaterial = null;
            }
        }

        private static void DestroyAllMarkers()
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                if (_markers[i] != null)
                    Object.Destroy(_markers[i]);
            }
            _markers.Clear();
        }
    }
}
