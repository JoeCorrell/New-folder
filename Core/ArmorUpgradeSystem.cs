namespace StartingClassMod
{
    public static class ArmorUpgradeSystem
    {
        public const int MaxLevel = 5;
        public const int CostPerLevel = 0;
        public const float BonusPerLevel = 2f;

        private const string EnhanceKey = "StartingClassMod_EnhanceLevel";

        private static readonly string[] Slots = { "Chest", "Legs", "Helmet", "Shoulder" };

        public static string[] GetSlotNames() => Slots;

        /// <summary>
        /// Get the enhancement level stored on a specific armor item.
        /// The level persists on the item itself, surviving unequip/re-equip.
        /// </summary>
        public static int GetUpgradeLevel(ItemDrop.ItemData item)
        {
            if (item == null) return 0;
            if (!item.m_customData.TryGetValue(EnhanceKey, out string val)) return 0;
            return int.TryParse(val, out int level) ? level : 0;
        }

        /// <summary>
        /// Get the armor bonus for a specific item based on its enhancement level.
        /// </summary>
        public static float GetArmorBonus(ItemDrop.ItemData item)
        {
            return GetUpgradeLevel(item) * BonusPerLevel;
        }

        /// <summary>
        /// Get the equipped armor piece for a slot index (0=Chest, 1=Legs, 2=Helmet, 3=Shoulder).
        /// </summary>
        public static ItemDrop.ItemData GetEquippedItem(Player player, int slotIndex)
        {
            if (player == null) return null;
            var inv = player.GetInventory();
            if (inv == null) return null;

            ItemDrop.ItemData.ItemType targetType;
            switch (slotIndex)
            {
                case 0: targetType = ItemDrop.ItemData.ItemType.Chest; break;
                case 1: targetType = ItemDrop.ItemData.ItemType.Legs; break;
                case 2: targetType = ItemDrop.ItemData.ItemType.Helmet; break;
                case 3: targetType = ItemDrop.ItemData.ItemType.Shoulder; break;
                default: return null;
            }

            foreach (var item in inv.GetAllItems())
            {
                if (item.m_equipped && item.m_shared.m_itemType == targetType)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Check if the player has armor equipped in ALL 4 slots (full set).
        /// </summary>
        public static bool IsFullSetEquipped(Player player)
        {
            if (player == null) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (GetEquippedItem(player, i) == null) return false;
            }
            return true;
        }

        /// <summary>
        /// Enhance a specific armor item. The level is stored on the item itself
        /// and persists through unequip, chest storage, trading, etc.
        /// </summary>
        public static bool TryUpgrade(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null) return false;
            if (!IsFullSetEquipped(player)) return false;

            int current = GetUpgradeLevel(item);
            if (current >= MaxLevel) return false;

            if (!SkillPointSystem.SpendPoints(player, CostPerLevel)) return false;

            item.m_customData[EnhanceKey] = (current + 1).ToString();
            StartingClassPlugin.Log($"Armor enhanced: {item.m_shared.m_name} to level {current + 1}");
            return true;
        }

        /// <summary>
        /// Total enhancement bonus across all currently equipped armor.
        /// </summary>
        public static float GetTotalEquippedBonus(Player player)
        {
            if (player == null) return 0f;
            float total = 0f;
            for (int i = 0; i < Slots.Length; i++)
            {
                var item = GetEquippedItem(player, i);
                if (item != null)
                    total += GetArmorBonus(item);
            }
            return total;
        }
    }
}
