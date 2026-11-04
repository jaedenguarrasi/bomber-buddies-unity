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
        /// <summary>The original's GUNS table, entry for entry: 24 weapons with their numbers and who may carry them (the fit rules below).</summary>
        public class Gun { public string id, name, desc, cls; public int price; public float interval, dmg, speed, fall0, range, heat, pen, spread, splash, gravity, w, kick; public bool foot; public int mag = 30, pellets = 1; public bool beam, scope, footOnly, gobOnly, ninjaOnly, slingOnly, apeOnly, melee, noFire, balloon, dual, twoShot, ray, fire; public string only; }
        public class Mate { public string id, name, desc; public int basePrice, n; }
        /// <summary>The original's CRAFTS + CRAFT_PRICE + GV, entry for entry (names, blurbs, prices, categories, showroom stats).</summary>
        public class CraftDef { public string id, name, desc, cat; public int price, seats = 1; public bool available, ground, foot; public float spd, jump, reach, hpK; }

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

        public static readonly Gun[] AllGuns =
        {
            new Gun { id = "rock", name = "Thrown rocks", desc = "The goblin's throwing arm. A heavy stone, lobbed in an arc. Free, and there are always more.", price = 0, interval = 0.62f, dmg = 42f, speed = 330f, fall0 = 420f, range = 640f, heat = 0.07f, pen = 0.15f, gravity = 130f, w = 2.4f, splash = 0f, gobOnly = true, foot = true },
            new Gun { id = "blade", name = "Mind blade", desc = "A blade of light. It cuts through a soldier in one stroke and chops at armour; guard with it to send bullets back.", price = 0, interval = 0.3f, dmg = 0f, speed = 0f, fall0 = 1f, range = 20f, heat = 0.02f, pen = 1f, gravity = 0f, w = 1f, splash = 0f, ninjaOnly = true, melee = true, noFire = true, foot = true },
            new Gun { id = "club", name = "Bone club", desc = "The goblin's club. Swung at whatever is in front of it, and it hits far harder than its size suggests.", price = 0, interval = 0.5f, dmg = 70f, speed = 0f, fall0 = 1f, range = 20f, heat = 0.05f, pen = 0.5f, gravity = 0f, w = 1f, splash = 0f, gobOnly = true, melee = true, foot = true },
            new Gun { id = "spear", name = "Spears and spikes", desc = "Thrown by the ape and the Devourer. One heavy spear at a time, thrown a long way in an arc. Hits aircraft hard.", price = 0, interval = 0.75f, dmg = 95f, speed = 460f, fall0 = 600f, range = 950f, heat = 0.06f, pen = 0.4f, gravity = 70f, w = 3.4f, splash = 0f, footOnly = true, apeOnly = true, foot = true },
            new Gun { id = "lmg", name = "Light machine gun", desc = "The machine gunner's weapon. Fast and steady, full damage to 450, gone by 640.", price = 0, interval = 0.085f, dmg = 9f, speed = 600f, fall0 = 450f, range = 640f, heat = 0.028f, pen = 0.08f, gravity = 34f, w = 1.3f, splash = 0f, footOnly = true, foot = true },
            new Gun { id = "webs", name = "Grapnel rigs", desc = "Grapple's wrists. Hold to fire quick strands of web: each one that lands wraps a little more, and a few will stick a soldier fast (he cannot move or shoot) or bring an aircraft down in a tangle.", price = 0, interval = 0.15f, dmg = 9f, speed = 560f, fall0 = 520f, range = 760f, heat = 0.035f, pen = 0f, gravity = 28f, w = 1.6f, splash = 0f, slingOnly = true, foot = true },
            new Gun { id = "laser", name = "Beam rifle", desc = "The sniper's weapon. A beam, so it hits the instant you fire and does not drop. Very long reach, one heavy pulse at a time.", price = 0, interval = 0.95f, dmg = 185f, speed = 9999f, fall0 = 1700f, range = 1900f, heat = 0.22f, pen = 0.7f, gravity = 0f, w = 2f, splash = 0f, scope = true, beam = true, footOnly = true, foot = true },
            new Gun { id = "sniper", name = "Sniper rifle", desc = "One heavy shot at a time. Full damage to 1100, gone by 1500.", price = 900, interval = 1.15f, dmg = 150f, speed = 1300f, fall0 = 1100f, range = 1500f, heat = 0.2f, pen = 0.55f, gravity = 14f, w = 1.6f, splash = 0f, footOnly = true, foot = true },
            new Gun { id = "carbine", name = "Carbine", desc = "The rocket trooper's sidearm. Light and quick, short reach.", price = 0, interval = 0.13f, dmg = 7f, speed = 560f, fall0 = 300f, range = 460f, heat = 0.03f, pen = 0.04f, gravity = 36f, w = 1.1f, splash = 0f, footOnly = true, foot = true },
            new Gun { id = "balloon", name = "Balloon gun", desc = "A balloon animal that fires balloons: slow, bobbing, and they pop with a bang where they land.", price = 0, interval = 0.5f, dmg = 30f, speed = 260f, fall0 = 300f, range = 420f, heat = 0.06f, pen = 0f, gravity = 120f, w = 2.6f, splash = 10f, footOnly = true, balloon = true, foot = true },
            new Gun { id = "duals", name = "Twin revolvers", desc = "The gunslinger's pair of heavy revolvers: left, right, left, each one a hammer blow. Slow to cock, short reach.", price = 0, interval = 0.42f, dmg = 32f, speed = 720f, fall0 = 360f, range = 560f, heat = 0.085f, pen = 0.15f, gravity = 36f, w = 1.4f, splash = 0f, footOnly = true, dual = true, foot = true },
            new Gun { id = "shotgun", name = "Pump shotgun", desc = "Eight pellets a shot. Wicked up close, thin past 200, gone by 320. Pump between shots.", price = 1100, interval = 0.85f, dmg = 9f, speed = 520f, fall0 = 150f, range = 320f, heat = 0.06f, pen = 0.02f, gravity = 40f, w = 1.3f, splash = 0f, pellets = 8, spread = 0.075f, kick = 1.6f, footOnly = true, foot = true },
            new Gun { id = "sawn", name = "Sawn-off", desc = "Two barrels, ten pellets each, and a spread you could hide behind. Nothing past 220. Slow to reload.", price = 1500, interval = 1.2f, dmg = 8f, speed = 480f, fall0 = 90f, range = 220f, heat = 0.1f, pen = 0.02f, gravity = 44f, w = 1.4f, splash = 0f, pellets = 10, spread = 0.13f, kick = 2.2f, footOnly = true, twoShot = true, foot = true },
            new Gun { id = "volt", name = "Lightning", desc = "Bolts thrown from bare hands. No gun to carry, no magazine, just heat. Chains a little on a hit.", price = 0, interval = 0.17f, dmg = 15f, speed = 820f, fall0 = 300f, range = 430f, heat = 0.045f, pen = 0.1f, gravity = 0f, w = 1.2f, splash = 0f, cls = "wire", footOnly = true, foot = true },
            new Gun { id = "darts", name = "Razor darts", desc = "A wrist launcher: fast, quiet darts in a steady stream.", price = 0, interval = 0.13f, dmg = 8f, speed = 920f, fall0 = 260f, range = 380f, heat = 0.03f, pen = 0.04f, gravity = 30f, w = 0.8f, splash = 0f, cls = "fiend", footOnly = true, foot = true },
            new Gun { id = "breath", name = "Fire breath", desc = "A river of fire from the jaws. Short reach, sets everything it touches burning.", price = 0, interval = 0.04f, dmg = 6f, speed = 280f, fall0 = 140f, range = 240f, heat = 0.012f, pen = 0.05f, gravity = 70f, w = 2.6f, splash = 7f, cls = "dragon", fire = true, foot = true },
            new Gun { id = "bite", name = "Teeth and claws", desc = "What it was born with.", price = 0, interval = 0.6f, dmg = 40f, speed = 0f, fall0 = 20f, range = 20f, heat = 0f, pen = 0.3f, gravity = 0f, w = 1f, splash = 0f, apeOnly = true, melee = true, foot = true },
            new Gun { id = "ray", name = "Ray gun", desc = "The grey's weapon: a bolt of light that flies dead straight and fast.", price = 0, interval = 0.28f, dmg = 17f, speed = 1800f, fall0 = 900f, range = 1200f, heat = 0.05f, pen = 0.3f, gravity = 0f, w = 1.2f, splash = 0f, footOnly = true, ray = true, foot = true },
            new Gun { id = "rifle", name = "Service rifle", desc = "The soldier's own weapon. Accurate single shots, full damage to 420, gone by 600.", price = 0, interval = 0.17f, dmg = 11f, speed = 620f, fall0 = 420f, range = 600f, heat = 0.03f, pen = 0.05f, gravity = 34f, w = 1.2f, splash = 0f, footOnly = true, foot = true },
            new Gun { id = "m2", name = ".50 cal machine guns", desc = "Fast and light. Full damage to 480, gone by 640.", price = 0, interval = 0.085f, dmg = 10f, speed = 640f, fall0 = 480f, range = 640f, heat = 0.042f, pen = 0f, gravity = 40f, w = 1.5f, splash = 0f, foot = false },
            new Gun { id = "mini", name = "Minigun", desc = "Shreds troops up close. Full damage to 280, gone by 460.", price = 2500, interval = 0.038f, dmg = 6f, speed = 620f, fall0 = 280f, range = 460f, heat = 0.021f, pen = 0f, gravity = 46f, w = 1.2f, splash = 0f, foot = false },
            new Gun { id = "m20", name = "20mm cannon", desc = "Hits harder. Full damage to 560, reaches 780.", price = 1500, interval = 0.16f, dmg = 24f, speed = 700f, fall0 = 560f, range = 780f, heat = 0.07f, pen = 0.2f, gravity = 30f, w = 2f, splash = 0f, foot = false },
            new Gun { id = "m25", name = "25mm Bushmaster", desc = "Long reach and armor bite. Full damage to 800, reaches 1,050.", price = 4000, interval = 0.24f, dmg = 42f, speed = 760f, fall0 = 800f, range = 1050f, heat = 0.095f, pen = 0.4f, gravity = 24f, w = 2.4f, splash = 9f, foot = false },
            new Gun { id = "m30", name = "30mm chain gun", desc = "Slow explosive shells. Full damage to 920, reaches 1,200.", price = 9000, interval = 0.36f, dmg = 70f, speed = 720f, fall0 = 920f, range = 1200f, heat = 0.13f, pen = 0.55f, gravity = 28f, w = 2.9f, splash = 15f, foot = false },
        };
        /// <summary>The aircraft guns (the hangar's gun tab for anything that flies) and the foot guns, as views of the one table.</summary>
        public static Gun[] Guns { get { var l = new List<Gun>(); foreach (var g in AllGuns) if (!g.foot) l.Add(g); return l.ToArray(); } }
        public static Gun[] FootGuns { get { var l = new List<Gun>(); foreach (var g in AllGuns) if (g.foot) l.Add(g); return l.ToArray(); } }

        /// <summary>The soldier classes' bodies (GV): speed, toughness, jump, and what the class can do.</summary>
        public class Kit { public string id, gun; public float spd, hpK, jump; public bool rocket, medic, jetpack, scope; public string melee, body; public float reach = 2.2f, meleeDmg = 40f, height = 1.8f; }
        public static readonly Kit[] Kits =
        {
            new Kit { id = "soldier", gun = "rifle", spd = 36f, hpK = 0.4f, jump = 92f },
            new Kit { id = "gunner", gun = "lmg", spd = 30f, hpK = 0.7f, jump = 80f },
            new Kit { id = "sniper", gun = "laser", spd = 36f, hpK = 0.3f, jump = 92f, scope = true },
            new Kit { id = "rocketeer", gun = "carbine", spd = 34f, hpK = 0.45f, jump = 90f, rocket = true },
            new Kit { id = "medic", gun = "carbine", spd = 38f, hpK = 0.5f, jump = 92f, medic = true },
            new Kit { id = "jetpack", gun = "rifle", spd = 36f, hpK = 0.35f, jump = 92f, jetpack = true },
            // the monsters (GV goblin/ape/raptor/clown): no gun, a swing or a bite on Fire, their own bodies
            new Kit { id = "goblin", gun = "rifle", spd = 58f, hpK = 0.3f, jump = 120f, melee = "club", body = "gob", reach = 2.2f, meleeDmg = 40f, height = 1.3f },
            new Kit { id = "ape", gun = "rifle", spd = 64f, hpK = 1.4f, jump = 140f, melee = "fist", body = "ape", reach = 3.4f, meleeDmg = 90f, height = 2.6f },
            new Kit { id = "raptor", gun = "rifle", spd = 120f, hpK = 0.5f, jump = 150f, melee = "bite", body = "raptor", reach = 2.6f, meleeDmg = 55f, height = 1.6f },
            new Kit { id = "gunslinger", gun = "duals", spd = 40f, hpK = 0.45f, jump = 95f },
            new Kit { id = "wire", gun = "volt", spd = 38f, hpK = 0.5f, jump = 95f },
            new Kit { id = "monkey", gun = "ray", spd = 60f, hpK = 0.25f, jump = 150f, height = 1.1f },
            new Kit { id = "grey", gun = "rifle", spd = 44f, hpK = 0.35f, jump = 140f, height = 1.6f },
            new Kit { id = "ninjaL", gun = "rifle", spd = 92f, hpK = 1.1f, jump = 110f, melee = "blade", body = null, reach = 2.6f, meleeDmg = 70f, height = 1.8f },
            new Kit { id = "ninjaD", gun = "rifle", spd = 92f, hpK = 1.1f, jump = 110f, melee = "blade", body = null, reach = 2.6f, meleeDmg = 70f, height = 1.8f },
            new Kit { id = "neph", gun = "rifle", spd = 56f, hpK = 2.2f, jump = 220f, melee = "fist", body = "stalk", reach = 3.4f, meleeDmg = 120f, height = 3.0f },
            new Kit { id = "devourer", gun = "rifle", spd = 50f, hpK = 2.6f, jump = 100f, melee = "bite", body = "ape", reach = 3.6f, meleeDmg = 140f, height = 3.2f },
            new Kit { id = "trex", gun = "rifle", spd = 70f, hpK = 6f, jump = 60f, melee = "bite", body = "trex", reach = 7f, meleeDmg = 260f, height = 7f },
            new Kit { id = "slinger", gun = "rifle", spd = 44f, hpK = 0.45f, jump = 120f, melee = "fist", body = null, reach = 2.2f, meleeDmg = 45f, height = 1.8f },
            new Kit { id = "clown", gun = "rifle", spd = 40f, hpK = 0.9f, jump = 100f, melee = "horn", body = null, reach = 2.6f, meleeDmg = 45f, height = 1.9f },
        };
        public static Kit KitOf(string id) { foreach (var k in Kits) if (k.id == id) return k; return null; }
        public static bool IsFoot(string craft) { return KitOf(craft) != null; }
        public static bool IsRide(string craft) { return craft == "tank" || craft == "scout" || craft == "bike" || craft == "clowncar" || craft == "trike" || craft == "arty"; }
        public static Gun FootGunDef(string id) { return GunDef(id); }
        /// <summary>Which guns a class may carry: its own, plus the general foot guns (gunFits).</summary>
        static bool IsDino(string c) { var d = Def(c); return d != null && d.cat == "Dinosaurs"; }
        /// <summary>gunFits, verbatim: a gun with a class goes only to that class; ninjas take only ninja guns; the Grapple only his rigs; the goblin his rocks and club; apes and the Devourer their spears; dinosaurs bite; the monkey has the ray gun; everyone else takes what is not reserved (foot guns only on foot).</summary>
        public static bool GunFits(string craft, Gun g)
        {
            var def = Def(craft); bool foot = def != null && def.foot;
            if (g.cls != null || craft == "dragon") return g.cls == craft;
            if (craft == "ninjaL" || craft == "ninjaD") return g.ninjaOnly;
            if (g.ninjaOnly) return false;
            if (craft == "slinger") return g.slingOnly;
            if (g.slingOnly) return false;
            if (craft == "goblin") return g.gobOnly;
            if (g.gobOnly) return false;
            if (craft == "ape" || craft == "devourer") return g.apeOnly;
            if (IsDino(craft)) return g.id == "bite";
            if (craft == "monkey") return g.id == "ray";
            return !g.apeOnly && (!g.footOnly || foot);
        }
        /// <summary>defaultGun, verbatim.</summary>
        public static string DefaultGun(string c)
        {
            if (c == "clown") return "balloon"; if (c == "ninjaL" || c == "ninjaD") return "blade"; if (c == "goblin") return "club"; if (c == "devourer") return "spear"; if (c == "slinger") return "webs"; if (c == "monkey") return "ray";
            if (IsDino(c)) return "bite"; if (c == "ape") return "spear"; if (c == "gunner") return "lmg"; if (c == "sniper") return "laser"; if (c == "rocketeer") return "carbine"; if (c == "gunslinger") return "duals"; if (c == "medic") return "carbine"; if (c == "wire") return "volt"; if (c == "fiend") return "darts"; if (c == "dragon") return "breath"; if (c == "grey") return "ray"; if (c == "neph") return "spear";
            var d = Def(c); return d != null && d.foot ? "rifle" : "m2";
        }
        public static string DefaultGun2(string c) { return c == "heli2" || c == "transport" || c == "ac130" ? "m20" : "m2"; }

        public static readonly Mate[] Mates =
        {
            new Mate { id = "squad", name = "Rifle squad", desc = "Four soldiers on foot. Cheap, and good at soaking up attention.", basePrice = 400, n = 4 },
            new Mate { id = "car", name = "Scout car", desc = "Fast, with a mounted machine gun.", basePrice = 700, n = 1 },
            new Mate { id = "tank", name = "Tank", desc = "Slow and tough. Its cannon cracks bunkers and armor.", basePrice = 1600, n = 1 },
            new Mate { id = "heli", name = "Gunship", desc = "Hovers near the fight and works targets with its chin gun.", basePrice = 2400, n = 1 },
            new Mate { id = "medic", name = "Field medic", desc = "Follows the squad and patches up anyone hurt, even the ones dragging themselves along, back to full health. Patches you up too.", basePrice = 900, n = 1 },
            new Mate { id = "wing", name = "Wingman", desc = "A fighter that makes strafing and bombing runs on whatever is nearest.", basePrice = 2000, n = 1 },
        };

        /// <summary>The roster. Only the first two fly in this build; the rest are shown so you can see what is coming (and what it costs).</summary>
        public static readonly CraftDef[] Crafts =
        {
            new CraftDef { id = "plane", name = "Fighter-bomber", desc = "Fast, loops, dive-bombs. Guns fire straight ahead.", price = 0, available = true, cat = "Aircraft", ground = false, foot = false, seats = 1, spd = 175f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "heli", name = "Attack helicopter", desc = "Hovers and backs up. The chin gun tracks targets ahead of you by itself.", price = 0, available = true, cat = "Aircraft", ground = false, foot = false, seats = 1, spd = 150f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "b52", name = "B-52 heavy bomber", desc = "3D game only. Eight engines on a wing that goes on forever, 40 bombs, and a tail gun. Hold Bomb and it lays a carpet: a stick of bombs walks across the ground under you. Slow to turn, hard to bring down. Fly it with the AI on the tail gun, or swap seats and shoot.", price = 6000, available = true, cat = "Aircraft", ground = false, foot = false, seats = 2, spd = 160f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "bomber2", name = "Two-seat bomber", desc = "3D game only. A pilot and a gunner. Heavier, carries 8 more bombs and has a belly turret that can shoot in any direction. Fly it while the AI gunner works the turret, or swap seats and let the AI fly while you shoot and bomb.", price = 2500, available = true, cat = "Aircraft", ground = false, foot = false, seats = 2, spd = 160f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "clown", name = "Clown", desc = "A horn that blows everyone off their feet (Bomb), a balloon gun whose balloons pop where they land, and B to eat whoever is within reach.", price = 1800, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 72f, jump = 125f, reach = 400f, hpK = 0.9f },
            new CraftDef { id = "clowncar", name = "Clown car", desc = "A tiny car, much faster than it looks. Stop dead and press Clowns out: five clowns climb out to fight for you.", price = 2200, available = true, cat = "Ground", ground = true, foot = false, seats = 1, spd = 150f, jump = 0f, reach = 500f, hpK = 0.95f },
            new CraftDef { id = "gunslinger", name = "Gunslinger", desc = "Twin pistols, fired left and right as fast as you can pull, and quick on his feet. Aim and press Jump to dive in slow motion, guns blazing.", price = 3000, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 64f, jump = 130f, reach = 430f, hpK = 0.85f },
            new CraftDef { id = "medic", name = "Soldier: medic", desc = "A carbine, and hands that put people back together. Stand by a hurt teammate and press B to patch them up to full health, even one dragging himself along without a leg. Slowly heals himself.", price = 1200, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 38f, jump = 92f, reach = 430f, hpK = 0.5f },
            new CraftDef { id = "wire", name = "Volt", desc = "A power-line worker who walked out of a substation fire able to throw the current himself. Fire: bolts (they arc to a second man). Tap L1: a shock grenade that sticks and knocks everyone near it down. Hold L1: a polarity wall that eats anything coming at you from the front. Bomb: a shock wave; Bomb while aiming: a rocket of lightning; hold Bomb: a lightning storm; Bomb in the air: a thunder drop. Punch: a charged fist. B over a downed man or next to a vehicle: drain it to heal. Hold Jump in the air: the static glide.", price = 2800, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 72f, jump = 165f, reach = 420f, hpK = 1.25f },
            new CraftDef { id = "fiend", name = "Skyjack", desc = "A sky pirate in a navy flight suit with yellow trim, standing on a jet board. Jump, then hold Jump: the board lights and you fly. Razor darts from the wrist, and cluster bombs with a blast twice the size of a grenade. He laughs when he throws one.", price = 3200, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 66f, jump = 100f, reach = 520f, hpK = 1.15f },
            new CraftDef { id = "grey", name = "Alien grey", desc = "A small grey with a big head and a ray gun that fires dead-straight bolts of light. Hard to hit, jumps high. Aim and press Jump to dive.", price = 4500, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 40f, jump = 130f, reach = 430f, hpK = 0.34f },
            new CraftDef { id = "neph", name = "Nephilim", desc = "Tall, slender and built like something out of an old story, half angel and half demon, with great dark wings, horns and burning eyes: huge leaps, claws that tear through anything, and it heals on what it rips apart. Plays like the ape.", price = 8000, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 118f, jump = 190f, reach = 620f, hpK = 1.4f },
            new CraftDef { id = "dragon", name = "Dragon", desc = "3D game only. A mountain with wings. Hovers, glides and lands anywhere, like a helicopter. Gun: a stream of fire that sets the ground alight. Bomb: a fireball. Big, slow to turn, and very hard to bring down.", price = 9000, available = true, cat = "Aircraft", ground = false, foot = false, seats = 1, spd = 175f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "ufo", name = "Flying saucer", desc = "Hovers and slides any way you like, like a helicopter. Gun fires twin lasers wherever you aim. Bomb holds a gravity beam: whatever is under you floats up, and drops when you let go. Boost cloaks you for five seconds: enemies lose you until you fire. Plays in 2D too.", price = 9000, available = true, cat = "Aircraft", ground = false, foot = false, seats = 1, spd = 150f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "transport", name = "Transport helicopter", desc = "3D game only. Three crew: the pilot flies and fires the nose gun, and two door gunners work the side miniguns. The left door gun is the gunner seat (swap into it, or a friend takes it); the right door gun is crewed by the AI unless a second friend climbs in.", price = 3200, available = true, cat = "Aircraft", ground = false, foot = false, seats = 2, spd = 150f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "ac130", name = "AC-130 gunship", desc = "3D game only. Three crew. The pilot flies the orbit; the gunner seat works the side cannon; the big 105 howitzer is crewed by the AI unless a second friend climbs in. Circle a target and rain fire on it.", price = 4200, available = true, cat = "Aircraft", ground = false, foot = false, seats = 2, spd = 150f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "heli2", name = "Two-seat gunship", desc = "3D game only. A pilot and a gunner. Fly it and fire forward while the AI gunner works the chin gun, or swap seats: the AI holds a firing position while you aim the gun.", price = 2500, available = true, cat = "Aircraft", ground = false, foot = false, seats = 2, spd = 150f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "drone", name = "Bomber drone", desc = "3D game only. Flies like an FPV quad and drops grenades. Best with a Bluetooth controller: left stick is throttle and turn, right stick tilts. On touch the stick flies it and the slider holds your height, or switch on tilt steering.", price = 800, available = true, cat = "Aircraft", ground = false, foot = false, seats = 1, spd = 210f, jump = 0f, reach = 0f, hpK = 1f },
            new CraftDef { id = "ninjaL", name = "Mind ninja: Sage", desc = "3D game only. A glowing blade: R2 slashes, hold L2 to guard and send bullets back where they came from, R1 throws the blade and it comes back to your hand. X pushes everything in front of you away with your mind, L1 drags one enemy to your feet. B heals you and every teammate near you. Jump again and again in the air: each jump flips you higher.", price = 5000, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 92f, jump = 110f, reach = 700f, hpK = 1.1f },
            new CraftDef { id = "ninjaD", name = "Mind ninja: Shade", desc = "3D game only. A red blade: R2 slashes, hold L2 to guard and send bullets back, R1 throws the blade and it comes back. X pushes everything away with your mind, L1 drags one enemy to your feet. Hold B to pour lightning from your hands into everything in front of you. Jump again and again: each jump flips you higher.", price = 5000, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 92f, jump = 110f, reach = 700f, hpK = 1.1f },
            new CraftDef { id = "goblin", name = "Goblin", desc = "3D game only. A nasty little scrapper: small, quick and hard to hit, with a club up close and rocks to throw. Tap Jump twice for a scrambling double jump. Barely any armor, so fight in the pack and keep moving.", price = 0, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 80f, jump = 112f, reach = 420f, hpK = 0.34f },
            new CraftDef { id = "devourer", name = "Devourer", desc = "3D game only. L2 devours, R2 throws what it is holding (or spikes when it holds nothing). L1 and R1 are its two lines: they stick to whatever the crosshair is on, and with both on the same enemy or vehicle you swing it wherever you look and fling it by letting go. A hulking dark creature with everything Grapple can do (line, walls, flips, glide, web-grab) and the ape's strength: X is a smack that sends things flying, X in the air is a dive stomp, and anything in arm's reach gets grabbed. It eats the fallen (the Devour button, or L1): each meal makes it faster and stronger for a while, up to three deep. Throws bone spikes at range.", price = 7500, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 120f, jump = 170f, reach = 650f, hpK = 1.2f },
            new CraftDef { id = "slinger", name = "Grapple", desc = "3D game only. A rescue-rig climber with a grapnel launcher on each wrist. Two lines: L1 fires the LEFT line, R1 the RIGHT, and they hook whatever the crosshair is on. Put both on the same enemy or vehicle and it is yours: look around to swing it, let go of both to fling it. Hold one to swing, tap one to zip to where it sticks, hold both and press Jump to slingshot, and push the stick sideways to spin between them. A web that catches an enemy yanks it to your feet when you let go; one on each web and you smash them together. R2 fires sticky web strands (enough of them wrap a soldier or bring down a helicopter). B catches, whirls and throws, yanks a tree or rubble into your hands, or, held with nothing to catch, winds up a web ball that wraps everything where it bursts. X is a jab-cross-uppercut combo, a flying kick while on a web, and a dive stomp in the air. Keep tapping Jump in the air for a little more height and a different flip each time; hold Boost in the air to spread out and glide. Hold Jump to grip a wall, then the stick runs you across it, round its corners and up on to the roof; let go to leap the way you are steering or looking. Carries grenades (L1, or the small Nade button). An acrobat with a grapple line. Hold the Line button to hook on to a building, tree or hillside and swing; let go to fling yourself. Jump while hanging reels you in. Run into a wall to run up it, jump to kick off, and you can fight from the rooftops. Double jump. Lightly armored.", price = 6000, available = true, cat = "Heroes", ground = true, foot = true, seats = 1, spd = 84f, jump = 170f, reach = 470f, hpK = 1.05f },
            new CraftDef { id = "raptor", name = "Velociraptor", desc = "3D game only. A pack hunter: quick as a bike, leaps a long way, and bites. L1 is a full-circle tail whip; X rips with the hand claws. Thin-skinned.", price = 1800, available = true, cat = "Dinosaurs", ground = true, foot = true, seats = 1, spd = 152f, jump = 130f, reach = 300f, hpK = 0.6f },
            new CraftDef { id = "trike", name = "Triceratops", desc = "3D game only. A living tank on four legs with three horns and a frill. Slow to turn, very hard to kill, and anything you run into goes over.", price = 4500, available = true, cat = "Dinosaurs", ground = true, foot = true, seats = 1, spd = 80f, jump = 40f, reach = 300f, hpK = 4.2f },
            new CraftDef { id = "trex", name = "Tyrannosaur", desc = "3D game only. The biggest thing on two legs: a bite that takes half a tank, a stomp that flattens a squad, a tail whip (L1) that clears everything round you, and a hide that shrugs off rifles.", price = 8000, available = true, cat = "Dinosaurs", ground = true, foot = true, seats = 1, spd = 92f, jump = 70f, reach = 300f, hpK = 8.5f },
            new CraftDef { id = "ptero", name = "Pterodactyl", desc = "3D game only. Jump, then hold Jump and the wings take you up: A climbs, B descends, and you swoop with the stick. Dive on someone and hit them at speed.", price = 2600, available = true, cat = "Dinosaurs", ground = true, foot = true, seats = 1, spd = 60f, jump = 110f, reach = 300f, hpK = 0.9f },
            new CraftDef { id = "raptorMount", name = "Raptor (ridden)", desc = "3D game only. A soldier in the saddle of a velociraptor: the fastest thing on the ground, with a rifle up top.", price = 2400, available = false, cat = "Mounts", ground = true, foot = false, seats = 1, spd = 150f, jump = 0f, reach = 520f, hpK = 1.2f },
            new CraftDef { id = "trikeMount", name = "Triceratops (ridden)", desc = "3D game only. A soldier riding a triceratops: slow, armoured, and it goes through anything.", price = 5500, available = false, cat = "Mounts", ground = true, foot = false, seats = 1, spd = 85f, jump = 0f, reach = 520f, hpK = 5f },
            new CraftDef { id = "trexMount", name = "Tyrannosaur (ridden)", desc = "3D game only. A soldier riding a tyrannosaur. Nothing on the island wants to be in its road.", price = 9500, available = false, cat = "Mounts", ground = true, foot = false, seats = 1, spd = 95f, jump = 0f, reach = 520f, hpK = 9f },
            new CraftDef { id = "monkey", name = "Space monkey", desc = "A rabid astronaut test monkey in a little spacesuit. Tiny, quick as anything, leaps like a flea and bites. Carries a ray gun it stole from the greys. Hard to hit, easy to kill.", price = 900, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 128f, jump = 150f, reach = 340f, hpK = 0.3f },
            new CraftDef { id = "ape", name = "Ape", desc = "3D game only. B picks up a person, a car, a tree or a lump of rubble, and B again throws it where you look. Climbs buildings: leap at a wall or run into it, push up to climb, Jump to leap off. L1 is a smack that sends people flying and wrecks what it hits. Press Leap again in the air for a front flip into a ground stomp. Four times as fast as a soldier on the ground. Anything within arm's reach gets grabbed and smashed with his hands; at range, Gun throws heavy spears that can bring down aircraft. Jump leaps a very long way, and landing from a big leap pounds the ground and hurts everything near. The Shell button is a power leap.", price = 4000, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 144f, jump = 190f, reach = 650f, hpK = 0.95f },
            new CraftDef { id = "gunner", name = "Soldier: machine gunner", desc = "3D game only. Carries a light machine gun and more armor than a rifleman. Slower on his feet.", price = 900, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 30f, jump = 78f, reach = 480f, hpK = 0.62f },
            new CraftDef { id = "sniper", name = "Soldier: sniper", desc = "3D game only. A long rifle that hits hard from very far away, and a strong scope on zoom. Fragile.", price = 1200, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 34f, jump = 92f, reach = 1100f, hpK = 0.3f },
            new CraftDef { id = "rocketeer", name = "Soldier: rocket trooper", desc = "3D game only. The Shell button fires an anti-tank rocket in a straight line. A carbine for everything else.", price = 1500, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 32f, jump = 86f, reach = 430f, hpK = 0.45f },
            new CraftDef { id = "jetpack", name = "Jetpack trooper (grey)", desc = "3D game only. A grey alien on a jetpack: big black eyes, grey skin and nothing else, with a rifle. Jump, then hold Jump in the air and the pack lights: from there you fly. Double-tap Jump to cut the pack and drop; hold Jump to relight. Push forward to lean into a fast wingsuit glide that goes where you are looking, dive to build speed, let the stick go and you hover on the spot at any height. Jump climbs, the slider (or B on a controller) descends, Boost is the afterburner. Lightly armored.", price = 2000, available = true, cat = "Creatures", ground = true, foot = true, seats = 1, spd = 38f, jump = 100f, reach = 430f, hpK = 0.34f },
            new CraftDef { id = "soldier", name = "Soldier: rifleman", desc = "3D game only. On foot with a rifle and grenades. Small and hard to hit, but slow: use houses and craters as cover. Boost is a sprint. With a controller the left stick walks and sidesteps, the right stick looks and aims.", price = 0, available = true, cat = "Soldiers", ground = true, foot = true, seats = 1, spd = 36f, jump = 92f, reach = 430f, hpK = 0.4f },
            new CraftDef { id = "tank", name = "Battle tank", desc = "3D game only. Heavy armor. The turret finds targets by itself: Gun fires the machine gun, Shell fires the cannon.", price = 1500, available = true, cat = "Ground", ground = true, foot = false, seats = 1, spd = 78f, jump = 0f, reach = 640f, hpK = 3.2f },
            new CraftDef { id = "bike", name = "Dirt bike", desc = "3D game only. Fast, light and nimble: it goes anywhere, turns on a dime and slides round corners, with twin machine guns on the bars. One good hit and you are walking.", price = 1500, available = true, cat = "Ground", ground = true, foot = false, seats = 1, spd = 240f, jump = 0f, reach = 520f, hpK = 0.32f },
            new CraftDef { id = "scout", name = "Scout car", desc = "3D game only. Fast and light, with a mounted gun and a grenade launcher on the Shell button.", price = 0, available = true, cat = "Ground", ground = true, foot = false, seats = 1, spd = 165f, jump = 0f, reach = 600f, hpK = 1.15f },
            new CraftDef { id = "arty", name = "Mobile artillery", desc = "3D game only. Heavy shells, fired from a map. The view button opens the fire-control map: move the marker with the stick (or tap the map), and Shell drops a round on it, in any direction, out to 1,700. Drive it from the other views.", price = 1800, available = true, cat = "Ground", ground = true, foot = false, seats = 1, spd = 56f, jump = 0f, reach = 520f, hpK = 1.7f },
        };

        public static int Bank { get { return PlayerPrefs.GetInt("bb_bank", 0); } set { PlayerPrefs.SetInt("bb_bank", Mathf.Max(0, value)); } }
        public static int Campaign { get { return PlayerPrefs.GetInt("bb_campaign", 0); } set { PlayerPrefs.SetInt("bb_campaign", value); } }
        public static int Best { get { return PlayerPrefs.GetInt("bb_best", 0); } set { PlayerPrefs.SetInt("bb_best", value); } }
        /// <summary>A mode that decides what you fly (Abduction: raid): overrides the chosen craft for the match.</summary>
        public static string Forced;
        public static string Craft { get { return Forced ?? PlayerPrefs.GetString("bb_craft", "plane"); } set { PlayerPrefs.SetString("bb_craft", value); } }
        public static string GunOf(string craft) { return PlayerPrefs.GetString("bb_gun_" + craft, DefaultGun(craft)); }
        public static void SetGun(string craft, string gun) { PlayerPrefs.SetString("bb_gun_" + craft, gun); PlayerPrefs.Save(); }
        public static bool OwnsGun(string craft, string gun) { return gun == DefaultGun(craft) || PlayerPrefs.GetInt("bb_own_" + craft + "_" + gun, 0) == 1; }
        public static int Level(string craft, string id) { return PlayerPrefs.GetInt("bb_up_" + craft + "_" + id, 0); }
        static void SetLevel(string craft, string id, int lvl) { PlayerPrefs.SetInt("bb_up_" + craft + "_" + id, lvl); }
        public static int MatesOf(string id) { return PlayerPrefs.GetInt("bb_mate_" + id, 0); }
        public static bool OwnsCraft(string id) { var d = Def(id); return d != null && (d.price == 0 || PlayerPrefs.GetInt("bb_owncraft_" + id, 0) == 1); }

        public static Item ItemOf(string id) { foreach (var it in Catalog) if (it.id == id) return it; return null; }
        public static Gun GunDef(string id) { foreach (var g in AllGuns) if (g.id == id) return g; return GunDef("m2"); }
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
            bool over = lvl >= it.max;
            Bank -= c; SetLevel(craft, id, lvl + 1); PlayerPrefs.Save(); Sfx.Play("ui_buy", 0.6f, over ? 1.15f : 1f);
            if (over) Hangar.Flash("You rebel");   /* past the top, it keeps going: dearer each time, and a word for it */
            return true;
        }

        public static int GunPrice(string craft, string gun) { return gun == DefaultGun(craft) ? 0 : gun == "m2" ? 600 : GunDef(gun).price; }
        public static bool BuyGun(string craft, string gun)
        {
            if (OwnsGun(craft, gun)) { SetGun(craft, gun); return true; }
            int p = GunPrice(craft, gun); if (p > Bank) return false;
            Bank -= p; PlayerPrefs.SetInt("bb_own_" + craft + "_" + gun, 1); SetGun(craft, gun); Sfx.Play("ui_buy", 0.6f); return true;
        }

        public static int MateCost(string id) { var m = MateDef(id); int have = MatesOf(id); return Mathf.RoundToInt(m.basePrice * (1f + 0.12f * have) / 10f) * 10; }
        public static bool Hire(string id) { int c = MateCost(id); if (c > Bank) return false; Sfx.Tone(700f, 0.09f, 0.08f, "triangle"); Bank -= c; PlayerPrefs.SetInt("bb_mate_" + id, MatesOf(id) + 1); PlayerPrefs.Save(); return true; }
        public static int MateCostAt(string id, int have) { var m = MateDef(id); return Mathf.RoundToInt(m.basePrice * (1f + 0.12f * have) / 10f) * 10; }
        /// <summary>Let one go: half of what the last one cost comes back (the original's unmate).</summary>
        public static bool Fire(string id) { int have = MatesOf(id); if (have <= 0) return false; PlayerPrefs.SetInt("bb_mate_" + id, have - 1); Bank += Mathf.RoundToInt(MateCostAt(id, have - 1) / 2f); PlayerPrefs.Save(); return true; }
        public static int MatesTotal { get { int t = 0; foreach (var m in Mates) t += MatesOf(m.id) * m.n; return t; } }

        // ---- the original's attachments (ATT), the gunner's weapon (gun2), the launcher pick (sec) and the crew pick ----
        public class Att { public string id, name, desc; public int price; }
        public static readonly Att[] Atts =
        {
            new Att { id = "optic", name = "Optic", desc = "A sight with real magnification: aim in tighter.", price = 600 },
            new Att { id = "grip", name = "Foregrip", desc = "Steadier: the shots land closer together.", price = 500 },
            new Att { id = "mag", name = "Extended mag", desc = "Runs cooler: a third longer between overheats.", price = 700 },
            new Att { id = "muzzle", name = "Compensator", desc = "Two thirds of the kick taken out of every shot.", price = 800 },
        };
        /// <summary>attFits: attachments go on the real firearms a soldier carries, not on blades, rocks, breath or the aircraft guns.</summary>
        public static bool AttFits(string gun) { var g = GunDef(gun); return g.foot && !g.melee && !g.noFire && !g.ray && !g.fire && !g.balloon && gun != "volt" && gun != "webs" && gun != "spear" && gun != "rock"; }
        public static bool AttOwned(string gun, string att) { return PlayerPrefs.GetInt("bb_attown_" + gun + "_" + att, 0) == 1; }
        public static bool AttOn(string gun, string att) { return PlayerPrefs.GetInt("bb_att_" + gun + "_" + att, 0) == 1; }
        public static bool ToggleAtt(string gun, string att)
        {
            Att a = null; foreach (var x in Atts) if (x.id == att) a = x; if (a == null) return false;
            if (!AttOwned(gun, att)) { if (a.price > Bank) return false; Bank -= a.price; PlayerPrefs.SetInt("bb_attown_" + gun + "_" + att, 1); PlayerPrefs.SetInt("bb_att_" + gun + "_" + att, 1); Sfx.Play("ui_buy", 0.6f); PlayerPrefs.Save(); return true; }
            PlayerPrefs.SetInt("bb_att_" + gun + "_" + att, AttOn(gun, att) ? 0 : 1); PlayerPrefs.Save(); Sfx.Tone(520f, 0.06f, 0.06f, "triangle"); return true;
        }
        public static string Gun2Of(string craft) { return PlayerPrefs.GetString("bb_gun2_" + craft, DefaultGun2(craft)); }
        public static bool OwnsGun2(string craft, string gun) { return gun == DefaultGun2(craft) || OwnsGun(craft, gun); }
        public static bool BuyGun2(string craft, string gun)
        {
            if (!OwnsGun2(craft, gun)) { int p = GunPrice(craft, gun); if (p > Bank) return false; Bank -= p; PlayerPrefs.SetInt("bb_own_" + craft + "_" + gun, 1); Sfx.Play("ui_buy", 0.6f); }
            PlayerPrefs.SetString("bb_gun2_" + craft, gun); PlayerPrefs.Save(); return true;
        }
        public static string Sec { get { return PlayerPrefs.GetString("bb_sec", "nade"); } set { PlayerPrefs.SetString("bb_sec", value); } }
        public static string Crew { get { return PlayerPrefs.GetString("bb_crew", "soldier"); } set { PlayerPrefs.SetString("bb_crew", value); } }
        /// <summary>Unlock without switching to it (the showroom's Unlock button).</summary>
        public static bool Unlock(string id)
        {
            var d = Def(id); if (d == null || OwnsCraft(id)) return false; if (d.price > Bank) return false;
            Bank -= d.price; PlayerPrefs.SetInt("bb_owncraft_" + id, 1); PlayerPrefs.Save(); Sfx.Play("ui_buy", 0.7f); return true;
        }

        public static bool PickCraft(string id)
        {
            var d = Def(id); if (d == null || !d.available) return false;
            if (!OwnsCraft(id)) { if (d.price > Bank) return false; Bank -= d.price; PlayerPrefs.SetInt("bb_owncraft_" + id, 1); }
            Craft = id; PlayerPrefs.Save(); Sfx.Tone(520f, 0.09f, 0.08f, "triangle"); return true;
        }

        public static void EraseAll()
        {
            var keys = new List<string>();
            Bank = 0; Best = 0; Craft = "plane";
            foreach (var c in Crafts) { foreach (var it in Catalog) PlayerPrefs.DeleteKey("bb_up_" + c.id + "_" + it.id); foreach (var g in Guns) PlayerPrefs.DeleteKey("bb_own_" + c.id + "_" + g.id); foreach (var g in FootGuns) PlayerPrefs.DeleteKey("bb_own_" + c.id + "_" + g.id); PlayerPrefs.DeleteKey("bb_gun_" + c.id); PlayerPrefs.DeleteKey("bb_owncraft_" + c.id); }
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
            bool heli = craft == "heli" || craft == "heli2" || craft == "transport" || craft == "drone" || craft == "fiend";
            var s = new Stats { gun = g };
            s.napalm = L("napalm") > 0;
            var kit = KitOf(craft);
            s.maxHp = Mathf.Round(((heli ? 125f : 100f) + 25f * L("armor")) * (kit != null ? kit.hpK * 2f : craft == "ac130" ? 3.2f : craft == "dragon" ? 5f : craft == "ptero" ? 0.8f : craft == "fiend" ? 0.6f : craft == "b52" ? 3.6f : craft == "transport" ? 2.4f : craft == "bomber2" ? 1.3f : craft == "drone" ? 0.2f : craft == "heli2" ? 1.4f : craft == "tank" ? 3.2f : craft == "trike" ? 4.2f : craft == "arty" ? 1.7f : craft == "clowncar" ? 0.95f : craft == "scout" ? 1.15f : craft == "bike" ? 0.32f : 1f));   /* a soldier on foot has twice the toughness he used to */
            s.thrustK = (1f + 0.08f * L("engine")) * (craft == "ac130" ? 0.9f : craft == "dragon" ? 0.7f : craft == "ptero" ? 0.6f : craft == "fiend" ? 1.3f : craft == "b52" ? 0.85f : craft == "drone" ? 1.3f : 1f); s.turnK = (1f + 0.04f * L("engine")) * (craft == "ac130" ? 0.8f : craft == "b52" ? 0.55f : craft == "transport" ? 0.8f : craft == "drone" ? 1.6f : 1f);
            s.boostSec = 2.4f * (1f + 0.3f * L("tank")); s.boostRegenK = 1f + 0.2f * Mathf.Min(L("tank"), 6f);
            s.regen = 1.5f * L("repair"); s.lives = BattleSetup.Lives + Mathf.RoundToInt(L("life"));
            s.fireInt = Mathf.Max(1f / 40f, g.interval * Mathf.Pow(0.88f, Mathf.Min(L("rate"), 5f)) / (1f + 0.05f * over("rate", 5f)));
            s.dmg = g.dmg * (1f + 0.25f * L("ap")); s.pen = Mathf.Min(0.9f, g.pen + 0.1f * Mathf.Min(L("ap"), 4f) + 0.02f * over("ap", 4f));
            s.heatShot = g.heat * Mathf.Pow(0.85f, Mathf.Min(L("cool"), 4f)) / (1f + 0.08f * over("cool", 4f));
            s.barrels = 1 + Mathf.RoundToInt(L("wing")); s.maxBombs = (craft == "fiend" ? 6 : craft == "dragon" ? 6 : craft == "ptero" ? 0 : craft == "b52" ? 40 : craft == "bomber2" ? 16 : craft == "drone" ? 4 : 8) + 2 * Mathf.RoundToInt(L("rack"));
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
