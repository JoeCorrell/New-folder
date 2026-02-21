using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Hunter active ability: Pathfinder.
    /// Spawns red LineRenderer trails on the ground leading toward wild animals
    /// within 100m. 60 second duration, 10 minute cooldown.
    /// Trails refresh every 0.3s so they update as the player moves.
    /// Uses object pooling — one LineRenderer per animal trail.
    /// </summary>
    public static class Pathfinder
    {
        private const float Duration = 60f;
        private const float Cooldown = 600f;
        private const float ScanRange = 100f;
        private const float ScanInterval = 0.3f;
        private const float PointSpacing = 3f;
        private const float LineWidthStart = 0.35f;
        private const float LineWidthEnd = 0.15f;
        private const float GroundOffset = 0.2f;
        private const int MaxTrails = 10;
        private const string CooldownKey = "StartingClassMod_Pathfinder_CD";
        private const string DurationKey = "StartingClassMod_Pathfinder_End";

        private static float _nextScanTime;

        // Object pool — LineRenderers are repositioned each refresh, not destroyed
        private static readonly List<GameObject> _pool = new List<GameObject>();
        private static int _activeCount;

        private static readonly Color TrailColor = new Color(0.9f, 0.1f, 0.05f, 0.8f);

        // Shared material for all trails
        private static Material _sharedTrailMaterial;

        /// <summary>Known animal/fauna prefab names — includes both passive and hostile fauna.</summary>
        private static readonly HashSet<string> AnimalPrefabs = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Boar", "Boar_piggy",
            "Deer",
            "Lox", "Lox_Calf",
            "Neck",
            "Wolf", "Wolf_cub",
            "Hare", "Hen", "Chicken",
            "Serpent", "Tick"
        };

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

        public static bool TryActivate(Player player)
        {
            if (player == null) return false;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Hunter") return false;
            if (!AbilityManager.IsAbilityUnlocked(player, "Hunter", 5)) return false;

            if (IsActive()) return false;

            if (GetCooldownRemaining(player) > 0f)
            {
                player.Message(MessageHud.MessageType.Center, "Pathfinder is not ready");
                return false;
            }

            if (ZNet.instance == null) return false;
            double now = ZNet.instance.GetTimeSeconds();
            player.m_customData[DurationKey] = (now + Duration).ToString("F0");
            player.m_customData[CooldownKey] = (now + Duration + Cooldown).ToString("F0");

            _nextScanTime = 0f;
            RefreshTrails(player);
            AddHudStatusEffect(player);

            player.Message(MessageHud.MessageType.Center, "Pathfinder activated");
            return true;
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
                    RefreshTrails(player);
                }
            }
            else if (_activeCount > 0 || player.m_customData.ContainsKey(DurationKey))
            {
                if (GetTimeRemaining() <= 0f && player.m_customData.ContainsKey(DurationKey))
                {
                    HideAllTrails();
                    player.m_customData.Remove(DurationKey);
                    player.Message(MessageHud.MessageType.Center, "Pathfinder expired");
                }
            }
        }

        /// <summary>
        /// Recalculates all trail lines from the player's current position toward each
        /// wild animal in range. LineRenderers are pooled — existing ones are repositioned,
        /// new ones created only if the pool is too small, extras hidden.
        /// </summary>
        private static void RefreshTrails(Player player)
        {
            Vector3 playerPos = player.transform.position;
            List<Character> allChars = Character.GetAllCharacters();
            int trailIndex = 0;

            foreach (var character in allChars)
            {
                if (trailIndex >= MaxTrails) break;
                if (character == null || character.IsDead()) continue;
                if (character.IsPlayer()) continue;
                if (character.IsTamed()) continue;

                string prefabName = Utils.GetPrefabName(character.gameObject.name);
                if (!AnimalPrefabs.Contains(prefabName)) continue;

                float dist = Vector3.Distance(playerPos, character.transform.position);
                if (dist > ScanRange) continue;

                Vector3 targetPos = character.transform.position;
                Vector3 direction = targetPos - playerPos;
                direction.y = 0f;
                float flatDist = direction.magnitude;
                if (flatDist < PointSpacing * 2f) continue;
                direction.Normalize();

                // Build ground-snapped points from player toward animal
                int numPoints = Mathf.Min(
                    Mathf.FloorToInt(flatDist / PointSpacing) + 1,
                    60); // cap points per trail

                var points = new Vector3[numPoints];
                for (int i = 0; i < numPoints; i++)
                {
                    float t = (float)i / (numPoints - 1);
                    Vector3 flatPos = Vector3.Lerp(playerPos, targetPos, t);
                    float groundY = GetGroundHeight(flatPos);
                    points[i] = new Vector3(flatPos.x, groundY + GroundOffset, flatPos.z);
                }

                var trail = GetOrCreatePoolTrail(trailIndex);
                var lr = trail.GetComponent<LineRenderer>();
                lr.positionCount = numPoints;
                lr.SetPositions(points);
                trail.SetActive(true);
                trailIndex++;
            }

            // Hide any pool objects beyond what we used this frame
            for (int i = trailIndex; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                    _pool[i].SetActive(false);
            }
            _activeCount = trailIndex;
        }

        /// <summary>Returns the pool trail at the given index, creating it if needed.</summary>
        private static GameObject GetOrCreatePoolTrail(int index)
        {
            if (index < _pool.Count && _pool[index] != null)
                return _pool[index];

            var go = new GameObject($"PathfinderTrail_{index}");

            var lr = go.AddComponent<LineRenderer>();
            lr.material = GetSharedMaterial();
            lr.startWidth = LineWidthStart;
            lr.endWidth = LineWidthEnd;
            lr.startColor = TrailColor;
            lr.endColor = TrailColor;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            // Face camera so trails are visible from any angle
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (index < _pool.Count)
                _pool[index] = go;
            else
                _pool.Add(go);

            return go;
        }

        /// <summary>Get ground height using ZoneSystem if available, else raycast fallback.</summary>
        private static float GetGroundHeight(Vector3 pos)
        {
            if (ZoneSystem.instance != null)
            {
                float height;
                if (ZoneSystem.instance.GetGroundHeight(pos, out height))
                    return height;
            }

            // Fallback: raycast down from high up
            Vector3 rayOrigin = new Vector3(pos.x, pos.y + 500f, pos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, LayerMask.GetMask("terrain")))
                return hit.point.y;

            return pos.y;
        }

        private static Material GetSharedMaterial()
        {
            if (_sharedTrailMaterial == null)
            {
                // Sprites/Default is guaranteed to exist in Unity and supports vertex colors
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    // Ultimate fallback
                    shader = Shader.Find("UI/Default");
                }
                _sharedTrailMaterial = new Material(shader);
                _sharedTrailMaterial.color = TrailColor;
            }
            return _sharedTrailMaterial;
        }

        public static void Register()
        {
            ActiveAbilityRegistry.Register(new ActiveAbilityRegistry.Entry
            {
                PowerId = "Pathfinder",
                ClassName = "Hunter",
                AbilityIndex = 5,
                DisplayName = "Pathfinder",
                TryActivate = TryActivate,
                ForceDeactivate = ForceDeactivate,
                RestoreIfActive = RestoreIfActive,
                Update = UpdatePathfinder,
                OnLogout = ClearAll,
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
        /// Full deactivation: destroys all pool objects, clears shared material, and removes
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

        /// <summary>Destroy all pool trails and clean up shared resources.</summary>
        public static void ClearAll()
        {
            DestroyPool();
            if (_sharedTrailMaterial != null)
            {
                Object.Destroy(_sharedTrailMaterial);
                _sharedTrailMaterial = null;
            }
        }

        /// <summary>Hide all trails without destroying the pool.</summary>
        private static void HideAllTrails()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                    _pool[i].SetActive(false);
            }
            _activeCount = 0;
        }

        /// <summary>Destroy all pool GameObjects.</summary>
        private static void DestroyPool()
        {
            for (int i = _pool.Count - 1; i >= 0; i--)
            {
                if (_pool[i] != null)
                    Object.Destroy(_pool[i]);
            }
            _pool.Clear();
            _activeCount = 0;
        }
    }
}
