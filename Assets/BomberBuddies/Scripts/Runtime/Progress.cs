using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Everything that survives between matches, as the original kept it in its save: the bank of credits, the upgrade levels,
    /// the guns bought and fitted, the craft you fly, and the teammates hired. Stored in PlayerPrefs. The catalog, prices and
    /// stat formulas are the original's (CATALOG, GUNS, MATES, CRAFT_PRICE, costOf, statsFrom).
    /// </summary>
    public static class Progress
    {
        public class Item { public string id, name, desc, group; public int max; public bool endless; public int basePrice; }
        public class Gun { public string id, name, desc; public int price; public float interval, dmg, speed, fall0, range, heat, pen, spread, splash; }
        public class Mate { public string id, name, desc; public int basePrice, n; }
        public class CraftDef { public string id, name, desc; public int price; public bool available; }

        public static readonly Item[] Catalog =
        {
            new Item { group = "Airframe", id = "armor", name = "Armor plating", max = 5, endless = true, basePrice = 150, desc = "+25 hull per level" },
            new Item { group = "Airframe", id = "engine", name = "Tuned engine", max = 4, basePrice = 200, desc = "Faster cruise, tighter turns" },
            new Item { group = "Airframe", id = "tank", name = "Boost tank", max = 4, endless = true, basePrice = 150, desc = "Longer boost, quicker refill" },
            new Item { group = "Airframe", id = "repair", name = "Field repair kit", max = 3, endless = true, basePrice = 300, desc = "Hull slowly repairs in flight" },
            new Item { group = "Airframe", id = "life", name = "Spare aircraft", max = 2, basePrice = 600, desc = "+1 life every flight" },
            new Item { group = "Gun", id = "rate", name = "Faster shooting", max = 5, endless = true, basePrice = 200, desc = "12% quicker fire rate per level" },
            new Item { group = "Gun", id = "ap", name = "Armor-piercing ammo", max = 4, endless = true, basePrice = 250, desc = "More damage, bites into tanks and bunkers" },
            new Item { group = "Gun", id = "cool", name = "Barrel cooling", max = 4, endless = true, basePrice = 150, desc = "Fire longer before overheating" },
            new Item { group = "Bombs", id = "rack", name = "Bigger bomb rack", max = 4, endless = true, basePrice = 200, desc = "+2 bombs carried per level" },
            new Item { group = "Bombs", id = "reload", name = "Fast rearm", max = 4, endless = true, basePrice = 200, desc = "Bombs refill 15% faster per level" },
            new Item { group = "Bombs", id = "heavy", name = "Heavy bombs", max = 4, endless = true, basePrice = 300, desc = "Bigger blast, deeper craters" },
            new Item { group = "Bombs", id = "napalm", name = "Napalm", max = 1, basePrice = 1800, desc = "Every bomb bursts into a river of fire that keeps burning" },
            new Item { group = "Attachments", id = "wing", name = "Wing guns", max = 2, basePrice = 500, desc = "Adds a gun barrel per level" },
            new Item { group = "Attachments", id = "rockets", name = "Rocket pods", max = 3, basePrice = 600, desc = "Fires a rocket while the gun is held" },
            new Item { group = "Attachments", id = "cluster", name = "Cluster casing", max = 3, basePrice = 700, desc = "Bombs split into bomblets above the ground" },
            new Item { group = "Attachments", id = "flares", name = "Flare dispenser", max = 3, basePrice = 400, desc = "Auto-decoys missiles that get close" },
        };

        public static readonly Gun[] Guns =
        {
            new Gun { id = "m2", name = ".50 cal machine guns", desc = "Fast and light. Full damage to 480, gone by 640.", price = 0, interval = 0.085f, dmg = 10f, speed = 640f, fall0 = 480f, range = 640f, heat = 0.042f, pen = 0f, spread = 1.5f, splash = 0f },
            new Gun { id = "mini", name = "Minigun", desc = "Shreds troops up close. Full damage to 280, gone by 460.", price = 2500, interval = 0.038f, dmg = 6f, speed = 620f, fall0 = 280f, range = 460f, heat = 0.021f, pen = 0f, spread = 1.2f, splash = 0f },
            new Gun { id = "m20", name = "20mm cannon", desc = "Hits harder. Full damage to 560, reaches 780.", price = 1500, interval = 0.16f, dmg = 24f, speed = 700f, fall0 = 560f, range = 780f, heat = 0.07f, pen = 0.2f, spread = 2.0f, splash = 0f },
            new Gun { id = "m25", name = "25mm Bushmaster", desc = "Long reach and armor bite. Full damage to 800, reaches 1,050.", price = 4000, interval = 0.24f, dmg = 42f, speed = 760f, fall0 = 800f, range = 1050f, heat = 0.095f, pen = 0.4f, spread = 2.4f, splash = 9f },
            new Gun { id = "m30", name = "30mm chain gun", desc = "Slow explosive shells. Full damage to 920, reaches 1,200.", price = 9000, interval = 0.36f, dmg = 70f, speed = 720f, fall0 = 920f, range = 1200f, heat = 0.13f, pen = 0.55f, spread = 2.9f, splash = 15f },
        };

        public static readonly Mate[] Mates =
        {
            new Mate { id = "squad", name = "Rifle squad", desc = "Four soldiers on foot. Cheap, and good at soaking up attention.", basePrice = 400, n = 4 },
            new Mate { id = "car", name = "Scout car", desc = "Fast, with a mounted machine gun.", basePrice = 700, n = 1 },
            new Mate { id = "tank", name = "Tank", desc = "Slow and tough. Its cannon cracks bunkers and armor.", basePrice = 1600, n = 1 },
            new Mate { id = "heli", name = "Gunship", desc = "Hovers near the fight and works targets with its chin gun.", basePrice = 2400, n = 1 },
            new Mate { id = "medic", name = "Field medic", desc = "Follows the squad and patches up anyone hurt. Patches you up too.", basePrice = 900, n = 1 },
            new Mate { id = "wing", name = "Wingman", desc = "A fighter that makes strafing and bombing runs on whatever is nearest.", basePrice = 2000, n = 1 },
        };

        /// <summary>The roster. Only the first two fly in this build; the rest are shown so you can see what is coming (and what it costs).</summary>
        public static readonly CraftDef[] Crafts =
        {
            new CraftDef { id = "plane", name = "Fighter-bomber", desc = "Fast, loops, dive-bombs. Guns fire straight ahead.", price = 0, available = true },
            new CraftDef { id = "heli", name = "Attack helicopter", desc = "Hovers and backs up. The chin gun tracks targets ahead of you by itself.", price = 0, available = true },
            new CraftDef { id = "soldier", name = "Soldier: rifleman", desc = "On foot with a rifle and grenades. Boost is a sprint.", price = 0 },
            new CraftDef { id = "drone", name = "Bomber drone", desc = "Flies like an FPV quad and drops grenades.", price = 800 },
            new CraftDef { id = "gunner", name = "Soldier: machine gunner", desc = "A light machine gun and more armor. Slower on his feet.", price = 900 },
            new CraftDef { id = "monkey", name = "Space monkey", desc = "A rabid astronaut test monkey with a stolen ray gun.", price = 900 },
            new CraftDef { id = "sniper", name = "Soldier: sniper", desc = "A long rifle that hits hard from very far away.", price = 1200 },
            new CraftDef { id = "medic", name = "Soldier: medic", desc = "A carbine, and hands that put people back together.", price = 1200 },
            new CraftDef { id = "rocketeer", name = "Soldier: rocket trooper", desc = "An anti-tank rocket, and a carbine for everything else.", price = 1500 },
            new CraftDef { id = "tank", name = "Battle tank", desc = "Heavy armor. Gun fires the machine gun, Shell the cannon.", price = 1500 },
            new CraftDef { id = "bike", name = "Dirt bike", desc = "Fast, light and nimble, twin machine guns on the bars.", price = 1500 },
            new CraftDef { id = "arty", name = "Mobile artillery", desc = "Heavy shells, fired from a map.", price = 1800 },
            new CraftDef { id = "clown", name = "Clown", desc = "A horn, a balloon gun, and B to eat whoever is within reach.", price = 1800 },
            new CraftDef { id = "goblin", name = "Goblin", desc = "Small, quick and hard to hit, with a club and rocks to throw.", price = 1800 },
            new CraftDef { id = "raptor", name = "Velociraptor", desc = "A pack hunter: quick as a bike, leaps a long way, and bites.", price = 1800 },
            new CraftDef { id = "jetpack", name = "Jetpack trooper", desc = "A grey alien on a jetpack. From the air, you fly.", price = 2000 },
            new CraftDef { id = "clowncar", name = "Clown car", desc = "A tiny car; stop and five clowns climb out to fight for you.", price = 2200 },
            new CraftDef { id = "bomber2", name = "Two-seat bomber", desc = "A pilot and a gunner, 8 more bombs and a belly turret.", price = 2500 },
            new CraftDef { id = "heli2", name = "Two-seat gunship", desc = "A pilot and a gunner on the chin gun.", price = 2500 },
            new CraftDef { id = "ptero", name = "Pterodactyl", desc = "Hold Jump and the wings take you up.", price = 2600 },
            new CraftDef { id = "wire", name = "Volt", desc = "Throws the current himself: bolts, shock grenades, lightning storms.", price = 2800 },
            new CraftDef { id = "gunslinger", name = "Gunslinger", desc = "Twin pistols and a slow-motion dive.", price = 3000 },
            new CraftDef { id = "transport", name = "Transport helicopter", desc = "Three crew: pilot, two door gunners.", price = 3200 },
            new CraftDef { id = "fiend", name = "Skyjack", desc = "A sky pirate on a jet board with razor darts and cluster bombs.", price = 3200 },
            new CraftDef { id = "ape", name = "Ape", desc = "Picks up people and cars and throws them. Climbs buildings.", price = 4000 },
            new CraftDef { id = "ac130", name = "AC-130 gunship", desc = "Circle a target and rain fire on it.", price = 4200 },
            new CraftDef { id = "grey", name = "Alien grey", desc = "A big head and a ray gun. Hard to hit, jumps high.", price = 4500 },
            new CraftDef { id = "trike", name = "Triceratops", desc = "A living tank on four legs.", price = 4500 },
            new CraftDef { id = "ninjaL", name = "Mind ninja: Sage", desc = "A glowing blade that sends bullets back.", price = 5000 },
            new CraftDef { id = "ninjaD", name = "Mind ninja: Shade", desc = "A red blade, and lightning from the hands.", price = 5000 },
            new CraftDef { id = "slinger", name = "Grapple", desc = "A grapnel launcher on each wrist.", price = 6000 },
            new CraftDef { id = "b52", name = "B-52 heavy bomber", desc = "Eight engines, 40 bombs, and a tail gun.", price = 6000 },
            new CraftDef { id = "devourer", name = "Devourer", desc = "A hulking dark creature that eats the fallen.", price = 7500 },
            new CraftDef { id = "trex", name = "Tyrannosaur", desc = "The biggest thing on two legs.", price = 8000 },
            new CraftDef { id = "neph", name = "Nephilim", desc = "Half angel and half demon, with great dark wings.", price = 8000 },
            new CraftDef { id = "dragon", name = "Dragon", desc = "A mountain with wings. Fire breath and fireballs.", price = 9000 },
            new CraftDef { id = "ufo", name = "Flying saucer", desc = "Twin lasers, a gravity beam, and a cloak.", price = 9000 },
        };

        public static int Bank { get { return PlayerPrefs.GetInt("bb_bank", 0); } set { PlayerPrefs.SetInt("bb_bank", Mathf.Max(0, value)); } }
        public static int Best { get { return PlayerPrefs.GetInt("bb_best", 0); } set { PlayerPrefs.SetInt("bb_best", value); } }
        public static string Craft { get { return PlayerPrefs.GetString("bb_craft", "plane"); } set { PlayerPrefs.SetString("bb_craft", value); } }
        public static string GunOf(string craft) { return PlayerPrefs.GetString("bb_gun_" + craft, "m2"); }
        public static void SetGun(string craft, string gun) { PlayerPrefs.SetString("bb_gun_" + craft, gun); PlayerPrefs.Save(); }
        public static bool OwnsGun(string craft, string gun) { return gun == "m2" || PlayerPrefs.GetInt("bb_own_" + craft + "_" + gun, 0) == 1; }
        public static int Level(string craft, string id) { return PlayerPrefs.GetInt("bb_up_" + craft + "_" + id, 0); }
        static void SetLevel(string craft, string id, int lvl) { PlayerPrefs.SetInt("bb_up_" + craft + "_" + id, lvl); }
        public static int MatesOf(string id) { return PlayerPrefs.GetInt("bb_mate_" + id, 0); }
        public static bool OwnsCraft(string id) { var d = Def(id); return d != null && (d.price == 0 || PlayerPrefs.GetInt("bb_owncraft_" + id, 0) == 1); }

        public static Item ItemOf(string id) { foreach (var it in Catalog) if (it.id == id) return it; return null; }
        public static Gun GunDef(string id) { foreach (var g in Guns) if (g.id == id) return g; return Guns[0]; }
        public static CraftDef Def(string id) { foreach (var c in Crafts) if (c.id == id) return c; return null; }
        public static Mate MateDef(string id) { foreach (var m in Mates) if (m.id == id) return m; return null; }

        /// <summary>The original's costOf: ×1.6 a level up to 8, ×1.25 after, rounded to tens; past the top it keeps going dearer.</summary>
        public static int CostOf(Item it, int lvl)
        {
            int c = Mathf.RoundToInt(it.basePrice * Mathf.Pow(1.6f, Mathf.Min(lvl, 8)) * Mathf.Pow(1.25f, Mathf.Max(0, lvl - 8)) / 10f) * 10;
            if (lvl >= it.max) c = Mathf.RoundToInt(c * (1.6f + (lvl - it.max) * 0.5f));
            return c;
        }
        public static bool CanBuyMore(Item it, int lvl) { return it.endless || lvl < it.max; }

        public static bool Buy(string craft, string id)
        {
            var it = ItemOf(id); if (it == null) return false;
            int lvl = Level(craft, id); if (!CanBuyMore(it, lvl)) return false;
            int c = CostOf(it, lvl); if (c > Bank) return false;
            Bank -= c; SetLevel(craft, id, lvl + 1); PlayerPrefs.Save(); return true;
        }

        public static int GunPrice(string craft, string gun) { return gun == "m2" ? 0 : GunDef(gun).price; }
        public static bool BuyGun(string craft, string gun)
        {
            if (OwnsGun(craft, gun)) { SetGun(craft, gun); return true; }
            int p = GunPrice(craft, gun); if (p > Bank) return false;
            Bank -= p; PlayerPrefs.SetInt("bb_own_" + craft + "_" + gun, 1); SetGun(craft, gun); return true;
        }

        public static int MateCost(string id) { var m = MateDef(id); int have = MatesOf(id); return Mathf.RoundToInt(m.basePrice * (1f + 0.12f * have) / 10f) * 10; }
        public static bool Hire(string id) { int c = MateCost(id); if (c > Bank) return false; Bank -= c; PlayerPrefs.SetInt("bb_mate_" + id, MatesOf(id) + 1); PlayerPrefs.Save(); return true; }
        public static bool Fire(string id) { int have = MatesOf(id); if (have <= 0) return false; PlayerPrefs.SetInt("bb_mate_" + id, have - 1); Bank += Mathf.RoundToInt(MateCost(id) / 2f); PlayerPrefs.Save(); return true; }

        public static bool PickCraft(string id)
        {
            var d = Def(id); if (d == null || !d.available) return false;
            if (!OwnsCraft(id)) { if (d.price > Bank) return false; Bank -= d.price; PlayerPrefs.SetInt("bb_owncraft_" + id, 1); }
            Craft = id; PlayerPrefs.Save(); return true;
        }

        public static void EraseAll()
        {
            var keys = new List<string>();
            Bank = 0; Best = 0; Craft = "plane";
            foreach (var c in Crafts) { foreach (var it in Catalog) PlayerPrefs.DeleteKey("bb_up_" + c.id + "_" + it.id); foreach (var g in Guns) PlayerPrefs.DeleteKey("bb_own_" + c.id + "_" + g.id); PlayerPrefs.DeleteKey("bb_gun_" + c.id); PlayerPrefs.DeleteKey("bb_owncraft_" + c.id); }
            foreach (var m in Mates) PlayerPrefs.DeleteKey("bb_mate_" + m.id);
            PlayerPrefs.Save();
        }

        /// <summary>What the upgrades add up to for an aircraft (statsFrom). Values in the original's units; callers scale by K / U.</summary>
        public class Stats
        {
            public Gun gun; public float maxHp, thrustK, turnK, boostSec, boostRegenK, regen, fireInt, dmg, pen, heatShot, bombReload, blastK, rocketInt, flareCd;
            public int lives, barrels, maxBombs, cluster; public bool napalm;
        }

        public static Stats StatsFor(string craft)
        {
            System.Func<string, float> L = k =>
            {
                var it = ItemOf(k); int raw = Level(craft, k);
                int cap = (it != null && !it.endless) ? it.max : 0;
                return (cap > 0 && raw > cap) ? cap + (raw - cap) * 0.35f : raw;
            };
            System.Func<string, float, float> over = (k, cap) => Mathf.Max(0f, L(k) - cap);
            var g = GunDef(GunOf(craft));
            bool heli = craft == "heli";
            var s = new Stats { gun = g };
            s.napalm = L("napalm") > 0;
            s.maxHp = Mathf.Round((heli ? 125f : 100f) + 25f * L("armor"));
            s.thrustK = 1f + 0.08f * L("engine"); s.turnK = 1f + 0.04f * L("engine");
            s.boostSec = 2.4f * (1f + 0.3f * L("tank")); s.boostRegenK = 1f + 0.2f * Mathf.Min(L("tank"), 6f);
            s.regen = 1.5f * L("repair"); s.lives = 3 + Mathf.RoundToInt(L("life"));
            s.fireInt = Mathf.Max(1f / 40f, g.interval * Mathf.Pow(0.88f, Mathf.Min(L("rate"), 5f)) / (1f + 0.05f * over("rate", 5f)));
            s.dmg = g.dmg * (1f + 0.25f * L("ap")); s.pen = Mathf.Min(0.9f, g.pen + 0.1f * Mathf.Min(L("ap"), 4f) + 0.02f * over("ap", 4f));
            s.heatShot = g.heat * Mathf.Pow(0.85f, Mathf.Min(L("cool"), 4f)) / (1f + 0.08f * over("cool", 4f));
            s.barrels = 1 + Mathf.RoundToInt(L("wing")); s.maxBombs = 8 + 2 * Mathf.RoundToInt(L("rack"));
            s.bombReload = 2.0f * Mathf.Pow(0.85f, Mathf.Min(L("reload"), 4f)) / (1f + 0.06f * over("reload", 4f));
            s.blastK = 1f + 0.15f * Mathf.Min(L("heavy"), 4f) + 0.05f * over("heavy", 4f);
            int cl = Mathf.RoundToInt(L("cluster")); s.cluster = cl > 0 ? 2 + cl : 0;
            float[] ri = { 0f, 1.4f, 1.0f, 0.7f }; s.rocketInt = ri[Mathf.Clamp(Mathf.RoundToInt(L("rockets")), 0, 3)];
            float[] fc = { 0f, 8f, 6f, 4f }; s.flareCd = fc[Mathf.Clamp(Mathf.RoundToInt(L("flares")), 0, 3)];
            return s;
        }

        public static string Fmt(int n) { return n.ToString("N0"); }
    }
}
