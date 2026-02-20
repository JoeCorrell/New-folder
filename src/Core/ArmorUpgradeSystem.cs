using System.Collections.Generic;

namespace StartingClassMod
{
    public class ArmorSetDef
    {
        public string Id;
        public string DisplayName;
        public string[] Pieces;
        public string IconPrefab;

        public ArmorSetDef(string id, string displayName, string[] pieces, string iconPrefab)
        {
            Id = id;
            DisplayName = displayName;
            Pieces = pieces;
            IconPrefab = iconPrefab;
        }
    }

    public static class ArmorUpgradeSystem
    {
        public const int MaxLevel = 5;
        public const int CostPerLevel = 0;
        public const float BonusPerLevel = 2f;

        private const string KeyPrefix = "StartingClassMod_SetLevel_";

        private static readonly ArmorSetDef[] Sets =
        {
            new ArmorSetDef("Rag", "Rag Armor",
                new[] { "ArmorRagsChest", "ArmorRagsLegs" },
                "ArmorRagsChest"),

            new ArmorSetDef("Leather", "Leather Armor",
                new[] { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide" },
                "ArmorLeatherChest"),

            new ArmorSetDef("TrollLeather", "Troll Leather Armor",
                new[] { "ArmorTrollLeatherChest", "ArmorTrollLeatherLegs", "HelmetTrollLeather", "CapeTrollHide" },
                "ArmorTrollLeatherChest"),

            new ArmorSetDef("Bronze", "Bronze Armor",
                new[] { "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze" },
                "ArmorBronzeChest"),

            new ArmorSetDef("Iron", "Iron Armor",
                new[] { "ArmorIronChest", "ArmorIronLegs", "HelmetIron" },
                "ArmorIronChest"),

            new ArmorSetDef("Root", "Root Armor",
                new[] { "ArmorRootChest", "ArmorRootLegs", "HelmetRoot" },
                "ArmorRootChest"),

            new ArmorSetDef("Wolf", "Wolf Armor",
                new[] { "ArmorWolfChest", "ArmorWolfLegs", "HelmetDrake", "CapeWolf" },
                "ArmorWolfChest"),

            new ArmorSetDef("Padded", "Padded Armor",
                new[] { "ArmorPaddedCuirass", "ArmorPaddedGreaves", "HelmetPadded" },
                "ArmorPaddedCuirass"),

            new ArmorSetDef("Fenris", "Fenris Armor",
                new[] { "ArmorFenringChest", "ArmorFenringLegs", "HelmetFenring" },
                "ArmorFenringChest"),

            new ArmorSetDef("Carapace", "Carapace Armor",
                new[] { "ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetCarapace", "CapeFeather" },
                "ArmorCarapaceChest"),

            new ArmorSetDef("Mage", "Mage Armor",
                new[] { "ArmorMageChest", "ArmorMageLegs", "HelmetMage" },
                "ArmorMageChest"),

            new ArmorSetDef("Flametal", "Flametal Armor",
                new[] { "ArmorFlametalChest", "ArmorFlametalLegs", "HelmetFlametal" },
                "ArmorFlametalChest"),
        };

        // Cached lookup: prefab name → set def
        private static Dictionary<string, ArmorSetDef> _prefabToSet;

        public static ArmorSetDef[] GetAllSets() => Sets;

        /// <summary>
        /// Get the enhancement level for an armor set, stored on the player.
        /// </summary>
        public static int GetSetLevel(Player player, ArmorSetDef set)
        {
            if (player == null || set == null) return 0;
            if (!player.m_customData.TryGetValue(KeyPrefix + set.Id, out string val)) return 0;
            return int.TryParse(val, out int level) ? level : 0;
        }

        /// <summary>
        /// Check if the player has ALL pieces of a specific armor set equipped.
        /// </summary>
        public static bool IsSetEquipped(Player player, ArmorSetDef set)
        {
            if (player == null || set == null) return false;
            var inv = player.GetInventory();
            if (inv == null) return false;

            var equipped = new HashSet<string>();
            foreach (var item in inv.GetAllItems())
            {
                if (!item.m_equipped) continue;
                string prefab = GetItemPrefabName(item);
                if (prefab != null) equipped.Add(prefab);
            }

            foreach (var piece in set.Pieces)
            {
                if (!equipped.Contains(piece)) return false;
            }
            return true;
        }

        /// <summary>
        /// Upgrade an entire armor set by one level.
        /// </summary>
        public static bool TryUpgradeSet(Player player, ArmorSetDef set)
        {
            if (player == null || set == null) return false;
            if (!IsSetEquipped(player, set)) return false;

            int current = GetSetLevel(player, set);
            if (current >= MaxLevel) return false;

            if (!SkillPointSystem.SpendPoints(player, CostPerLevel)) return false;

            player.m_customData[KeyPrefix + set.Id] = (current + 1).ToString();
            StartingClassPlugin.Log($"Armor set enhanced: {set.DisplayName} to level {current + 1}");
            return true;
        }

        /// <summary>
        /// Find which armor set a given item belongs to (for the Harmony patch).
        /// </summary>
        public static ArmorSetDef FindSetForItem(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            string prefab = GetItemPrefabName(item);
            if (prefab == null) return null;

            if (_prefabToSet == null) BuildPrefabCache();
            _prefabToSet.TryGetValue(prefab, out ArmorSetDef set);
            return set;
        }

        /// <summary>
        /// Get the armor bonus to apply to a single item based on its set's level.
        /// Used by the Harmony patch on GetArmor().
        /// </summary>
        public static float GetItemSetBonus(Player player, ItemDrop.ItemData item)
        {
            var set = FindSetForItem(item);
            if (set == null) return 0f;
            return GetSetLevel(player, set) * BonusPerLevel;
        }

        /// <summary>
        /// Get the icon sprite for a set via ObjectDB prefab lookup.
        /// </summary>
        public static UnityEngine.Sprite GetSetIcon(ArmorSetDef set)
        {
            if (set == null || string.IsNullOrEmpty(set.IconPrefab)) return null;
            var prefab = ObjectDB.instance?.GetItemPrefab(set.IconPrefab);
            if (prefab == null) return null;
            var drop = prefab.GetComponent<ItemDrop>();
            return drop?.m_itemData?.GetIcon();
        }

        private static string GetItemPrefabName(ItemDrop.ItemData item)
        {
            if (item.m_dropPrefab != null) return item.m_dropPrefab.name;
            return null;
        }

        private static void BuildPrefabCache()
        {
            _prefabToSet = new Dictionary<string, ArmorSetDef>();
            foreach (var set in Sets)
            {
                foreach (var piece in set.Pieces)
                {
                    _prefabToSet[piece] = set;
                }
            }
        }
    }
}
