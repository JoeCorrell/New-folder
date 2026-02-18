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
                Archer(),
                Assassin(),
                Blacksmith(),
                Brute(),
                Carpenter(),
                Explorer(),
                Farmer(),
                Fisher(),
                Forager(),
                Healer(),
                Hunter(),
                Lumberjack(),
                Miner(),
                Tank(),
                Witch(),
                Wizard()
            };
        }

        private static List<StartingItem> PlaceholderItems()
        {
            return new List<StartingItem>
            {
                new StartingItem("Hammer")
            };
        }

        private static StartingClass Archer()
        {
            return new StartingClass(
                "Archer",
                "A sharpshooter who rains death from afar.\nStarts with a bow, arrows, and keen eyes.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Bows, 5f)
                }
            );
        }

        private static StartingClass Assassin()
        {
            return new StartingClass(
                "Assassin",
                "A shadow who strikes without warning.\nStarts with a blade, leather armor, and deadly stealth.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Sneak, 10f),
                    new SkillBonus(Skills.SkillType.Knives, 5f)
                }
            );
        }

        private static StartingClass Blacksmith()
        {
            return new StartingClass(
                "Blacksmith",
                "A master of the forge who shapes metal into might.\nStarts with smithing tools and raw materials.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Pickaxes, 5f)
                }
            );
        }

        private static StartingClass Brute()
        {
            return new StartingClass(
                "Brute",
                "A savage warrior who overwhelms foes with raw strength.\nStarts with heavy weapons and thick skin.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Clubs, 5f)
                }
            );
        }

        private static StartingClass Carpenter()
        {
            return new StartingClass(
                "Carpenter",
                "A master builder and woodworker.\nStarts with building tools, wood, and axe proficiency.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.WoodCutting, 10f),
                    new SkillBonus(Skills.SkillType.Axes, 5f)
                }
            );
        }

        private static StartingClass Explorer()
        {
            return new StartingClass(
                "Explorer",
                "A fearless wanderer of uncharted lands.\nStarts with travel gear and swift legs.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Run, 5f)
                }
            );
        }

        private static StartingClass Farmer()
        {
            return new StartingClass(
                "Farmer",
                "A skilled cultivator of the land.\nStarts with farming tools, seeds, and knowledge of the soil.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Blocking, 5f),
                    new SkillBonus(Skills.SkillType.Run, 5f)
                }
            );
        }

        private static StartingClass Fisher()
        {
            return new StartingClass(
                "Fisher",
                "A patient angler and friend of the sea.\nStarts with fishing gear, bait, and enhanced swimming ability.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Swim, 10f),
                    new SkillBonus(Skills.SkillType.Knives, 5f)
                }
            );
        }

        private static StartingClass Forager()
        {
            return new StartingClass(
                "Forager",
                "A wanderer who lives off the land.\nStarts with gathered provisions and knowledge of the wilds.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Run, 10f),
                    new SkillBonus(Skills.SkillType.Sneak, 5f)
                }
            );
        }

        private static StartingClass Healer()
        {
            return new StartingClass(
                "Healer",
                "A devoted mender who restores vitality to the wounded.\nStarts with healing supplies and protective magic.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.BloodMagic, 5f)
                }
            );
        }

        private static StartingClass Hunter()
        {
            return new StartingClass(
                "Hunter",
                "A keen-eyed stalker of prey.\nStarts with a bow, arrows, and the instincts of a predator.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Bows, 10f),
                    new SkillBonus(Skills.SkillType.Sneak, 5f)
                }
            );
        }

        private static StartingClass Lumberjack()
        {
            return new StartingClass(
                "Lumberjack",
                "A rugged woodsman with a sharp axe.\nStarts with chopping tools and a sturdy defense.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Axes, 10f),
                    new SkillBonus(Skills.SkillType.WoodCutting, 5f)
                }
            );
        }

        private static StartingClass Miner()
        {
            return new StartingClass(
                "Miner",
                "A stalwart excavator of Midgard's depths.\nStarts with mining tools, ore, and defensive skills.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Pickaxes, 10f),
                    new SkillBonus(Skills.SkillType.Blocking, 5f)
                }
            );
        }

        private static StartingClass Tank()
        {
            return new StartingClass(
                "Tank",
                "An immovable bulwark who shields allies from harm.\nStarts with heavy armor and a sturdy shield.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Blocking, 5f)
                }
            );
        }

        private static StartingClass Witch()
        {
            return new StartingClass(
                "Witch",
                "A practitioner of dark arts and potent brews.\nStarts with ritual components and cursed knowledge.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.BloodMagic, 5f)
                }
            );
        }

        private static StartingClass Wizard()
        {
            return new StartingClass(
                "Wizard",
                "A wielder of arcane knowledge and elemental fury.\nStarts with magical implements and ancient scrolls.",
                "Hammer",
                PlaceholderItems(),
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.ElementalMagic, 5f)
                }
            );
        }
    }
}
