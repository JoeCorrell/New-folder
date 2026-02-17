using System.Collections.Generic;

namespace StartingClassMod
{
    /// <summary>
    /// Defines all available starting classes with their items and skill bonuses.
    /// All items are vanilla Valheim prefabs. Skill bonuses are small (5-10 levels).
    /// </summary>
    public static class ClassDefinitions
    {
        public static List<StartingClass> GetAll()
        {
            return new List<StartingClass>
            {
                Farmer(),
                Fisher(),
                Carpenter(),
                Miner()
            };
        }

        /// <summary>
        /// Farmer - Focuses on cultivation and sustenance.
        /// Gets a cultivator, hoe, turnip seeds, carrot seeds, and basic food.
        /// Skill bonuses: Farming-related skills.
        /// </summary>
        private static StartingClass Farmer()
        {
            return new StartingClass(
                "Farmer",
                "A skilled cultivator of the land.\nStarts with farming tools, seeds, and knowledge of the soil.",
                "Cultivator",
                new List<StartingItem>
                {
                    new StartingItem("Cultivator"),
                    new StartingItem("Hoe"),
                    new StartingItem("TurnipSeeds", 10),
                    new StartingItem("CarrotSeeds", 10),
                    new StartingItem("CookedMeat", 5),
                    new StartingItem("Torch")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Blocking, 5f),
                    new SkillBonus(Skills.SkillType.Run, 5f)
                }
            );
        }

        /// <summary>
        /// Fisher - Focuses on fishing and water survival.
        /// Gets a fishing rod, bait, and a raft nail for early water access.
        /// Skill bonuses: Swim-related skills.
        /// </summary>
        private static StartingClass Fisher()
        {
            return new StartingClass(
                "Fisher",
                "A patient angler and friend of the sea.\nStarts with fishing gear, bait, and enhanced swimming ability.",
                "FishingRod",
                new List<StartingItem>
                {
                    new StartingItem("FishingRod"),
                    new StartingItem("FishingBait", 50),
                    new StartingItem("Torch"),
                    new StartingItem("KnifeFlint"),
                    new StartingItem("CookedMeat", 5)
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Swim, 10f),
                    new SkillBonus(Skills.SkillType.Knives, 5f)
                }
            );
        }

        /// <summary>
        /// Carpenter - Focuses on building and woodworking.
        /// Gets a hammer, bronze axe, wood, and building materials.
        /// Skill bonuses: Woodcutting and axes.
        /// </summary>
        private static StartingClass Carpenter()
        {
            return new StartingClass(
                "Carpenter",
                "A master builder and woodworker.\nStarts with building tools, wood, and axe proficiency.",
                "Hammer",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("AxeBronze"),
                    new StartingItem("Wood", 100),
                    new StartingItem("RoundLog", 30),
                    new StartingItem("BronzeNails", 30),
                    new StartingItem("Torch")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.WoodCutting, 10f),
                    new SkillBonus(Skills.SkillType.Axes, 5f)
                }
            );
        }

        /// <summary>
        /// Miner - Focuses on mining and underground exploration.
        /// Gets a pickaxe, some ore, and a shield for cave defense.
        /// Skill bonuses: Pickaxes and blocking.
        /// </summary>
        private static StartingClass Miner()
        {
            return new StartingClass(
                "Miner",
                "A stalwart excavator of Midgard's depths.\nStarts with mining tools, ore, and defensive skills.",
                "PickaxeBronze",
                new List<StartingItem>
                {
                    new StartingItem("PickaxeBronze"),
                    new StartingItem("ShieldBronzeBuckler"),
                    new StartingItem("CopperOre", 10),
                    new StartingItem("TinOre", 10),
                    new StartingItem("Torch"),
                    new StartingItem("CookedMeat", 5)
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Pickaxes, 10f),
                    new SkillBonus(Skills.SkillType.Blocking, 5f)
                }
            );
        }
    }
}
