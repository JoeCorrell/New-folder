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

        public StartingClass(string name, string description,
            List<StartingItem> items, List<SkillBonus> skillBonuses)
        {
            Name = name;
            Description = description;
            Items = items;
            SkillBonuses = skillBonuses;
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
