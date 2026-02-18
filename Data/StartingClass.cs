using System.Collections.Generic;

namespace StartingClassMod
{
    /// <summary>
    /// Defines a starting class with its role description, starting items, and skill bonuses.
    /// All items use existing vanilla Valheim prefab names.
    /// </summary>
    public class StartingClass
    {
        public string Name { get; }
        public string Description { get; }
        public List<StartingItem> Items { get; }
        public List<SkillBonus> SkillBonuses { get; }
        /// <summary>Prefab names to visually equip on the preview model.</summary>
        public List<string> PreviewEquipment { get; }
        /// <summary>Prefab name whose icon represents this class in the list.</summary>
        public string IconPrefab { get; }

        public StartingClass(string name, string description,
            List<StartingItem> items, List<SkillBonus> skillBonuses,
            List<string> previewEquipment = null, string iconPrefab = null)
        {
            Name = name;
            Description = description;
            Items = items;
            SkillBonuses = skillBonuses;
            PreviewEquipment = previewEquipment ?? new List<string>();
            IconPrefab = iconPrefab;
        }
    }

    public class StartingItem
    {
        public string PrefabName { get; }
        public int Quantity { get; }

        public StartingItem(string prefabName, int quantity = 1)
        {
            PrefabName = prefabName;
            Quantity = quantity;
        }
    }

    public class SkillBonus
    {
        public Skills.SkillType SkillType { get; }
        public float BonusLevel { get; }

        public SkillBonus(Skills.SkillType skillType, float bonusLevel)
        {
            SkillType = skillType;
            BonusLevel = bonusLevel;
        }
    }
}
