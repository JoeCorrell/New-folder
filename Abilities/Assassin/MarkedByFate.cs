using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Assassin ultimate ability: Mark enemies your crosshair is aimed at.
    /// Up to 3 marks at once (charge-based, no cooldown).
    /// Marked enemies glow red and appear on the minimap.
    /// Marks persist until the target dies or you manually unmark with Shift+V.
    /// Unmarking gives back 1 charge.
    /// </summary>
    public static class MarkedByFate
    {
        private const float MarkRange = 50f;
        private const int MaxCharges = 3;

        // Glow pulse color
        private static readonly Color GlowColor = new Color(1f, 0.15f, 0.1f, 1f);

        private struct Mark
        {
            public Character Target;
            public Minimap.PinData Pin;
        }

        private static readonly List<Mark> _activeMarks = new List<Mark>();
        private static AudioClip _markSfx;

        /// <summary>Number of charges currently available (not spent on active marks).</summary>
        public static int GetChargesRemaining() => MaxCharges - _activeMarks.Count;

        /// <summary>Number of enemies currently marked.</summary>
        public static int GetActiveMarkCount() => _activeMarks.Count;

        /// <summary>Try to mark the enemy under the crosshair.</summary>
        public static void TryActivate(Player player)
        {
            if (player == null) return;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Assassin") return;
            if (!AbilityManager.IsAbilityUnlocked(player, "Assassin", 1)) return;

            // Check charges
            if (_activeMarks.Count >= MaxCharges)
                return;

            // Get the creature the player is looking at
            Character target = GetAimedCharacter(player);

            if (target == null || target.IsPlayer() || target.IsDead())
                return;

            // Check range
            float dist = Vector3.Distance(player.transform.position, target.transform.position);
            if (dist > MarkRange)
                return;

            // Check if this enemy is already marked
            if (IsMarked(target))
                return;

            var pin = Minimap.instance?.AddPin(
                target.transform.position,
                Minimap.PinType.Icon3,
                target.m_name,
                false,
                false,
                0L);

            if (pin != null)
            {
                _activeMarks.Add(new Mark
                {
                    Target = target,
                    Pin = pin
                });

                ApplyGlow(target);
                PlayMarkSfx(player);
            }
        }

        /// <summary>Shift+V: unmark the enemy the player is looking at.</summary>
        public static void TryUnmark(Player player)
        {
            if (player == null) return;
            if (_activeMarks.Count == 0) return;

            Character target = GetAimedCharacter(player);
            if (target == null) return;

            for (int i = _activeMarks.Count - 1; i >= 0; i--)
            {
                if (_activeMarks[i].Target == target)
                {
                    RemoveGlow(_activeMarks[i].Target);
                    RemoveMark(i);
                    PlayMarkSfx(player);
                    return;
                }
            }
        }

        /// <summary>Check if a specific character is currently marked.</summary>
        public static bool IsMarked(Character character)
        {
            if (character == null) return false;
            foreach (var mark in _activeMarks)
                if (mark.Target == character) return true;
            return false;
        }

        /// <summary>Update mark positions and glow each frame.</summary>
        public static void UpdateMarks()
        {
            if (_activeMarks.Count == 0) return;

            for (int i = _activeMarks.Count - 1; i >= 0; i--)
            {
                var mark = _activeMarks[i];

                // Remove marks for dead or destroyed targets
                if (mark.Target == null || mark.Target.IsDead())
                {
                    if (mark.Target != null)
                        RemoveGlow(mark.Target);
                    RemoveMark(i);
                    continue;
                }

                // Remove marks for enemies that are too far away (>100m)
                var localPlayer = Player.m_localPlayer;
                if (localPlayer != null)
                {
                    float dist = Vector3.Distance(localPlayer.transform.position, mark.Target.transform.position);
                    if (dist > 100f)
                    {
                        RemoveGlow(mark.Target);
                        RemoveMark(i);
                        continue;
                    }
                }

                // Update pin position to track the enemy
                if (mark.Pin != null)
                    mark.Pin.m_pos = mark.Target.transform.position;

                // Pulse the glow
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

        /// <summary>Get the character the player is aiming at via crosshair.</summary>
        private static Character GetAimedCharacter(Player player)
        {
            Character target = player.GetHoverCreature();
            if (target != null) return target;

            var cam = GameCamera.instance;
            if (cam == null) return null;

            int mask = LayerMask.GetMask("character", "character_net");
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, MarkRange, mask))
            {
                var rb = hit.collider.attachedRigidbody;
                return rb != null ? rb.GetComponent<Character>() : hit.collider.GetComponent<Character>();
            }
            return null;
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

        private static void PlayMarkSfx(Player player)
        {
            if (_markSfx == null)
            {
                foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                {
                    if (clip.name == "UI_InventoryHide_S_01")
                    { _markSfx = clip; break; }
                }
            }
            if (_markSfx != null)
                AudioSource.PlayClipAtPoint(_markSfx, player.transform.position);
        }
    }
}
