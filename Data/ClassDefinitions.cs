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

        // Ability type tags — unlocked
        private const string Passive = "<color=#8AE58A>(Passive)</color>";
        private static string AbilityName(string name) => $"<color=#D4A24E>{name}</color>";

        // Ability type tags — locked (greyed out)
        private const string LockedAbility = "<color=#999999>(Activated Ability \u2014 Locked)</color>";
        private const string LockedCondPassive = "<color=#999999>(Conditional Passive \u2014 Locked)</color>";
        private static string LockedAbilityName(string name) => $"<color=#999999>{name}</color>";
        private static string LockedDesc(string desc) => $"<color=#999999>{desc}</color>";

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
                Witch(),
                Wizard()
            };
        }

        private static StartingClass Archer()
        {
            return new StartingClass(
                "Archer",
                $"{PastLife}\nIn life you loosed arrows until your fingers bled, and even Odin's ravens learned to fear your aim.\n\n{Purpose}\nYour steady hand and sharp eye carry over into death. Nock, draw, and let the tenth world know your name.\n\n{AbilityName("Eagle Eye")}  {Passive}\nArrows deal +15% bonus damage at maximum range. The bonus scales with your Bow skill and rewards patient, long-range shots over panic fire.\n\n{LockedAbilityName("Steady Aim")}  {LockedAbility}\n{LockedDesc("+20% draw speed and -10% stamina cost for bows. Your practiced form lets you loose arrows faster and with less effort.")}\n\n{LockedAbilityName("Piercing Shot")}  {LockedAbility}\n{LockedDesc("Arrows penetrate +1 target, dealing 60% damage to the second enemy hit. Rewards careful positioning and lined-up shots.")}\n\n{LockedAbilityName("Rain of Arrows")}  {LockedAbility}\n{LockedDesc("Fire a volley of arrows into the sky that rain down on a targeted area, striking all enemies within. Consumes multiple arrows per use.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("HelmetLeather"),
                    new StartingItem("CapeDeerHide"),
                    new StartingItem("BowFineWood"),
                    new StartingItem("ArrowWood", 20)
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Bows, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide", "BowFineWood" },
                iconPrefab: "BowFineWood",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Eagle Eye", "Arrows deal +15% bonus damage at maximum range. The bonus scales with your Bow skill and rewards patient, long-range shots over panic fire.", true, 0),
                    new ClassAbility("Steady Aim", "+20% draw speed and -10% stamina cost for bows. Your practiced form lets you loose arrows faster and with less effort.", false, 10),
                    new ClassAbility("Piercing Shot", "Arrows penetrate +1 target, dealing 60% damage to the second enemy hit. Rewards careful positioning and lined-up shots.", false, 25),
                    new ClassAbility("Rain of Arrows", "Fire a volley of arrows into the sky that rain down on a targeted area, striking all enemies within. Consumes multiple arrows per use.", false, 50)
                }
            );
        }

        private static StartingClass Assassin()
        {
            return new StartingClass(
                "Assassin",
                $"{PastLife}\nYou were the blade that struck between heartbeats, the shadow that slipped past locked doors and sleeping guards.\n\n{Purpose}\nValheim's creatures have never known a killer so quiet. Move unseen, strike once, and vanish.\n\n{AbilityName("Death from Behind")}  {Passive}\nAttacks that strike an enemy from behind deal +25% backstab damage and stagger. Bonus scales with Sneak skill and only applies while the enemy is not facing you.\n\n{LockedAbilityName("Shadow Step")}  {LockedAbility}\n{LockedDesc("+15% movement speed while sneaking and -20% sneak stamina cost. You glide through the shadows like a whisper.")}\n\n{LockedAbilityName("Poison Blade")}  {LockedAbility}\n{LockedDesc("Backstab attacks apply poison for 5s dealing 30% weapon damage per tick. Your blade carries a venomous edge.")}\n\n{LockedAbilityName("Marked by Fate")}  {LockedAbility}\n{LockedDesc("Mark up to three enemies at once. Marked enemies are highlighted and visible on the minimap. The mark is removed when the enemy dies or when the duration ends.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorTrollLeatherChest"),
                    new StartingItem("ArmorTrollLeatherLegs"),
                    new StartingItem("HelmetTrollLeather"),
                    new StartingItem("CapeTrollHide")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Sneak, 10f),
                    new SkillBonus(Skills.SkillType.Knives, 5f)
                },
                new List<string> { "ArmorTrollLeatherChest", "ArmorTrollLeatherLegs", "HelmetTrollLeather", "CapeTrollHide" },
                iconPrefab: "ArmorTrollLeatherChest",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Death from Behind", "Attacks that strike an enemy from behind deal +25% backstab damage and stagger. Bonus scales with Sneak skill and only applies while the enemy is not facing you.", true, 0),
                    new ClassAbility("Shadow Step", "+15% movement speed while sneaking and -20% sneak stamina cost. You glide through the shadows like a whisper.", false, 10),
                    new ClassAbility("Poison Blade", "Backstab attacks apply poison for 5s dealing 30% weapon damage per tick. Your blade carries a venomous edge.", false, 25),
                    new ClassAbility("Marked by Fate", "Mark up to three enemies at once. Marked enemies are highlighted and visible on the minimap. The mark is removed when the enemy dies or when the duration ends.", false, 50)
                }
            );
        }

        private static StartingClass Blacksmith()
        {
            return new StartingClass(
                "Blacksmith",
                $"{PastLife}\nThe ring of hammer on anvil was your hymn, and molten metal ran through your hands like water through a stream.\n\n{Purpose}\nOdin sends you to the tenth world with the memory of the forge still hot in your palms. Shape this land's ore into something worthy.\n\n{AbilityName("Tempered Edge")}  {Passive}\nWeapons and armor you craft have +15% increased durability. Your hands remember the forge even in death.\n\n{LockedAbilityName("Efficient Smelting")}  {LockedAbility}\n{LockedDesc("Smelters process 25% faster and yield +10% ore output. The forge burns hotter at your command.")}\n\n{LockedAbilityName("Masterwork")}  {LockedAbility}\n{LockedDesc("15% chance that crafted items gain +1 quality level. Your masterful technique sometimes produces exceptional results.")}\n\n{LockedAbilityName("Slag Surge")}  {LockedAbility}\n{LockedDesc("Superheat your equipped weapon, causing the next three strikes to deal bonus fire damage and ignite enemies on hit.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorBronzeChest"),
                    new StartingItem("ArmorBronzeLegs"),
                    new StartingItem("HelmetBronze"),
                    new StartingItem("SledgeStagbreaker")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Pickaxes, 5f)
                },
                new List<string> { "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze", "SledgeStagbreaker" },
                iconPrefab: "SledgeStagbreaker",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Tempered Edge", "Weapons and armor you craft have +15% increased durability. Your hands remember the forge even in death.", true, 0),
                    new ClassAbility("Efficient Smelting", "Smelters process 25% faster and yield +10% ore output. The forge burns hotter at your command.", false, 10),
                    new ClassAbility("Masterwork", "15% chance that crafted items gain +1 quality level. Your masterful technique sometimes produces exceptional results.", false, 25),
                    new ClassAbility("Slag Surge", "Superheat your equipped weapon, causing the next three strikes to deal bonus fire damage and ignite enemies on hit.", false, 50)
                }
            );
        }

        private static StartingClass Brute()
        {
            return new StartingClass(
                "Brute",
                $"{PastLife}\nWords were never your weapon. You spoke with fists, with clubs, with anything heavy enough to break bone and end arguments.\n\n{Purpose}\nIn Valheim, the strong devour the weak. Swing hard, swing first, and let the ravens sort out what's left.\n\n{AbilityName("Bone Breaker")}  {Passive}\nHeavy attacks with clubs and maces deal +20% stagger damage. Enemies staggered by your blows take longer to recover.\n\n{LockedAbilityName("Thick Skin")}  {LockedAbility}\n{LockedDesc("+10% physical damage resistance and +15 max health. Your body hardens against the blows of lesser foes.")}\n\n{LockedAbilityName("Ground Slam")}  {LockedAbility}\n{LockedDesc("Heavy attacks create a 3m shockwave dealing 40% weapon damage to nearby enemies. The earth trembles at your might.")}\n\n{LockedAbilityName("Berserker Rush")}  {LockedAbility}\n{LockedDesc("Enter a frenzy that increases attack speed and movement speed for a short duration. Taking damage during the frenzy extends it.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorIronChest"),
                    new StartingItem("ArmorIronLegs"),
                    new StartingItem("HelmetIron"),
                    new StartingItem("MaceBronze")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Clubs, 5f)
                },
                new List<string> { "ArmorIronChest", "ArmorIronLegs", "HelmetIron", "MaceBronze" },
                iconPrefab: "MaceBronze",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Bone Breaker", "Heavy attacks with clubs and maces deal +20% stagger damage. Enemies staggered by your blows take longer to recover.", true, 0),
                    new ClassAbility("Thick Skin", "+10% physical damage resistance and +15 max health. Your body hardens against the blows of lesser foes.", false, 10),
                    new ClassAbility("Ground Slam", "Heavy attacks create a 3m shockwave dealing 40% weapon damage to nearby enemies. The earth trembles at your might.", false, 25),
                    new ClassAbility("Berserker Rush", "Enter a frenzy that increases attack speed and movement speed for a short duration. Taking damage during the frenzy extends it.", false, 50)
                }
            );
        }

        private static StartingClass Carpenter()
        {
            return new StartingClass(
                "Carpenter",
                $"{PastLife}\nYou raised longhouses that stood for generations and built ships that crossed the northern seas without leaking a single drop.\n\n{Purpose}\nThe tenth world is nothing but wilderness. Good. You see timber where others see trees, and walls where others see only wind.\n\n{AbilityName("Structural Mastery")}  {Passive}\nBuilding pieces you place have +20% structural integrity and stability. Structures resist weather damage longer.\n\n{LockedAbilityName("Efficient Builder")}  {LockedAbility}\n{LockedDesc("-25% wood cost for building and +15% build radius. Your practiced eye wastes nothing and reaches further.")}\n\n{LockedAbilityName("Reinforced Walls")}  {LockedAbility}\n{LockedDesc("Structures take 30% less damage from enemies and weather. Your constructions stand firm against all assaults.")}\n\n{LockedAbilityName("Rapid Assembly")}  {LockedAbility}\n{LockedDesc("For a short duration, building costs are halved and placement speed is doubled. Your hands move with practiced precision.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("HelmetLeather"),
                    new StartingItem("AxeBronze")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.WoodCutting, 10f),
                    new SkillBonus(Skills.SkillType.Axes, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "AxeBronze" },
                iconPrefab: "Hammer",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Structural Mastery", "Building pieces you place have +20% structural integrity and stability. Structures resist weather damage longer.", true, 0),
                    new ClassAbility("Efficient Builder", "-25% wood cost for building and +15% build radius. Your practiced eye wastes nothing and reaches further.", false, 10),
                    new ClassAbility("Reinforced Walls", "Structures take 30% less damage from enemies and weather. Your constructions stand firm against all assaults.", false, 25),
                    new ClassAbility("Rapid Assembly", "For a short duration, building costs are halved and placement speed is doubled. Your hands move with practiced precision.", false, 50)
                }
            );
        }

        private static StartingClass Explorer()
        {
            return new StartingClass(
                "Explorer",
                $"{PastLife}\nNo map was ever large enough, no horizon ever final. You chased the edge of the world in life and found only more world beyond it.\n\n{Purpose}\nOdin has granted you the ultimate frontier. Every shore is unknown, every mountain unnamed. Run far, and claim it all.\n\n{AbilityName("Trailblazer")}  {Passive}\nYou move +10% faster and reveal a +25% wider area on the map as you travel. Stamina drains slower while running on roads.\n\n{LockedAbilityName("Light Feet")}  {LockedAbility}\n{LockedDesc("-20% stamina drain while running and +15% jump height. Your footfalls are swift and sure.")}\n\n{LockedAbilityName("Pathfinder")}  {LockedAbility}\n{LockedDesc("+30% movement speed on roads and no speed penalty in water. You find the fastest route through any terrain.")}\n\n{LockedAbilityName("Waystone")}  {LockedAbility}\n{LockedDesc("Place an invisible waypoint at your current location. Activate again to see its direction and distance on your HUD. Only one waypoint active at a time.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("CapeLox"),
                    new StartingItem("SpearBronze")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Run, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "CapeLox", "SpearBronze" },
                iconPrefab: "CapeLox",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Trailblazer", "You move +10% faster and reveal a +25% wider area on the map as you travel. Stamina drains slower while running on roads.", true, 0),
                    new ClassAbility("Light Feet", "-20% stamina drain while running and +15% jump height. Your footfalls are swift and sure.", false, 10),
                    new ClassAbility("Pathfinder", "+30% movement speed on roads and no speed penalty in water. You find the fastest route through any terrain.", false, 25),
                    new ClassAbility("Waystone", "Place an invisible waypoint at your current location. Activate again to see its direction and distance on your HUD. Only one waypoint active at a time.", false, 50)
                }
            );
        }

        private static StartingClass Farmer()
        {
            return new StartingClass(
                "Farmer",
                $"{PastLife}\nWhile warriors chased glory, you fed them. Your hands knew the soil better than any sword, and your harvests kept entire villages alive through the harshest winters.\n\n{Purpose}\nValheim's earth is untamed but fertile. Plant deep, grow strong, and prove that the hand that feeds is mightier than the hand that kills.\n\n{AbilityName("Green Thumb")}  {Passive}\nCrops you plant grow +20% faster and yield +10% bonus harvests. Tamed animals near your cultivated land breed more frequently.\n\n{LockedAbilityName("Animal Kinship")}  {LockedAbility}\n{LockedDesc("Tamed animals breed 25% faster and taming speed is increased by +15%. Your gentle hand calms even the wildest beasts.")}\n\n{LockedAbilityName("Fertile Ground")}  {LockedAbility}\n{LockedDesc("Crops in a 10m radius have a 30% chance to drop double harvest. The soil blesses those who tend it with care.")}\n\n{LockedAbilityName("Bountiful Harvest")}  {LockedAbility}\n{LockedDesc("Instantly ripen all crops within a large radius around you. Can only be used once per in-game day.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorRagsChest"),
                    new StartingItem("ArmorRagsLegs"),
                    new StartingItem("ShieldWood"),
                    new StartingItem("Cultivator")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Blocking, 5f),
                    new SkillBonus(Skills.SkillType.Run, 5f)
                },
                new List<string> { "ArmorRagsChest", "ArmorRagsLegs", "ShieldWood" },
                iconPrefab: "Cultivator",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Green Thumb", "Crops you plant grow +20% faster and yield +10% bonus harvests. Tamed animals near your cultivated land breed more frequently.", true, 0),
                    new ClassAbility("Animal Kinship", "Tamed animals breed 25% faster and taming speed is increased by +15%. Your gentle hand calms even the wildest beasts.", false, 10),
                    new ClassAbility("Fertile Ground", "Crops in a 10m radius have a 30% chance to drop double harvest. The soil blesses those who tend it with care.", false, 25),
                    new ClassAbility("Bountiful Harvest", "Instantly ripen all crops within a large radius around you. Can only be used once per in-game day.", false, 50)
                }
            );
        }

        private static StartingClass Fisher()
        {
            return new StartingClass(
                "Fisher",
                $"{PastLife}\nThe sea was your hall, the rod your scepter. You pulled life from depths that swallowed lesser folk whole and never once feared the tide.\n\n{Purpose}\nValheim's waters teem with creatures unknown to Midgard. Cast your line and see what bites.\n\n{AbilityName("Deep Sense")}  {Passive}\nFish bite +20% faster on your line and your swimming speed is increased by +15%. Stamina drains slower in water.\n\n{LockedAbilityName("Strong Line")}  {LockedAbility}\n{LockedDesc("-25% stamina drain while fishing and +15% reel speed. Your line holds firm against even the strongest catch.")}\n\n{LockedAbilityName("Lucky Catch")}  {LockedAbility}\n{LockedDesc("20% chance to catch rare fish and +30% fish size. Fortune favors the patient angler.")}\n\n{LockedAbilityName("Tidal Lure")}  {LockedAbility}\n{LockedDesc("Cast a special lure that attracts all nearby fish to one spot for a short duration. Works in any body of water.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("KnifeFlint"),
                    new StartingItem("FishingRod"),
                    new StartingItem("FishingBait", 20)
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Swim, 10f),
                    new SkillBonus(Skills.SkillType.Knives, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "KnifeFlint" },
                iconPrefab: "FishingRod",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Deep Sense", "Fish bite +20% faster on your line and your swimming speed is increased by +15%. Stamina drains slower in water.", true, 0),
                    new ClassAbility("Strong Line", "-25% stamina drain while fishing and +15% reel speed. Your line holds firm against even the strongest catch.", false, 10),
                    new ClassAbility("Lucky Catch", "20% chance to catch rare fish and +30% fish size. Fortune favors the patient angler.", false, 25),
                    new ClassAbility("Tidal Lure", "Cast a special lure that attracts all nearby fish to one spot for a short duration. Works in any body of water.", false, 50)
                }
            );
        }

        private static StartingClass Forager()
        {
            return new StartingClass(
                "Forager",
                $"{PastLife}\nYou never needed a farm or a market stall. The forest provided mushrooms, the meadows gave berries, and you knew which roots healed and which ones killed.\n\n{Purpose}\nThe wilds of Valheim are generous to those who know where to look. Tread lightly and you will never go hungry.\n\n{AbilityName("Nature's Bounty")}  {Passive}\nPickable resources like berries, mushrooms, and thistle respawn +15% faster in areas you have visited. You spot pickables from further away with a faint glow.\n\n{LockedAbilityName("Keen Eyes")}  {LockedAbility}\n{LockedDesc("Pickables glow from 20m away and foraging radius is increased by +10%. Nothing escapes your trained gaze.")}\n\n{LockedAbilityName("Herbalist")}  {LockedAbility}\n{LockedDesc("+25% potion effectiveness and food lasts 15% longer. Your knowledge of herbs enhances every concoction.")}\n\n{LockedAbilityName("Keen Nose")}  {LockedAbility}\n{LockedDesc("Briefly highlight all nearby pickable resources, beehives, and hidden vegvisir through terrain and obstacles.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("CapeDeerHide")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Run, 10f),
                    new SkillBonus(Skills.SkillType.Sneak, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "CapeDeerHide" },
                iconPrefab: "Mushroom",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Nature's Bounty", "Pickable resources like berries, mushrooms, and thistle respawn +15% faster in areas you have visited. You spot pickables from further away with a faint glow.", true, 0),
                    new ClassAbility("Keen Eyes", "Pickables glow from 20m away and foraging radius is increased by +10%. Nothing escapes your trained gaze.", false, 10),
                    new ClassAbility("Herbalist", "+25% potion effectiveness and food lasts 15% longer. Your knowledge of herbs enhances every concoction.", false, 25),
                    new ClassAbility("Keen Nose", "Briefly highlight all nearby pickable resources, beehives, and hidden vegvisir through terrain and obstacles.", false, 50)
                }
            );
        }

        private static StartingClass Healer()
        {
            return new StartingClass(
                "Healer",
                $"{PastLife}\nWhere others brought death, you brought them back from its door. Your salves closed wounds that should have been graves, and your prayers held the dying to this side of Hel's gate.\n\n{Purpose}\nIn Valheim, death is cheap but survival is precious. Mend what breaks, and you will never stand alone.\n\n{AbilityName("Mending Aura")}  {Passive}\nNearby allies within 10m slowly regenerate +1 HP/s. The effect is stronger when you are resting or standing still.\n\n{LockedAbilityName("Soothing Presence")}  {LockedAbility}\n{LockedDesc("+20% health regen while resting and +15% food healing. Your calming presence mends body and spirit.")}\n\n{LockedAbilityName("Life Bond")}  {LockedAbility}\n{LockedDesc("Heal nearby allies for 25% of healing you receive. Your vitality flows outward to those who fight beside you.")}\n\n{LockedAbilityName("Purge")}  {LockedAbility}\n{LockedDesc("Cleanse yourself and nearby allies of all negative status effects including poison, frost, and wet. Short cooldown.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorRootChest"),
                    new StartingItem("ArmorRootLegs"),
                    new StartingItem("HelmetRoot"),
                    new StartingItem("CapeFeather")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.BloodMagic, 5f)
                },
                new List<string> { "ArmorRootChest", "ArmorRootLegs", "HelmetRoot", "CapeFeather" },
                iconPrefab: "MeadHealthMedium",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Mending Aura", "Nearby allies within 10m slowly regenerate +1 HP/s. The effect is stronger when you are resting or standing still.", true, 0),
                    new ClassAbility("Soothing Presence", "+20% health regen while resting and +15% food healing. Your calming presence mends body and spirit.", false, 10),
                    new ClassAbility("Life Bond", "Heal nearby allies for 25% of healing you receive. Your vitality flows outward to those who fight beside you.", false, 25),
                    new ClassAbility("Purge", "Cleanse yourself and nearby allies of all negative status effects including poison, frost, and wet. Short cooldown.", false, 50)
                }
            );
        }

        private static StartingClass Hunter()
        {
            return new StartingClass(
                "Hunter",
                $"{PastLife}\nYou tracked elk through blizzards and brought down boar in pitch darkness, reading the land like others read runes.\n\n{Purpose}\nValheim's beasts are fiercer than anything in Midgard, but a hunter's patience is the same in any world. Stalk, strike, survive.\n\n{AbilityName("Predator's Mark")}  {Passive}\nCreatures you damage leave bloody footprints visible only to you. Marked creatures take +10% more damage from your subsequent attacks.\n\n{LockedAbilityName("Tracker")}  {LockedAbility}\n{LockedDesc("Bloody tracks last 50% longer and sneak damage to animals is increased by +15%. The prey never escapes your watchful eye.")}\n\n{LockedAbilityName("Vital Strike")}  {LockedAbility}\n{LockedDesc("20% chance for critical hits dealing 150% damage against creatures. You know exactly where to strike for maximum effect.")}\n\n{LockedAbilityName("Feral Senses")}  {LockedAbility}\n{LockedDesc("Briefly reveal all nearby creatures through terrain, showing their silhouettes and highlighting aggressive ones in red.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("HelmetLeather"),
                    new StartingItem("CapeDeerHide"),
                    new StartingItem("BowHuntsman"),
                    new StartingItem("ArrowWood", 20)
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Bows, 10f),
                    new SkillBonus(Skills.SkillType.Sneak, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide", "BowHuntsman" },
                iconPrefab: "DeerHide",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Predator's Mark", "Creatures you damage leave bloody footprints visible only to you. Marked creatures take +10% more damage from your subsequent attacks.", true, 0),
                    new ClassAbility("Tracker", "Bloody tracks last 50% longer and sneak damage to animals is increased by +15%. The prey never escapes your watchful eye.", false, 10),
                    new ClassAbility("Vital Strike", "20% chance for critical hits dealing 150% damage against creatures. You know exactly where to strike for maximum effect.", false, 25),
                    new ClassAbility("Feral Senses", "Briefly reveal all nearby creatures through terrain, showing their silhouettes and highlighting aggressive ones in red.", false, 50)
                }
            );
        }

        private static StartingClass Lumberjack()
        {
            return new StartingClass(
                "Lumberjack",
                $"{PastLife}\nAncient oaks fell to your axe like grass to a scythe. You felled forests that men called impassable and turned timberland into lumber before the sun set.\n\n{Purpose}\nValheim's trees grow thick and tall. Grip your axe and show them what you showed every forest before.\n\n{AbilityName("Timber!")}  {Passive}\nTrees you chop drop +15% bonus wood and your axe swings cost -10% less stamina. A lumberjack's efficiency carries into the afterlife.\n\n{LockedAbilityName("Hardened Grip")}  {LockedAbility}\n{LockedDesc("+20% axe damage to trees and -15% axe durability loss. Your grip never falters, your edge never dulls.")}\n\n{LockedAbilityName("Chain Fell")}  {LockedAbility}\n{LockedDesc("30% chance felled trees topple neighbors, dealing 50% damage. The forest falls like dominoes before you.")}\n\n{LockedAbilityName("Cleaving Blow")}  {LockedAbility}\n{LockedDesc("Deliver a mighty overhead axe strike that deals massive damage in a wide arc. Can fell multiple trees in a single swing.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorLeatherChest"),
                    new StartingItem("ArmorLeatherLegs"),
                    new StartingItem("AxeIron")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Axes, 10f),
                    new SkillBonus(Skills.SkillType.WoodCutting, 5f)
                },
                new List<string> { "ArmorLeatherChest", "ArmorLeatherLegs", "AxeIron" },
                iconPrefab: "AxeIron",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Timber!", "Trees you chop drop +15% bonus wood and your axe swings cost -10% less stamina. A lumberjack's efficiency carries into the afterlife.", true, 0),
                    new ClassAbility("Hardened Grip", "+20% axe damage to trees and -15% axe durability loss. Your grip never falters, your edge never dulls.", false, 10),
                    new ClassAbility("Chain Fell", "30% chance felled trees topple neighbors, dealing 50% damage. The forest falls like dominoes before you.", false, 25),
                    new ClassAbility("Cleaving Blow", "Deliver a mighty overhead axe strike that deals massive damage in a wide arc. Can fell multiple trees in a single swing.", false, 50)
                }
            );
        }

        private static StartingClass Miner()
        {
            return new StartingClass(
                "Miner",
                $"{PastLife}\nYou dug where others feared to tread, hauling copper and tin from veins buried deep beneath the mountain's heart.\n\n{Purpose}\nThe tenth world hides its treasures under stone and swamp. Swing your pick and crack it open. The earth always rewards those stubborn enough to keep digging.\n\n{AbilityName("Vein Sense")}  {Passive}\nOre deposits and silver veins shimmer within 15m, visible only to you. Your pickaxe swings cost -10% less stamina against rock and ore.\n\n{LockedAbilityName("Deep Strike")}  {LockedAbility}\n{LockedDesc("+20% pickaxe damage to ore and -15% durability loss. Each swing bites deeper into the stone.")}\n\n{LockedAbilityName("Mother Lode")}  {LockedAbility}\n{LockedDesc("25% chance for double ore drops and +10% gem find chance. The mountain yields its riches eagerly to you.")}\n\n{LockedAbilityName("Shatter Strike")}  {LockedAbility}\n{LockedDesc("Strike the ground with your pickaxe, sending a shockwave that cracks nearby rock and ore nodes, dealing partial damage to all of them at once.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorBronzeChest"),
                    new StartingItem("ArmorBronzeLegs"),
                    new StartingItem("HelmetBronze"),
                    new StartingItem("PickaxeIron")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.Pickaxes, 10f),
                    new SkillBonus(Skills.SkillType.Blocking, 5f)
                },
                new List<string> { "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze", "PickaxeIron" },
                iconPrefab: "IronOre",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Vein Sense", "Ore deposits and silver veins shimmer within 15m, visible only to you. Your pickaxe swings cost -10% less stamina against rock and ore.", true, 0),
                    new ClassAbility("Deep Strike", "+20% pickaxe damage to ore and -15% durability loss. Each swing bites deeper into the stone.", false, 10),
                    new ClassAbility("Mother Lode", "25% chance for double ore drops and +10% gem find chance. The mountain yields its riches eagerly to you.", false, 25),
                    new ClassAbility("Shatter Strike", "Strike the ground with your pickaxe, sending a shockwave that cracks nearby rock and ore nodes, dealing partial damage to all of them at once.", false, 50)
                }
            );
        }

        private static StartingClass Witch()
        {
            return new StartingClass(
                "Witch",
                $"{PastLife}\nThey whispered your name at hearthside and left offerings at your door. Your brews could curse a jarl's bloodline or save a dying child, depending on your mood.\n\n{Purpose}\nValheim pulses with dark energy that Midgard only dreamed of. Taste it, shape it, and make this world fear the old ways.\n\n{AbilityName("Hex Brewer")}  {Passive}\nMeads and potions you craft last +15% longer and deal +10% poison damage. Enemies you poison take damage for an extended duration.\n\n{LockedAbilityName("Dark Affinity")}  {LockedAbility}\n{LockedDesc("-20% eitr cost for blood magic and +15% staff damage. The dark arts flow through you with unnatural ease.")}\n\n{LockedAbilityName("Lingering Curse")}  {LockedAbility}\n{LockedDesc("Poison effects last 30% longer and have a 20% chance to spread to nearby enemies on kill. Your hexes outlive their victims.")}\n\n{LockedAbilityName("Curse of Frailty")}  {LockedAbility}\n{LockedDesc("Hex a target enemy, reducing its damage resistance and attack speed for a duration. The curse spreads to nearby enemies if the target dies while hexed.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorFenringChest"),
                    new StartingItem("ArmorFenringLegs"),
                    new StartingItem("HelmetFenring"),
                    new StartingItem("CapeWolf"),
                    new StartingItem("StaffSkeleton")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.BloodMagic, 5f)
                },
                new List<string> { "ArmorFenringChest", "ArmorFenringLegs", "HelmetFenring", "CapeWolf" },
                iconPrefab: "StaffSkeleton",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Hex Brewer", "Meads and potions you craft last +15% longer and deal +10% poison damage. Enemies you poison take damage for an extended duration.", true, 0),
                    new ClassAbility("Dark Affinity", "-20% eitr cost for blood magic and +15% staff damage. The dark arts flow through you with unnatural ease.", false, 10),
                    new ClassAbility("Lingering Curse", "Poison effects last 30% longer and have a 20% chance to spread to nearby enemies on kill. Your hexes outlive their victims.", false, 25),
                    new ClassAbility("Curse of Frailty", "Hex a target enemy, reducing its damage resistance and attack speed for a duration. The curse spreads to nearby enemies if the target dies while hexed.", false, 50)
                }
            );
        }

        private static StartingClass Wizard()
        {
            return new StartingClass(
                "Wizard",
                $"{PastLife}\nYou read the runes that others feared to speak aloud and bent the elements to your will with nothing but words and will.\n\n{Purpose}\nThe tenth world crackles with raw seidr. Channel it through staff and stone, and remind every creature here that magic answers to no master but you.\n\n{AbilityName("Arcane Reservoir")}  {Passive}\nYour maximum eitr pool is increased by +15% and staff attacks consume -10% less eitr. Eitr regenerates faster while standing still.\n\n{LockedAbilityName("Mana Flow")}  {LockedAbility}\n{LockedDesc("+25% eitr regeneration and -15% spell cooldown. Arcane energy surges through you like a river.")}\n\n{LockedAbilityName("Elemental Mastery")}  {LockedAbility}\n{LockedDesc("+20% elemental damage and spells have 15% larger AoE. The elements bend eagerly to your practiced will.")}\n\n{LockedAbilityName("Elemental Surge")}  {LockedAbility}\n{LockedDesc("Overcharge your next staff attack, doubling its damage and area of effect. Requires a full eitr bar to activate.")}",
                new List<StartingItem>
                {
                    new StartingItem("Hammer"),
                    new StartingItem("ArmorMageChest"),
                    new StartingItem("ArmorMageLegs"),
                    new StartingItem("HelmetMage"),
                    new StartingItem("CapeFeather"),
                    new StartingItem("StaffFireballs")
                },
                new List<SkillBonus>
                {
                    new SkillBonus(Skills.SkillType.ElementalMagic, 5f)
                },
                new List<string> { "ArmorMageChest", "ArmorMageLegs", "HelmetMage", "CapeFeather", "StaffFireballs" },
                iconPrefab: "StaffFireballs",
                abilities: new List<ClassAbility>
                {
                    new ClassAbility("Arcane Reservoir", "Your maximum eitr pool is increased by +15% and staff attacks consume -10% less eitr. Eitr regenerates faster while standing still.", true, 0),
                    new ClassAbility("Mana Flow", "+25% eitr regeneration and -15% spell cooldown. Arcane energy surges through you like a river.", false, 10),
                    new ClassAbility("Elemental Mastery", "+20% elemental damage and spells have 15% larger AoE. The elements bend eagerly to your practiced will.", false, 25),
                    new ClassAbility("Elemental Surge", "Overcharge your next staff attack, doubling its damage and area of effect. Requires a full eitr bar to activate.", false, 50)
                }
            );
        }
    }
}
