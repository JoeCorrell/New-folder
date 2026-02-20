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
        /// <summary>Structured ability data for the skill tree UI.</summary>
        public List<ClassAbility> Abilities { get; }

        public StartingClass(string name, string description,
            List<StartingItem> items, List<SkillBonus> skillBonuses,
            List<string> previewEquipment = null, string iconPrefab = null,
            List<ClassAbility> abilities = null)
        {
            Name = name;
            Description = description;
            Items = items;
            SkillBonuses = skillBonuses;
            PreviewEquipment = previewEquipment ?? new List<string>();
            IconPrefab = iconPrefab;
            Abilities = abilities ?? new List<ClassAbility>();
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

    public class ClassAbility
    {
        public string Name { get; }
        public string Description { get; }
        public bool IsPassive { get; }
        public int PointCost { get; }

        public ClassAbility(string name, string description, bool isPassive, int pointCost = 0)
        {
            Name = name;
            Description = description;
            IsPassive = isPassive;
            PointCost = pointCost;
        }
    }
}
