using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TheForest.Building.Config;
using TheForest.Building.Data;
using TheForest.Crafting;
using TheForest.Items;

namespace TheForest.EditorTools
{
    public static class SonsOfTheForestDataSeeder
    {
        private const string Root = "Assets/Resources/SotFData";
        private const string ItemDir = Root + "/Items";
        private const string RecipeDir = Root + "/Recipes";
        private const string BlueprintDir = Root + "/Blueprints";

        private static readonly Dictionary<string, ItemData> ItemsById = new Dictionary<string, ItemData>();

        [MenuItem("The Forest/Data/Seed Sons of the Forest Data")]
        public static void Seed()
        {
            EnsureFolders();
            ItemsById.Clear();

            SeedResources();
            SeedWeapons();
            SeedAmmo();
            SeedArmor();
            SeedToolsAndConsumables();
            SeedRecipes();
            SeedBlueprints();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SotFDataSeeder] Seeded Sons of the Forest data into " + Root + ".");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(ItemDir);
            Directory.CreateDirectory(RecipeDir);
            Directory.CreateDirectory(BlueprintDir);
        }

        private static void SeedResources()
        {
            Item("stick", "Stick", ItemCategory.Resource, 20, "SotF item id 392.");
            Item("rock", "Rock", ItemCategory.Resource, 20, "SotF item id 393.");
            Item("small_rock", "Small Rock", ItemCategory.Resource, 20, "SotF item id 476.");
            Item("stone", "Stone", ItemCategory.Resource, 20, "Large building stone.");
            Item("log", "Log", ItemCategory.Resource, 1, "SotF item id 78. Runtime logs are still handled as LogPiece world objects.");
            Item("log_plank", "Log Plank", ItemCategory.Resource, 10, "SotF item id 395.");
            Item("rope", "Rope", ItemCategory.Resource, 10, "SotF item id 403.");
            Item("cloth", "Cloth", ItemCategory.Resource, 10, "SotF item id 415.");
            Item("duct_tape", "Duct Tape", ItemCategory.Resource, 10, "SotF item id 419.");
            Item("bone", "Bone", ItemCategory.Resource, 20, "SotF item id 405.");
            Item("skull", "Skull", ItemCategory.Resource, 10, "SotF item id 430.");
            Item("leaf", "Leaf", ItemCategory.Resource, 50, "SotF item id 484.");
            Item("wire", "Wire", ItemCategory.Resource, 20, "SotF item id 418.");
            Item("circuit_board", "Circuit Board", ItemCategory.Resource, 10, "SotF item id 416.");
            Item("battery", "Battery", ItemCategory.Resource, 10, "Recipe ingredient used by Tech Armor and flashlight-style combinations.");
            Item("tech_mesh", "Tech Mesh", ItemCategory.Resource, 10, "SotF item id 553.");
            Item("vodka_bottle", "Vodka Bottle", ItemCategory.Resource, 10, "SotF item id 414.");
            Item("turtle_shell", "Turtle Shell", ItemCategory.Resource, 10, "SotF item id 506.");
            Food("turtle_egg", "Turtle Egg", 401, 12f, 0f, 0f, 0f, 10);
            Food("oyster", "Oyster", 466, 10f, 0f, 0f, 0f, 10);
            Item("animal_hide", "Animal Hide", ItemCategory.Resource, 10, "Hide used by Hide Armor and furniture blueprints.");
            Item("feather", "Feather", ItemCategory.Resource, 50, "SotF item id 479.");
            Item("coin", "Coin", ItemCategory.Resource, 50, "SotF item id 502.");
            Item("wrist_watch", "Wrist Watch", ItemCategory.Resource, 10, "SotF item id 410.");
            Item("c4_brick", "C4 Brick", ItemCategory.Resource, 5, "SotF item id 420.");
            Item("grappling_hook", "Grappling Hook", ItemCategory.Resource, 10, "Printed grappling hook, SotF item id 560.");
            Item("radio", "Radio", ItemCategory.Resource, 1, "SotF item id 590.");
            Item("grenade", "Frag Grenade", ItemCategory.Weapon, 5, "SotF weapon/ammo item id 381.");

            Item("aloe_vera", "Aloe Vera", ItemCategory.Resource, 10, "SotF item id 451.");
            Item("arrowleaf", "Arrowleaf", ItemCategory.Resource, 10, "SotF item id 454.");
            Item("chicory", "Chicory", ItemCategory.Resource, 10, "SotF item id 465.");
            Item("devils_club", "Devil's Club", ItemCategory.Resource, 10, "SotF item id 449.");
            Item("fireweed", "Fireweed", ItemCategory.Resource, 10, "SotF item id 453.");
            Item("horsetail", "Horsetail", ItemCategory.Resource, 10, "SotF item id 450.");
            Item("yarrow", "Yarrow", ItemCategory.Resource, 10, "SotF item id 452.");
        }

        private static void SeedWeapons()
        {
            Axe("tactical_axe", "Tactical Axe", 379, 18f, 1.35f, 0.35f, 0.35f);
            Axe("modern_axe", "Modern Axe", 356, 34f, 1.0f, 0.75f, 0.5f);
            Axe("firefighter_axe", "Firefighter Axe", 431, 44f, 0.75f, 0.9f, 0.45f);

            Melee("utility_knife", "Utility Knife", 380, 10f, 1.8f, 0.1f, 1f, false, 0, false);
            Melee("machete", "Machete", 359, 22f, 1.55f, 0.25f, 0.8f, false, 0, false);
            Melee("crafted_spear", "Crafted Spear", 474, 24f, 1.25f, 0.2f, 0.75f, false, 0, false);
            Melee("crafted_club", "Crafted Club", 477, 32f, 0.9f, 1f, 0.55f, false, 0, false);
            Melee("katana", "Katana", 367, 34f, 1.75f, 0.05f, 0.9f, false, 0, false);
            Melee("putter", "Putter", 525, 14f, 1.2f, 0.35f, 0.8f, false, 0, false);
            Melee("guitar", "Guitar", 340, 20f, 1.05f, 0.5f, 0.65f, false, 0, false);
            Melee("electric_chainsaw", "Electric Chainsaw", 394, 18f, 3.0f, 0.2f, 0.7f, true, 0, false);
            Melee("stun_baton", "Stun Baton", 396, 16f, 1.25f, 0.25f, 0.75f, false, 0, true);
            Melee("repair_tool", "Repair Tool", 422, 8f, 1.1f, 0.2f, 0.8f, false, 0, false);

            Bow("crafted_bow", "Crafted Bow", 443, 12f, 40f, 0.9f, 5f);
            Bow("compound_bow", "Compound Bow", 360, 18f, 55f, 0.8f, 12f);

            Firearm("pistol", "Pistol", 355, AmmoCaliber.Pistol9mm, 12, 1.8f, 0.22f, 90f, false);
            Firearm("revolver", "Revolver", 386, AmmoCaliber.Pistol9mm, 6, 2.0f, 0.55f, 95f, false);
            Firearm("shotgun", "Shotgun", 358, AmmoCaliber.Shotgun, 1, 2.4f, 0.95f, 55f, false);
            Firearm("rifle", "Rifle", 361, AmmoCaliber.Rifle, 5, 2.2f, 0.75f, 120f, false);
            Firearm("stun_gun", "Stun Gun", 353, AmmoCaliber.StunCartridge, 1, 1.8f, 0.85f, 0f, true);
        }

        private static void SeedAmmo()
        {
            Ammo("ammo_9mm", "9mm Ammo", 362, AmmoCaliber.Pistol9mm, 35f, 1, 0f);
            Ammo("ammo_slug", "Slug Ammo", 363, AmmoCaliber.Shotgun, 90f, 1, 0f);
            Ammo("ammo_buckshot", "Buckshot Ammo", 364, AmmoCaliber.Shotgun, 14f, 8, 8f);
            Ammo("ammo_rifle", "Rifle Ammo", 387, AmmoCaliber.Rifle, 75f, 1, 0f);
            Ammo("ammo_stun_gun", "Stun Gun Ammo", 369, AmmoCaliber.StunCartridge, 0f, 1, 0f);

            Arrow("crafted_arrow", "Crafted Arrow", 507, 18f);
            Arrow("carbon_fiber_arrow", "Carbon Fiber Arrow", 373, 28f);
            Arrow("printed_arrow", "Printed Arrow", 618, 24f);
        }

        private static void SeedArmor()
        {
            Armor("leaf_armor", "Leaf Armor", 473, ArmorTier.Leaf, 10f, 1, false, 0f);
            Armor("hide_armor", "Hide Armor", 519, ArmorTier.Hide, 18f, 2, false, 0f);
            Armor("bone_armor", "Bone Armor", 494, ArmorTier.Bone, 26f, 3, false, 0f);
            Armor("creepy_armor", "Creepy Armor", 593, ArmorTier.Creepy, 40f, 3, false, 0f);
            Armor("tech_armor", "Tech Armor", 554, ArmorTier.Tech, 50f, 4, false, 0f);
            Armor("ancient_armor", "Ancient Armor", 572, ArmorTier.Golden, 0f, 1, true, 0.3f);
        }

        private static void SeedToolsAndConsumables()
        {
            GpsLocator("gps_locator", "GPS Locator", 529);
            Item("gps_tracker", "GPS Tracker", ItemCategory.Tool, 1, "SotF item id 412.");
            Item("walkie_talkie", "Walkie-Talkie", ItemCategory.Tool, 1, "SotF item id 486.");
            Item("binoculars", "Binoculars", ItemCategory.Tool, 1, "SotF item id 341.");
            Item("rope_gun", "Rope Gun", ItemCategory.Tool, 1, "SotF item id 522.");
            Item("rebreather", "Rebreather", ItemCategory.Tool, 1, "SotF item id 444.");
            Item("shovel", "Shovel", ItemCategory.Tool, 1, "SotF item id 485.");
            Item("flashlight", "Flashlight", ItemCategory.Tool, 1, "SotF item id 471.");
            Item("printed_flask", "Printed Flask", ItemCategory.Tool, 1, "SotF item id 426.");
            Item("tarp", "Tarp", ItemCategory.Resource, 10, "SotF item id 504.");
            Item("torch", "Torch", ItemCategory.Tool, 1, "SotF item id 503.");
            Item("can_opener", "Can Opener", ItemCategory.Tool, 1, "Tool used with canned food.");
            Item("crossbow", "Crossbow", ItemCategory.Weapon, 1, "SotF item id 365. Identity only: current project has no Crossbow/Bolt controller.");
            Item("crossbow_bolt", "Crossbow Bolt", ItemCategory.Resource, 50, "SotF item id 368. Identity only: current project has no bolt projectile data class.");
            Item("slingshot", "Slingshot", ItemCategory.Weapon, 1, "SotF item id 459. Identity only: current project has no slingshot controller.");

            Food("fish", "Fish", 436, 15f, 0f, 0f, 0f, 5);
            Food("raw_meat", "Raw Meat", 433, 18f, 0f, 0f, 0f, 5);
            Food("cooked_fish", "Cooked Fish", 0, 24f, 0f, 5f, 0f, 5);
            Food("dried_fish", "Dried Fish", 0, 26f, -2f, 0f, 0f, 5);
            Food("burnt_fish", "Burnt Fish", 0, 7f, -10f, 0f, -3f, 5);
            Food("spoiled_fish", "Spoiled Fish", 0, 4f, -5f, 0f, -8f, 5);
            Food("cooked_meat", "Cooked Meat", 0, 28f, 0f, 5f, 0f, 5);
            Food("dried_meat", "Dried Meat", 0, 30f, -3f, 0f, 0f, 5);
            Food("burnt_meat", "Burnt Meat", 0, 8f, -10f, 0f, -3f, 5);
            Food("spoiled_meat", "Spoiled Meat", 0, 5f, -5f, 0f, -10f, 5);
            Food("clean_water", "Clean Water", 0, 0f, 35f, 0f, 0f, 5);
            Food("canned_food", "Canned Food", 434, 35f, 0f, 0f, 0f, 5);
            Food("cat_food", "Cat Food", 464, 25f, 0f, 0f, 0f, 5);
            Food("mre", "MRE", 438, 55f, 0f, 15f, 0f, 5);
            Food("energy_drink", "Energy Drink", 439, 0f, 25f, 45f, 0f, 5);
            Food("ramen_noodles", "Ramen Noodles", 421, 20f, -8f, 0f, 0f, 5);
            Food("bacon_bite", "Bacon Bite", 571, 15f, 0f, 0f, 0f, 5);

            Food("medicine", "Medicine", 437, 0f, 0f, 0f, 55f, 5);
            Food("health_mix", "Health Mix", 455, 0f, 0f, 0f, 35f, 5);
            Food("health_mix_plus", "Health Mix +", 456, 0f, 0f, 0f, 60f, 5);
            Food("energy_mix", "Energy Mix", 461, 0f, 0f, 35f, 0f, 5);
            Food("energy_mix_plus", "Energy Mix +", 462, 0f, 0f, 60f, 0f, 5);
        }

        private static void SeedRecipes()
        {
            Item("molotov", "Molotov", ItemCategory.Weapon, 5, "SotF thrown weapon. Created as ItemData because this project has no throwable data class.");
            Item("zipline_rope", "Zipline Rope", ItemCategory.Resource, 10, "SotF item id 523.");
            Item("time_bomb", "Time Bomb", ItemCategory.Weapon, 5, "Crafted explosive. Created as ItemData because this project has no explosive data class.");

            Recipe("crafted_bow", "crafted_bow", ("stick", 2), ("rope", 1), ("duct_tape", 1));
            Recipe("crafted_club", "crafted_club", ("stick", 1), ("skull", 1), ("rope", 1));
            RecipeWithTools("crafted_spear", "crafted_spear",
                ("stick", 2, true),
                ("utility_knife", 1, false),
                ("duct_tape", 1, true));
            Recipe("bone_armor", "bone_armor", ("bone", 4), ("rope", 1), ("duct_tape", 1));
            Recipe("hide_armor", "hide_armor", ("animal_hide", 2), ("cloth", 1));
            Recipe("leaf_armor", "leaf_armor", ("leaf", 10), ("cloth", 1));
            Recipe("stone_arrow", "crafted_arrow", ("small_rock", 4), ("feather", 2), ("stick", 2));
            Recipe("tech_armor", "tech_armor", ("tech_mesh", 10), ("wire", 10), ("duct_tape", 10), ("battery", 10), ("circuit_board", 10));
            Recipe("molotov", "molotov", ("vodka_bottle", 1), ("cloth", 1));
            Recipe("repair_tool", "repair_tool", ("stick", 1), ("stone", 1), ("duct_tape", 1));
            Recipe("torch", "torch", ("stick", 1), ("cloth", 1));
            Recipe("zipline_rope", "zipline_rope", ("grappling_hook", 1), ("rope", 1));
            Recipe("time_bomb", "time_bomb", ("wrist_watch", 1), ("wire", 1), ("duct_tape", 1), ("coin", 5), ("circuit_board", 1), ("c4_brick", 1));
            Recipe("energy_mix", "energy_mix", ("chicory", 1), ("arrowleaf", 1));
            Recipe("energy_mix_plus", "energy_mix_plus", ("chicory", 1), ("devils_club", 1), ("fireweed", 1));
            Recipe("health_mix", "health_mix", ("yarrow", 1), ("aloe_vera", 1));
            Recipe("health_mix_plus", "health_mix_plus", ("horsetail", 1), ("aloe_vera", 1), ("fireweed", 1));
        }

        private static void SeedBlueprints()
        {
            var blueprints = new List<BlueprintData>
            {
                Blueprint("hunting_shelter", "Hunting Shelter", "Shelter", ("full_log", 5), ("stick", 6), ("large_stone", 7)),
                Blueprint("small_log_cabin", "Small Log Cabin", "Shelter", ("full_log", 75)),
                Blueprint("lean_to", "Lean To", "Shelter", ("full_log", 53)),
                Blueprint("lookout_tower", "Lookout Tower", "Shelter", ("full_log", 60), ("rope", 1)),
                Blueprint("tree_platform_1", "Tree Platform 1", "Tree Shelter", ("full_log", 7), ("rope", 1)),
                Blueprint("tree_platform_2", "Tree Platform 2", "Tree Shelter", ("full_log", 35), ("rope", 1)),
                Blueprint("tree_shelter_1", "Tree Shelter 1", "Tree Shelter", ("full_log", 70), ("rope", 1)),
                Blueprint("tree_shelter_2", "Tree Shelter 2", "Tree Shelter", ("full_log", 96), ("rope", 1)),

                Blueprint("stick_chair", "Stick Chair", "Furniture", ("stick", 14)),
                Blueprint("bench", "Bench", "Furniture", ("full_log", 2)),
                Blueprint("bone_chair", "Bone Chair", "Furniture", ("bone", 15), ("skull", 1)),
                Blueprint("bone_chandelier", "Bone Chandelier", "Furniture", ("bone", 19), ("skull", 9)),
                Blueprint("wall_torch", "Wall Torch", "Furniture", ("stick", 1), ("cloth", 1)),
                Blueprint("ceiling_skull_lamp", "Ceiling Skull Lamp", "Furniture", ("rope", 1), ("skull", 1)),
                Blueprint("head_trophy_mount", "Head Trophy Mount", "Furniture", ("stick", 1)),
                Blueprint("deer_hide_rug", "Deer Hide Rug", "Furniture", ("animal_hide", 1)),
                Blueprint("table", "Table", "Furniture", ("full_log", 3)),
                Blueprint("round_table", "Round Table", "Furniture", ("full_log", 6)),
                Blueprint("stick_bed", "Stick Bed", "Furniture", ("stick", 16), ("duct_tape", 1)),
                Blueprint("double_bed", "Double Bed", "Furniture", ("stick", 40), ("full_log", 4), ("duct_tape", 2), ("animal_hide", 2)),

                Blueprint("stick_storage", "Stick Storage", "Storage", ("stick", 6)),
                Blueprint("rock_storage", "Rock Storage", "Storage", ("stick", 7)),
                Blueprint("log_storage", "Log Storage", "Storage", ("stick", 8)),
                Blueprint("large_log_storage", "Large Log Storage", "Storage", ("stick", 40)),
                Blueprint("stone_storage", "Stone Storage", "Storage", ("stick", 12)),
                Blueprint("bone_storage", "Bone Storage", "Storage", ("stick", 7)),
                Blueprint("spear_rack", "Spear Rack", "Storage", ("stick", 10)),
                Blueprint("firewood_storage", "Firewood Storage", "Storage", ("stick", 20)),
                Blueprint("drying_rack", "Drying Rack", "Storage", ("stick", 9)),
                Blueprint("armor_rack", "Armor Rack", "Storage", ("stick", 20), ("duct_tape", 1)),
                Blueprint("shelf", "Shelf", "Storage", ("full_log", 2)),
                Blueprint("wall_shelf", "Wall Shelf", "Storage", ("stick", 2), ("full_log", 1)),
                Blueprint("weapon_rack", "Weapon Rack", "Storage", ("stick", 9), ("full_log", 1)),
                Blueprint("wall_weapon_rack", "Wall Weapon Rack", "Storage", ("stick", 5), ("full_log", 1)),

                Blueprint("rain_catcher", "Rain Catcher", "Utility", ("stick", 12), ("turtle_shell", 1)),
                Blueprint("stone_fireplace", "Stone Fireplace", "Utility", ("large_stone", 42)),
                Blueprint("bird_house", "Bird House", "Utility", ("stick", 16)),
                Blueprint("scarecrow", "Scarecrow", "Utility", ("stick", 14), ("duct_tape", 1)),
                Blueprint("basic_log_sled", "Basic Log Sled", "Utility", ("stick", 33)),
                Blueprint("log_sled", "Log Sled", "Utility", ("stick", 60), ("full_log", 3), ("skull", 2), ("cloth", 2), ("duct_tape", 4)),

                Blueprint("standing_planter", "Standing Planter", "Gardening", ("stick", 18)),
                Blueprint("wall_planter", "Wall Planter", "Gardening", ("stick", 16)),

                Blueprint("small_animal_trap", "Small Animal Trap", "Traps", ("stick", 14)),
                Blueprint("fish_trap", "Fish Trap", "Traps", ("stick", 25)),
                Blueprint("bonemaker_trap", "Bonemaker Trap", "Traps", ("stick", 2), ("leaf", 3), ("large_stone", 3), ("vodka_bottle", 1), ("rope", 1)),
                Blueprint("fly_swatter_trap", "Fly Swatter Trap", "Traps", ("stick", 10), ("large_stone", 3), ("rope", 1)),
                Blueprint("spring_trap", "Spring Trap", "Traps", ("stick", 7), ("turtle_shell", 1), ("wire", 2)),
                Blueprint("hokey_pokey_trap", "Hokey Pokey Trap", "Traps", ("full_log", 6), ("rope", 3), ("stick", 10), ("large_stone", 15)),
                Blueprint("silent_alarm", "Silent Alarm", "Traps", ("stick", 13), ("bone", 8), ("large_stone", 1), ("skull", 1), ("wire", 1), ("radio", 1)),
                Blueprint("leaf_trap", "Leaf Trap", "Traps", ("leaf", 20)),
                Blueprint("explosive_tripwire", "Explosive Tripwire", "Traps", ("stick", 1), ("wire", 1), ("grenade", 1)),
                Blueprint("molotov_tripwire", "Molotov Tripwire", "Traps", ("stick", 1), ("rope", 1), ("large_stone", 1), ("vodka_bottle", 1), ("leaf", 4))
            };

            var config = AssetDatabase.LoadAssetAtPath<BlueprintConfig>("Assets/BuildingConfig/BlueprintConfig.asset");
            if (config != null)
            {
                config.defaultBlueprints = blueprints.ToArray();
                EditorUtility.SetDirty(config);
            }
        }

        private static ItemData Item(string id, string displayName, ItemCategory category, int maxStack, string description)
        {
            var item = Asset<ItemData>(ItemDir + "/Item_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = category;
            item.description = description;
            item.maxStack = maxStack;
            item.isEquippable = category == ItemCategory.Weapon || category == ItemCategory.Tool;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void Food(string id, string displayName, int sotfId, float hunger, float thirst, float energy, float health, int maxStack)
        {
            string description = sotfId > 0
                ? "SotF item id " + sotfId + "."
                : "Survival item variant used by cooking and preservation systems.";
            var item = Item(id, displayName, health > 0f ? ItemCategory.Medical : ItemCategory.Food, maxStack, description);
            item.hungerRestore = hunger;
            item.thirstRestore = thirst;
            item.energyRestore = energy;
            item.healthRestore = health;
        }

        private static void Axe(string id, string displayName, int sotfId, float damage, float swingSpeed, float block, float moveMult)
        {
            var item = Asset<AxeItemData>(ItemDir + "/Axe_" + FileName(id) + ".asset");
            FillMelee(item, id, displayName, sotfId, damage, swingSpeed, block, moveMult, false, 0, false);
        }

        private static void Melee(string id, string displayName, int sotfId, float damage, float swingSpeed, float block, float moveMult, bool freeStamina, int maxSwingsBeforeBreak, bool stun)
        {
            var item = Asset<MeleeWeaponItemData>(ItemDir + "/Melee_" + FileName(id) + ".asset");
            FillMelee(item, id, displayName, sotfId, damage, swingSpeed, block, moveMult, freeStamina, maxSwingsBeforeBreak, stun);
        }

        private static void FillMelee(MeleeWeaponItemData item, string id, string displayName, int sotfId, float damage, float swingSpeed, float block, float moveMult, bool freeStamina, int maxSwingsBeforeBreak, bool stun)
        {
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Weapon;
            item.description = "SotF item id " + sotfId + ". Damage/speed are project balance values mapped onto current fields.";
            item.maxStack = 1;
            item.isEquippable = true;
            item.blockPercent = block;
            item.blockMoveSpeedMultiplier = moveMult;
            item.damage = damage;
            item.swingSpeed = swingSpeed;
            item.hitTimeNormalized = 0.45f;
            item.freeStamina = freeStamina;
            item.maxSwingsBeforeBreak = maxSwingsBeforeBreak;
            item.appliesStunOnHit = stun;
            item.meleeStunDuration = stun ? 2.5f : 0f;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Bow(string id, string displayName, int sotfId, float minSpeed, float maxSpeed, float drawTime, float damage)
        {
            var item = Asset<BowItemData>(ItemDir + "/Bow_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Weapon;
            item.description = "SotF item id " + sotfId + ". Uses current BowItemData projectile model.";
            item.maxStack = 1;
            item.isEquippable = true;
            item.minLaunchSpeed = minSpeed;
            item.maxLaunchSpeed = maxSpeed;
            item.drawTime = drawTime;
            item.bowDamage = damage;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Firearm(string id, string displayName, int sotfId, AmmoCaliber caliber, int magazineSize, float reloadSeconds, float fireCooldown, float range, bool stun)
        {
            var item = Asset<FirearmItemData>(ItemDir + "/Firearm_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Weapon;
            item.description = "SotF item id " + sotfId + ". Uses current FirearmItemData hitscan/stun model.";
            item.maxStack = 1;
            item.isEquippable = true;
            item.caliber = caliber;
            item.magazineSize = magazineSize;
            item.reloadSeconds = reloadSeconds;
            item.fireCooldown = fireCooldown;
            item.hitscanRange = range;
            item.isStunWeapon = stun;
            item.stunDuration = stun ? 4f : 0f;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Ammo(string id, string displayName, int sotfId, AmmoCaliber caliber, float damage, int pellets, float spread)
        {
            var item = Asset<AmmoItemData>(ItemDir + "/Ammo_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Resource;
            item.description = "SotF ammo item id " + sotfId + ".";
            item.maxStack = 100;
            item.caliber = caliber;
            item.damage = damage;
            item.pellets = pellets;
            item.spreadAngle = spread;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Arrow(string id, string displayName, int sotfId, float damage)
        {
            var item = Asset<ArrowItemData>(ItemDir + "/Arrow_" + FileName(id) + ".asset");
            var projectileTemplate = AssetDatabase.LoadAssetAtPath<ArrowItemData>("Assets/Item_/Arrow_Normal.asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Resource;
            item.description = "SotF arrow item id " + sotfId + ". Current ArrowType enum maps this as Normal.";
            item.maxStack = 50;
            item.arrowType = ArrowType.Normal;
            item.arrowDamage = damage;
            if (item.arrowProjectilePrefab == null && projectileTemplate != null)
                item.arrowProjectilePrefab = projectileTemplate.arrowProjectilePrefab;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Armor(string id, string displayName, int sotfId, ArmorTier tier, float absorb, int hits, bool unbreakable, float flatReduction)
        {
            var item = Asset<ArmorItemData>(ItemDir + "/Armor_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Special;
            item.description = "SotF armor item id " + sotfId + ". Absorb/hits are project balance values mapped onto current armor fields.";
            item.maxStack = 10;
            item.tier = tier;
            item.absorbPerHit = absorb;
            item.hitsUntilBreak = hits;
            item.unbreakable = unbreakable;
            item.flatReductionPercent = flatReduction;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void GpsLocator(string id, string displayName, int sotfId)
        {
            var item = Asset<GpsLocatorItemData>(ItemDir + "/GpsLocator_" + FileName(id) + ".asset");
            item.itemId = id;
            item.displayName = displayName;
            item.category = ItemCategory.Special;
            item.description = "SotF item id " + sotfId + ". Virginia gift/tracking item.";
            item.maxStack = 5;
            ItemsById[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void Recipe(string id, string resultId, params (string itemId, int amount)[] ingredients)
        {
            var expanded = new (string itemId, int amount, bool consume)[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++)
                expanded[i] = (ingredients[i].itemId, ingredients[i].amount, true);
            RecipeWithTools(id, resultId, expanded);
        }

        private static void RecipeWithTools(string id, string resultId, params (string itemId, int amount, bool consume)[] ingredients)
        {
            var recipe = Asset<CraftingRecipe>(RecipeDir + "/Recipe_" + FileName(id) + ".asset");
            recipe.ingredients.Clear();
            foreach (var ing in ingredients)
            {
                if (!ItemsById.TryGetValue(ing.itemId, out var item))
                {
                    Debug.LogWarning("[SotFDataSeeder] Missing recipe ingredient item: " + ing.itemId);
                    continue;
                }

                recipe.ingredients.Add(new Ingredient { item = item, amount = ing.amount, consume = ing.consume });
            }

            if (!ItemsById.TryGetValue(resultId, out recipe.result))
                Debug.LogWarning("[SotFDataSeeder] Missing recipe result item: " + resultId);

            recipe.resultAmount = 1;
            recipe.isUpgrade = false;
            EditorUtility.SetDirty(recipe);
        }

        private static BlueprintData Blueprint(string id, string displayName, string category, params (string materialId, int amount)[] costs)
        {
            var blueprint = Asset<BlueprintData>(BlueprintDir + "/Blueprint_" + FileName(id) + ".asset");
            blueprint.blueprintId = id;
            blueprint.displayName = displayName;
            blueprint.category = category;
            blueprint.description = "SotF guide-book blueprint. Cost data seeded from verified wiki/guide references.";
            blueprint.isHidden = false;
            blueprint.materialCosts.Clear();
            foreach (var cost in costs)
            {
                blueprint.materialCosts.Add(new MaterialCost
                {
                    materialId = cost.materialId,
                    amount = cost.amount
                });
            }
            blueprint.pieces.Clear();
            EditorUtility.SetDirty(blueprint);
            return blueprint;
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string FileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }
}
