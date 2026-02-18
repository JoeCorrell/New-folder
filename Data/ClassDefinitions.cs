using System.Collections.Generic;

namespace StartingClassMod
{
    /// <summary>
    /// Defines all available starting classes with their items and skill bonuses.
    /// All items are vanilla Valheim prefabs. Skill bonuses are small (5-10 levels).
    /// </summary>
    public static class ClassDefinitions
    {
        // Rich text section headers for class descriptions
        private const string PastLife = "<color=#D4A24E>Past Life</color>";
        private const string Purpose = "<color=#66B3E5>Odin's Purpose</color>";

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
                $"{PastLife}\nIn life you loosed arrows until your fingers bled, and even Odin's ravens learned to fear your aim.\n\n{Purpose}\nYour steady hand and sharp eye carry over into death. Nock, draw, and let the tenth world know your name.",
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
                $"{PastLife}\nYou were the blade that struck between heartbeats, the shadow that slipped past locked doors and sleeping guards.\n\n{Purpose}\nValheim's creatures have never known a killer so quiet. Move unseen, strike once, and vanish.",
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
                $"{PastLife}\nThe ring of hammer on anvil was your hymn, and molten metal ran through your hands like water through a stream.\n\n{Purpose}\nOdin sends you to the tenth world with the memory of the forge still hot in your palms. Shape this land's ore into something worthy.",
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
                $"{PastLife}\nWords were never your weapon. You spoke with fists, with clubs, with anything heavy enough to break bone and end arguments.\n\n{Purpose}\nIn Valheim, the strong devour the weak. Swing hard, swing first, and let the ravens sort out what's left.",
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
                $"{PastLife}\nYou raised longhouses that stood for generations and built ships that crossed the northern seas without leaking a single drop.\n\n{Purpose}\nThe tenth world is nothing but wilderness. Good. You see timber where others see trees, and walls where others see only wind.",
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
                $"{PastLife}\nNo map was ever large enough, no horizon ever final. You chased the edge of the world in life and found only more world beyond it.\n\n{Purpose}\nOdin has granted you the ultimate frontier. Every shore is unknown, every mountain unnamed. Run far, and claim it all.",
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
                $"{PastLife}\nWhile warriors chased glory, you fed them. Your hands knew the soil better than any sword, and your harvests kept entire villages alive through the harshest winters.\n\n{Purpose}\nValheim's earth is untamed but fertile. Plant deep, grow strong, and prove that the hand that feeds is mightier than the hand that kills.",
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
                $"{PastLife}\nThe sea was your hall, the rod your scepter. You pulled life from depths that swallowed lesser folk whole and never once feared the tide.\n\n{Purpose}\nValheim's waters teem with creatures unknown to Midgard. Cast your line and see what bites.",
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
                $"{PastLife}\nYou never needed a farm or a market stall. The forest provided mushrooms, the meadows gave berries, and you knew which roots healed and which ones killed.\n\n{Purpose}\nThe wilds of Valheim are generous to those who know where to look. Tread lightly and you will never go hungry.",
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
                $"{PastLife}\nWhere others brought death, you brought them back from its door. Your salves closed wounds that should have been graves, and your prayers held the dying to this side of Hel's gate.\n\n{Purpose}\nIn Valheim, death is cheap but survival is precious. Mend what breaks, and you will never stand alone.",
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
                $"{PastLife}\nYou tracked elk through blizzards and brought down boar in pitch darkness, reading the land like others read runes.\n\n{Purpose}\nValheim's beasts are fiercer than anything in Midgard, but a hunter's patience is the same in any world. Stalk, strike, survive.",
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
                $"{PastLife}\nAncient oaks fell to your axe like grass to a scythe. You felled forests that men called impassable and turned timberland into lumber before the sun set.\n\n{Purpose}\nValheim's trees grow thick and tall. Grip your axe and show them what you showed every forest before.",
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
                $"{PastLife}\nYou dug where others feared to tread, hauling copper and tin from veins buried deep beneath the mountain's heart.\n\n{Purpose}\nThe tenth world hides its treasures under stone and swamp. Swing your pick and crack it open. The earth always rewards those stubborn enough to keep digging.",
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
                $"{PastLife}\nYou stood in the shieldwall when others broke and ran. Every scar on your body was earned facing forward, and not one enemy ever saw your back.\n\n{Purpose}\nValheim's horrors hit hard, but you hit the ground harder. Raise your shield and let them come.",
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
                $"{PastLife}\nThey whispered your name at hearthside and left offerings at your door. Your brews could curse a jarl's bloodline or save a dying child, depending on your mood.\n\n{Purpose}\nValheim pulses with dark energy that Midgard only dreamed of. Taste it, shape it, and make this world fear the old ways.",
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
                $"{PastLife}\nYou read the runes that others feared to speak aloud and bent the elements to your will with nothing but words and will.\n\n{Purpose}\nThe tenth world crackles with raw seidr. Channel it through staff and stone, and remind every creature here that magic answers to no master but you.",
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
