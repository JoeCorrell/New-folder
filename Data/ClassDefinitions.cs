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

        // Ability type tags
        private const string Passive = "<color=#8AE58A>(Passive)</color>";
        private static string AbilityName(string name) => $"<color=#D4A24E>{name}</color>";

        // Locked ability styling (greyed out, no "Locked" label)
        private static string LockedAbilityName(string name) => $"<color=#999999>{name}</color>";
        private static string LockedDesc(string desc) => $"<color=#999999>{desc}</color>";

        public static List<StartingClass> GetAll()
        {
            return new List<StartingClass>
            {
                Archer(),
                Assassin(),
                Builder(),
                Explorer(),
                Farmer(),
                Healer(),
                Hunter(),
                Miner()
            };
        }

        private static StartingClass Archer()
        {
            return new StartingClass(
                "Archer",
                $"{PastLife}\nYou loosed arrows until your fingers bled and even Odin's ravens learned to fear your aim. Not a single shot that mattered ever missed its mark.\n\n{Purpose}\nYour steady hand and sharp eye carry over into death. Nock, draw, and let the tenth world know your name.\n\n{AbilityName("Eagle Eye")}  {Passive}\nArrows deal increased damage the further they travel. The bonus scales with your Bow skill and rewards patient, long-range shots over panic fire.\n\n{LockedAbilityName("Steady Aim")}\n{LockedDesc("Your draw speed is faster and stamina cost per shot is reduced, letting you loose arrows in rapid succession without exhausting yourself.")}",
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
                    new ClassAbility("Eagle Eye", "Your eyes read the wind and your fingers compensate before your mind even registers the gust. Arrows you loose deal increased bonus damage the further they travel before striking their target, rewarding patient marksmanship over close-range panic shots. The bonus scales naturally with your Bow skill, growing more potent as your muscle memory sharpens. At higher skill levels, even moving targets at extreme range become reliable kills.", true, 0),
                    new ClassAbility("Steady Aim", "Years of drawing heavy longbows have hardened the muscles in your arms and shoulders into something that barely registers fatigue. Your draw speed is noticeably faster, allowing you to loose arrows in rapid succession without sacrificing accuracy. The stamina cost of each draw is reduced, meaning you can sustain prolonged engagements where lesser archers would be gasping and shaking. This efficiency compounds over long fights, giving you a decisive edge in wars of attrition.", false, 10),
                    new ClassAbility("Piercing Shot", "You learned to forge arrowheads with a narrow, hardened profile designed to punch clean through flesh and bone. Your arrows penetrate their initial target and continue on to strike a second enemy behind them, dealing reduced but meaningful damage. This rewards careful positioning \u2014 lining up shots through clusters of enemies turns a single arrow into a devastating chain. Against tightly packed foes, one well-placed shot can turn the tide of an encounter.", false, 25),
                    new ClassAbility("Rain of Arrows", "The culmination of a lifetime's archery mastery \u2014 you fire a volley of arrows high into the sky in a calculated arc, raining death across a wide area below. Every enemy caught in the impact zone takes significant damage and is briefly staggered by the barrage. The ability consumes several arrows from your quiver with each use, but the devastation it wreaks on clustered enemies more than justifies the cost. Masters of this technique have been known to break entire raids single-handedly.", false, 50)
                }
            );
        }

        private static StartingClass Assassin()
        {
            return new StartingClass(
                "Assassin",
                $"{PastLife}\nYou were the blade that struck between heartbeats, the shadow that slipped past locked doors and sleeping guards. They found your victims at dawn and blamed the gods.\n\n{Purpose}\nValheim's creatures have never known a killer so quiet. Move unseen, strike once, and vanish.\n\n{AbilityName("Killing Edge")}  {Passive}\nBackstab attacks deal twenty percent increased damage, turning every ambush into a decisive strike.\n\n{LockedAbilityName("Marked by Fate")}\n{LockedDesc("Designate enemies with a hunter's mark, tracking their every movement through terrain and obstacles.")}",
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
                    new ClassAbility("Killing Edge", "Your instinct for finding weak points makes every ambush lethal. Backstab damage is increased by 20% (1.2x multiplier applied to the backstab bonus). You also start with +10 Sneak and +5 Knives from your past life as a blade in the dark.", true, 0),
                    new ClassAbility("Marked by Fate", "Press Z to place a hunter's mark on the enemy you're aiming at within 50m. Marked targets glow red and are tracked on your minimap, visible through terrain and darkness. You can maintain up to 3 marks at once. Press Z on a marked target to remove the mark and reclaim the charge. Marks automatically expire if the target moves beyond 100m. Use ALT to switch between abilities.", false, 10),
                    new ClassAbility("Shadow Step", "Your practiced footwork lets you move through stealth with unnatural speed. Sneak movement speed is increased by 25% and stamina drain while sneaking is reduced by 25%, allowing you to reposition for the perfect strike without exhausting yourself.", true, 20),
                    new ClassAbility("Nature's Shroud", "The wilderness becomes your greatest disguise. While crouched within 4m of bushes, shrubs, or undergrowth, your stealth factor is boosted by 80%, making you virtually invisible to nearby enemies even at close range.", true, 35),
                    new ClassAbility("Ghost Stride", "You run with a supernatural lightness that barely disturbs the air. Noise produced while running is reduced by 70%, letting you sprint at full speed without alerting enemies that would normally hear your approach from a distance.", true, 50),
                    new ClassAbility("Blade Dance", "Press Z to enter a state of deadly focus. For 15 seconds, all knife and dagger attacks deal double damage (2x multiplier). After the frenzy ends, you need 10 minutes to recover before activating it again. Use ALT to switch between abilities.", false, 70)
                }
            );
        }

        private static StartingClass Builder()
        {
            return new StartingClass(
                "Builder",
                $"{PastLife}\nYou raised longhouses that stood for generations and bridges that spanned rivers other builders declared impossible. Your hands knew timber and stone the way a musician knows strings.\n\n{Purpose}\nThe tenth world has not a single roof or wall. Where others see trees you see timber, where others see rocks you see foundations.\n\n{AbilityName("Structural Mastery")}  {Passive}\nBuilding pieces you place have increased structural integrity and stability. Your constructions resist weather damage far longer, reducing maintenance over time.\n\n{LockedAbilityName("Efficient Builder")}\n{LockedDesc("Wood and stone costs for building are reduced and your build radius is extended, letting you place pieces at greater distances without repositioning.")}",
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
                    new ClassAbility("Structural Mastery", "Your understanding of load distribution, material stress, and architectural principles is so deeply ingrained that it manifests in everything you build, as naturally as breathing. Building pieces you place have significantly increased structural integrity and stability, allowing you to build taller towers, wider spans, and more ambitious structures than other builders could attempt. Your constructions resist weather damage and decay far longer than standard builds, reducing maintenance and repair costs over time. This passive knowledge also means your structures handle wind, rain, and enemy assaults with a resilience that seems almost supernatural.", true, 0),
                    new ClassAbility("Efficient Builder", "Years of professional carpentry have taught you to waste nothing \u2014 every cut is precise, every joint is clean, and every scrap of material finds its purpose. The wood and stone costs of your building projects are noticeably reduced, stretching your gathered resources much further than they would go in lesser hands. Your build radius is also extended, allowing you to place pieces at greater distances without needing to reposition. This efficiency compounds dramatically over large building projects, saving enormous amounts of material across a full base construction.", false, 10),
                    new ClassAbility("Reinforced Walls", "Your intimate knowledge of structural weak points \u2014 and how to eliminate them \u2014 means that everything you build is naturally fortified against assault. Structures you place take significantly reduced damage from enemy attacks, weather degradation, and environmental hazards. Walls and floors resist troll clubs and deathsquito stings with equal stubbornness, buying precious time during raids and sieges. This defensive bonus applies to all building pieces you place, from simple wooden walls to stone castles, making your bases some of the most durable in all of Valheim.", false, 25),
                    new ClassAbility("Rapid Assembly", "When the situation demands speed over precision, you can enter a state of focused intensity where your hands move almost faster than the eye can follow. For a short duration, all building costs are halved and placement speed is dramatically increased, allowing you to throw up walls, roofs, and fortifications at an astonishing pace. This ability is invaluable during emergency situations \u2014 raising quick shelters during a storm, building defensive walls mid-raid, or completing time-sensitive projects before nightfall. The tradeoff is the intense focus required, limiting how often you can sustain this burst of construction.", false, 50)
                }
            );
        }

        private static StartingClass Explorer()
        {
            return new StartingClass(
                "Explorer",
                $"{PastLife}\nNo map was ever large enough and no horizon ever final. You chased the edge of the world in life and found only more world beyond it.\n\n{Purpose}\nOdin has granted you the ultimate frontier. Every shore is unknown, every mountain unnamed \u2014 run far and claim it all.\n\n{AbilityName("Trailblazer")}  {Passive}\nYou move faster and reveal a wider area on the map as you travel. Stamina drains slower while running on roads.\n\n{LockedAbilityName("Light Feet")}\n{LockedDesc("Stamina drain while running is reduced and jump height is increased. Your footfalls are swift and sure across any terrain.")}",
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
                    new ClassAbility("Trailblazer", "Your restless spirit pushes you onward faster than those content to stay in one place. You move at increased speed and reveal a wider area on the map as you travel, charting territory that would take others twice as long to uncover. Stamina drains slower while running on roads and established paths, rewarding you for finding and following the routes that connect the world. At higher Run skill levels, the speed bonus becomes substantial enough to outpace most hostile creatures.", true, 0),
                    new ClassAbility("Light Feet", "Years of crossing every kind of terrain \u2014 frozen peaks, boggy marshes, dense forest undergrowth \u2014 have taught your body to move with extraordinary efficiency. Stamina drain while running is noticeably reduced, allowing you to sprint for longer distances without stopping to catch your breath. Your jump height is also increased, letting you scale rocky terrain and clear obstacles that would force others to find a way around. Combined with your natural Trailblazer speed, this makes you the fastest traveler in all of Valheim.", false, 10),
                    new ClassAbility("Pathfinder", "You have an uncanny sense for the fastest route through any landscape, an instinct honed by a lifetime of navigating unfamiliar terrain. Movement speed on roads is dramatically increased, and the usual penalty for wading through water is eliminated entirely. This passive knowledge extends to reading the terrain ahead \u2014 you naturally avoid the soft ground, hidden roots, and loose stones that slow other travelers. In practical terms, a journey that takes others an entire day can be completed in half the time.", false, 25),
                    new ClassAbility("Waystone", "You place an invisible waypoint at your current location, anchoring a thread of awareness that stretches across any distance. Activating the ability again reveals the waypoint's direction and distance on your HUD, serving as an unerring compass back to your marked location. Only one waypoint can be active at a time \u2014 placing a new one replaces the old \u2014 but this limitation forces strategic thinking about which locations matter most. Invaluable for marking dungeon entrances, distant resource deposits, or the path home through unfamiliar territory.", false, 50)
                }
            );
        }

        private static StartingClass Farmer()
        {
            return new StartingClass(
                "Farmer",
                $"{PastLife}\nWhile warriors chased glory, you fed them. Your harvests kept entire villages alive through winters that buried the world in snow.\n\n{Purpose}\nValheim's earth is untamed but fertile. Plant deep, grow strong, and prove that the hand that feeds is mightier than the hand that kills.\n\n{AbilityName("Green Thumb")}  {Passive}\nCrops you plant grow faster and yield bonus harvests. Tamed animals near your cultivated land breed more frequently.\n\n{LockedAbilityName("Animal Kinship")}\n{LockedDesc("Tamed animals breed faster and the taming process is quicker. Wild creatures respond to your offerings with less fear, settling into domesticated life with ease.")}",
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
                    new ClassAbility("Green Thumb", "Your connection to growing things is so deeply rooted that it manifests as a tangible influence on the land around you. Crops you plant grow noticeably faster than those tended by other hands, reaching maturity in a fraction of the normal time as though the soil itself responds eagerly to your touch. Your harvests also yield bonus resources, with each plant producing more than the standard amount \u2014 a subtle but compounding advantage that transforms a modest garden into a bountiful farm. Additionally, tamed animals that graze near your cultivated land breed more frequently, drawn by the lush vegetation and the calm energy you bring to your farmstead.", true, 0),
                    new ClassAbility("Animal Kinship", "Your patient and gentle nature extends beyond plants to the creatures of Valheim, who sense in you a caretaker rather than a threat. Tamed animals under your care breed at a significantly accelerated rate, growing your herds and flocks far faster than other farmers could manage. The taming process itself is quicker and more reliable, with wild animals responding to your offerings with less fear and more trust. This bond extends to all tameable creatures \u2014 boar, wolves, and lox alike recognize your calming presence and settle into domesticated life with surprising ease.", false, 10),
                    new ClassAbility("Fertile Ground", "The soil remembers your footsteps and rewards your devotion with extraordinary generosity. Crops growing within a wide radius around you have a meaningful chance to produce double their normal harvest when picked, as though the earth itself is offering tribute to the one person who truly understands its potential. This bonus stacks with your natural Green Thumb passive, meaning your most productive fields can yield harvests that dwarf what other farmers could achieve with twice the land. The effect is centered on you and moves as you walk, so tending your fields in person always produces the best results.", false, 25),
                    new ClassAbility("Bountiful Harvest", "You channel your deep connection to the land into a surge of vital energy that ripples outward through the soil, instantly bringing all nearby crops to full maturity regardless of how recently they were planted. Seeds that were sown minutes ago burst from the earth fully grown, and partially mature crops leap to harvest-ready state in a heartbeat. This ability has a long cooldown \u2014 it can only be used once per in-game day \u2014 but the sheer volume of food it produces in a single moment can sustain an entire settlement. Strategic use of this ability during planting cycles can multiply your farm's output exponentially.", false, 50)
                }
            );
        }

        private static StartingClass Healer()
        {
            return new StartingClass(
                "Healer",
                $"{PastLife}\nYour salves closed wounds that should have been graves and your prayers held the dying to this side of Hel's gate. Where others brought death, you brought them back from its door.\n\n{Purpose}\nIn Valheim, death is cheap but survival is precious. Mend what breaks and you will never stand alone.\n\n{AbilityName("Mending Aura")}  {Passive}\nNearby allies within 10m slowly regenerate health. The effect is stronger when you are resting or standing still.\n\n{LockedAbilityName("Soothing Presence")}\n{LockedDesc("Health regeneration while resting is increased and food healing is more effective. Your calming presence mends body and spirit alike.")}",
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
                    new ClassAbility("Mending Aura", "Your very presence knits torn flesh and soothes broken bones. Nearby allies within 10m slowly regenerate health over time, a faint warmth spreading outward from you like the glow of a hearthfire. The effect intensifies when you are resting or standing still, as your focused concentration channels more restorative energy into the aura. In the heat of battle the regeneration is modest, but during the quiet moments between fights it can mean the difference between pressing forward and limping home.", true, 0),
                    new ClassAbility("Soothing Presence", "Your calming nature has a tangible effect on the body's ability to heal itself. Health regeneration while resting is noticeably increased, and the healing provided by food is more effective, squeezing more vitality out of every meal. This passive benefit extends to the quiet rhythms of recovery \u2014 sitting by a fire, sleeping in a bed, or simply standing in the shelter of a well-built home. Over the course of a long expedition, the cumulative healing advantage is substantial, reducing your reliance on potions and meads.", false, 10),
                    new ClassAbility("Life Bond", "You forge an invisible thread of vital energy between yourself and your nearest allies, sharing the gift of recovery across the bond. When you receive healing from any source \u2014 food, potions, resting, or your own aura \u2014 a portion of that healing flows outward to nearby companions. The bond strengthens with proximity, rewarding groups that fight close together rather than scattered across the battlefield. In coordinated groups, a single well-timed healing potion can restore the entire party.", false, 25),
                    new ClassAbility("Purge", "You release a burst of purifying energy that washes over yourself and all nearby allies, stripping away every affliction that clings to body and spirit. Poison, frost, the soaking weight of water, and every other negative status effect is instantly cleansed in a single radiant pulse. The ability has a short cooldown, making it reliable enough to use reactively when the situation turns dire. Against enemies that rely on debilitating effects \u2014 blobs, drakes, leeches \u2014 a well-timed Purge can turn a losing fight around in an instant.", false, 50)
                }
            );
        }

        private static StartingClass Hunter()
        {
            return new StartingClass(
                "Hunter",
                $"{PastLife}\nYou tracked elk through blizzards and brought down boar in pitch darkness, reading the land like others read runes. The forest was your hall and the hunt your religion.\n\n{Purpose}\nValheim's beasts are fiercer than anything in Midgard. Study the tracks, respect the prey, and strike when the moment is right.\n\n{AbilityName("Predator's Mark")}  {Passive}\nCreatures you damage leave bloody footprints visible only to you. Marked creatures take increased damage from your subsequent attacks.\n\n{LockedAbilityName("Tracker")}\n{LockedDesc("Bloody tracks last longer and glow brighter, even in rain or darkness. Your sneak damage against creatures is increased, rewarding the patient stalk.")}",
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
                    new ClassAbility("Predator's Mark", "Your senses are attuned to the subtle signs of wounded prey in a way that borders on supernatural awareness. Creatures you damage leave faintly glowing bloody footprints on the ground, visible only to your trained eyes, allowing you to track wounded prey that flees into dense forest or over rough terrain. Marked creatures also take increased damage from your subsequent attacks, as your understanding of their movement patterns reveals weaknesses in their defense. The tracking marks persist for a generous duration, ensuring that even the fastest prey cannot simply outrun your patience.", true, 0),
                    new ClassAbility("Tracker", "Building on your natural predatory instincts, your tracking abilities sharpen to a razor's edge. Bloody footprints from wounded prey last significantly longer before fading, and the trails glow brighter and from further away, making tracking during rain or at night far more reliable. Your sneak damage against animals and creatures is meaningfully increased, rewarding the patient approach of stalking close before striking. This combination of extended tracking and enhanced stealth damage makes you the definitive apex predator of Valheim's wilderness.", false, 10),
                    new ClassAbility("Vital Strike", "Countless hours studying animal anatomy \u2014 watching how creatures move, where their muscles connect, where the blood flows closest to the surface \u2014 have given you an instinctive sense for critical vulnerabilities. Your attacks against creatures carry a meaningful chance to strike a vital organ or nerve cluster, dealing dramatically increased critical damage that can fell lesser beasts in a single blow. This critical hit chance applies to all weapon types, though it is particularly devastating with high-damage weapons like bows and spears. Against boss-tier enemies, vital strikes can interrupt attack patterns and create openings for sustained damage.", false, 25),
                    new ClassAbility("Feral Senses", "You quiet your mind and expand your awareness to encompass the living world around you, sensing the heartbeat of every creature within a vast radius. For a short duration, all nearby creatures are revealed through terrain and obstacles, their silhouettes glowing with a soft light that makes them visible through trees, hills, and even underground. Aggressive creatures are highlighted in red, passive ones in blue, and fleeing prey in yellow, giving you a complete tactical picture of your surroundings. This ability is invaluable for locating hidden enemies, planning hunting routes, and avoiding ambushes in dangerous biomes.", false, 50)
                }
            );
        }

        private static StartingClass Miner()
        {
            return new StartingClass(
                "Miner",
                $"{PastLife}\nYou dug where others feared to tread, hauling ore from veins buried deep beneath the mountain's heart. Your eyes could spot a glint of metal in stone so dark others saw nothing but worthless rock.\n\n{Purpose}\nThe tenth world hides its treasures under stone and swamp. Swing your pick and crack it open \u2014 the earth always rewards those stubborn enough to keep digging.\n\n{AbilityName("Vein Sense")}  {Passive}\nOre deposits and silver veins shimmer faintly within range, visible only to you. Your pickaxe swings cost less stamina against rock and ore.\n\n{LockedAbilityName("Deep Strike")}\n{LockedDesc("Your pickaxe technique is refined to near perfection. Every swing deals increased damage to ore and rock, and tool durability loss is dramatically reduced.")}",
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
                    new ClassAbility("Vein Sense", "Years of working underground have given you an almost supernatural sensitivity to the presence of mineral deposits in your surroundings. Ore veins, silver deposits, and valuable rock formations shimmer with a faint luminescence visible only to your trained eyes, making them stand out against ordinary stone even in poor lighting conditions. This innate awareness extends through solid rock to a meaningful distance, effectively revealing hidden deposits that other miners would walk right past. Additionally, your practiced swing technique means each strike of your pickaxe costs noticeably less stamina against rock and ore, allowing you to mine for extended periods without exhaustion.", true, 0),
                    new ClassAbility("Deep Strike", "Your decades of experience have refined your pickaxe technique to near perfection \u2014 every swing lands at the optimal angle, every impact transfers maximum force into the rock face with minimum wasted energy. Your pickaxe deals significantly increased damage to ore deposits and rock formations, shattering stone that would take others many more swings to break through. The efficiency of your technique also means dramatically reduced wear on your tools, extending pickaxe durability far beyond its normal lifespan. Combined, these bonuses mean you clear ore veins in a fraction of the time and with far fewer repair trips, making your mining expeditions vastly more productive.", false, 10),
                    new ClassAbility("Mother Lode", "Fortune favors the persistent, and no one is more persistent than a miner who has spent their life chasing veins deep into the earth. When you break ore deposits, there is a meaningful chance that the node yields double its normal output \u2014 a rich pocket of ore that seems to materialize under your expert strikes. Additionally, your keen eye for mineral formations gives you an increased chance of discovering rare gems and unusual materials hidden within the stone. This passive luck bonus applies to all mining activities and compounds beautifully with your other abilities, making every mining expedition a potentially lucrative venture.", false, 25),
                    new ClassAbility("Shatter Strike", "You channel every ounce of your mining expertise into a single devastating blow, driving your pickaxe into the ground with such force that a shockwave ripples outward through the bedrock. The tremor cracks and damages every rock formation, ore deposit, and destructible stone object in a wide radius around the impact point, dealing significant partial damage to all of them simultaneously. This ability is extraordinarily efficient for clearing dense mining areas \u2014 a copper deposit surrounded by rock, a cluster of silver veins buried in frozen stone, or a field of muddy scrap piles in the swamp crypts. The shockwave can break weaker nodes outright and leaves larger deposits cracked and ready to shatter with a few follow-up swings.", false, 50)
                }
            );
        }
    }
}
