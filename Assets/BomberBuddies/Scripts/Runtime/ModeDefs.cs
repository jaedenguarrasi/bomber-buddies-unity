using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The modes table from the original (MODES + MODE_CATS), every mode with its exact name and blurb; the ones this build
    /// can run carry their composition and rules, the rest show on the menu as still to come. Wave counts from startWave3/waveMix.
    /// </summary>
    public static class ModeDefs
    {
        public class Mode
        {
            public string id, name, group, desc;
            public string[] comp;          // allowed enemy types (null = everything)
            public float infK = 1f;
            public bool horde, playable = true, night;
            public float timer;            // a timed mode: seconds to survive / finish
            public System.Func<int, Dictionary<string, int>, Dictionary<string, int>> force;
        }
        static Mode Set(string id, string name, string group, string desc, Mode m) { m.id = id; m.name = name; m.group = group; m.desc = desc; return m; }

        public static readonly string[] Groups = { "Campaign", "Battles", "Missions", "Monsters", "Dragons", "Underground" };

        public static readonly Dictionary<string, Mode> All = new Dictionary<string, Mode>
        {
            { "tutorial", Set("tutorial", "Boot camp: soldier", "Campaign", "Start here. Looking, walking, sprinting, jumping, crouching, aiming, shooting, grenades, cover, the item wheel, marking, and a drive in the scout car: one step at a time, with the sergeant talking you through it. Clear it to open Chapter 1.", new Mode { playable = false }) },
            { "tutflight", Set("tutflight", "Flight school", "Campaign", "Take off, level out, turn, fly the rings, roll, strafe, bomb, boost, and get back down in one piece. Every aircraft in the hangar flies like this.", new Mode { playable = false }) },
            { "tutspecial", Set("tutspecial", "Grapple school", "Campaign", "The web-slinger, from the first line to the slingshot: swinging, zipping, running up walls, the charged leap, the glide, the bola and the fists. The special classes are where the game opens up.", new Mode { playable = false }) },
            { "ch1", Set("ch1", "Chapter 1: First light", "Campaign", "The enemy has crossed the river in the night. Hold the crossroads through three waves, then push up the road and silence the two anti-air guns so our helicopters can come in.", new Mode { playable = false }) },
            { "battle", Set("battle", "Battle", "Battles", "Waves of the enemy army with a breather between each. Everything you set in Battle setup applies.", new Mode { }) },
            { "infantry", Set("infantry", "Infantry", "Battles", "Soldiers only: no vehicles, no aircraft, no monsters. Waves of riflemen with the odd medic, gunslinger and bomber among them. Best on foot.", new Mode { comp = new[] { "inf", "medic", "egun", "vest", "bunker" }, infK = 2.2f }) },
            { "lightarmor", Set("lightarmor", "Light armour", "Battles", "Soldiers, scout jeeps and gunships, with machine-gun nests. No tanks, no missiles, no monsters.", new Mode { comp = new[] { "inf", "jeep", "eheli", "medic", "egun", "ejet", "bunker", "aa" }, infK = 1.6f }) },
            { "horde", Set("horde", "Horde", "Battles", "Endless waves with no breather, each one bigger, and a new wave rolls in every 40 seconds whether you are ready or not. How long can you last?", new Mode { horde = true }) },
            { "air", Set("air", "Air Superiority", "Battles", "The sky is full of enemy fighters and gunships, wave after wave, and nothing on the ground. Shoot down forty of them.", new Mode { comp = new[] { "eplane", "eheli" } }) },
            { "conquest", Set("conquest", "Conquest", "Battles", "Five command posts across the island. Take them by standing on them with no enemy there. Every post you hold drains the enemy's reinforcements; every post they hold drains yours. Run them down to zero to win.", new Mode { }) },
            { "defend", Set("defend", "Hold the Tower", "Battles", "Keep the marked tower standing through eight waves. Anything that reaches it tears at it where it stands. If it falls, you lose.", new Mode { }) },
            { "galactic", Set("galactic", "Galactic Assault", "Battles", "An invasion from orbit. Their capital ship hangs at the edge of space, shielding the army it has landed; your carrier has come to back up your troops. On the ground, take the command posts so your carrier can strike from orbit and drop reinforcements, and destroy the shield generator. Then climb through the clouds, fly into their hangar, get out, and fight through to the engine room or the computer room. Lose your own carrier and you lose.", new Mode { }) },
            { "demo", Set("demo", "Demolition", "Missions", "Three minutes to wreck the city. Every building you bring down pays well. Anti-aircraft guns and patrols defend it.", new Mode { comp = new[] { "inf", "jeep", "aa" }, timer = 180f }) },
            { "convoy", Set("convoy", "Convoy Escort", "Missions", "Three of your trucks have to cross the island. The enemy is waiting along the way. Keep at least two of them alive to the far side.", new Mode { }) },
            { "strike", Set("strike", "Strike", "Missions", "Five enemy fuel depots, each ringed with flak and missiles. Destroy all five in six minutes.", new Mode { }) },
            { "assassin", Set("assassin", "Assassination", "Missions", "An enemy general is somewhere on the island with his bodyguards, marked on your radar in gold. He will try to get to his escape helicopter. Kill him in five minutes.", new Mode { }) },
            { "abduct_def", Set("abduct_def", "Abduction: defend", "Missions", "The saucers have come for the islanders. Ten of them wander the field; the saucers hover over them and beam them up while the greys and their jetpack troopers cover them. Lose six and the island is lost. Bring the saucers down.", new Mode { comp = new[] { "ufo", "grey", "ejet" }, force = (n, c) => { c["ufo"] = 1 + Mathf.Min(3, n / 2); c["grey"] = 3 + n; c["ejet"] = n / 2; return c; } }) },
            { "abduct_att", Set("abduct_att", "Abduction: raid", "Missions", "You fly the saucer. The army is spread out below: hold the beam on a soldier until he is pulled inside. Fifteen taken wins it. Their guns and jeeps will not make it easy.", new Mode { playable = false }) },
            { "nest", Set("nest", "Goblin nest", "Missions", "You have walked into a nest. Goblins only, in packs, more every wave, coming from every side. Keep moving.", new Mode { comp = new[] { "gob", "ape" }, force = (n, c) => { c["gob"] = 6 + 3 * n; c["ape"] = 1; return c; } }) },
            { "shadow", Set("shadow", "Shadow war", "Missions", "Ninjas of both kinds and Grapples, nothing else: fast, close, and hard to keep in front of you. Aim for the ones in mid-leap.", new Mode { night = true, comp = new[] { "eninja", "eshade", "esling" }, force = (n, c) => { c["eninja"] = 3 + n * 2; c["eshade"] = 1 + n; c["esling"] = 1 + n / 2; return c; } }) },
            { "invasion", Set("invasion", "Saucer invasion", "Missions", "The greys come in force: saucers overhead, greys and their jetpack troopers on the ground, ray guns that leave skeletons. Daylight. No islanders to save this time, only yourselves.", new Mode { comp = new[] { "ufo", "grey", "ejet" }, force = (n, c) => { c["ufo"] = 2 + Mathf.Min(4, n); c["grey"] = 4 + 2 * n; c["ejet"] = 1 + n / 2; return c; } }) },
            { "bombers", Set("bombers", "Sewer sliders", "Missions", "They come up out of the manholes: greasy little bomb-machines on skids that slide at you with a charge on the chest and a timer that starts beeping when they get close. Medics keep the rest going. Shoot early, and watch the road.", new Mode { comp = new[] { "vest", "medic", "inf" }, force = (n, c) => { c["vest"] = 4 + 2 * n; c["medic"] = 1 + n / 2; c["inf"] = 2 + n; return c; } }) },
            { "beastside", Set("beastside", "Beast side", "Missions", "You fight for the monsters. Pick a goblin, an ape, a Devourer or a Nephilim (or anyone else), your hired squad comes as greys, and the army comes at you: soldiers, jeeps, tanks, guns and aircraft. The plain army, no monsters against you.", new Mode { playable = false }) },
            { "army", Set("army", "Beasts vs army", "Missions", "The monsters against the army, with you in the middle: apes, goblins and the rest hit the same waves the soldiers do. Whoever is left standing wins your credits.", new Mode { comp = new[] { "inf", "jeep", "tank", "aa", "ape", "gob", "eheli" }, force = (n, c) => { c["ape"] = 1 + n; c["gob"] = 4 + 2 * n; return c; } }) },
            { "monsters", Set("monsters", "Monster hunt", "Monsters", "Broad daylight, no army: apes, goblin packs, Devourers, Nephilim and the little apes, wave after wave, with your squad and whatever you bring. On foot it is a horror; from the air it is a hunt.", new Mode { comp = new[] { "ape", "gob", "raptor", "trex" }, force = (n, c) => { c["ape"] = 1 + n; c["gob"] = 4 + 2 * n; c["raptor"] = 2 + n; c["trex"] = n % 3 == 0 ? 1 : 0; return c; } }) },
            { "hunt", Set("hunt", "Night Hunt", "Monsters", "It is night. Your light only reaches so far, and things are hunting you: pale stalkers that circle in the dark and rush in, goblin packs, apes. Survive until dawn. Played on the ground.", new Mode { comp = new[] { "stalk", "gob", "ape" }, force = (n, c) => { c["stalk"] = 4 + 2 * n; c["gob"] = 3 + n; c["ape"] = n / 2; return c; }, timer = 300f, night = true }) },
            { "hunt_day", Set("hunt_day", "Daylight hunt", "Monsters", "The Night Hunt in daylight: the pale stalkers, goblin packs and apes come for you across open ground for five minutes. You can see them coming. That is not as much help as it sounds.", new Mode { comp = new[] { "stalk", "gob", "ape" }, force = (n, c) => { c["stalk"] = 4 + 2 * n; c["gob"] = 3 + n; c["ape"] = n / 2; return c; }, timer = 300f }) },
            { "beast", Set("beast", "The Beast", "Monsters", "Moonlight, and something enormous wakes up in the hills. It leaps, it smashes, it throws. Its brood comes with it. Kill the Beast before it kills you.", new Mode { night = true, comp = new[] { "beast", "stalk", "raptor" }, force = (n, c) => { c["beast"] = 1; c["stalk"] = 3 + n * 2; c["raptor"] = 2 + n; return c; } }) },
            { "lostworld", Set("lostworld", "Lost world", "Monsters", "Dinosaurs, and only dinosaurs: raptor packs from the start, pterodactyls swooping, a triceratops or two, and the tyrannosaur when it is ready. Nothing here needs a gun to kill you.", new Mode { comp = new[] { "raptor", "trex" }, force = (n, c) => { c["raptor"] = 3 + 2 * n; c["trex"] = n >= 2 ? 1 : 0; return c; } }) },
            { "kaiju", Set("kaiju", "The giant", "Monsters", "Something has come up out of the sea and it is walking toward the city. Every step shakes the ground, houses go under it, towers come down when it shoulders them, and it has a beam. Twelve thousand hit points. Bring everything.", new Mode { comp = new[] { "kaiju" }, force = (n, c) => { c["kaiju"] = 1; return c; } }) },
            { "colossus", Set("colossus", "The colossus", "Monsters", "A giant ape, the great ape four times over, with its monkeys. It hurls boulders the size of jeeps, and what it stands on it flattens.", new Mode { comp = new[] { "colossus", "ape", "gob" }, force = (n, c) => { c["colossus"] = 1; c["ape"] = 2 + n; c["gob"] = 4 + n * 2; return c; } }) },
            { "circus", Set("circus", "Circus of terror", "Monsters", "The clowns have come to town. They honk a horn that blows you off your feet, blow up balloon swords and balloon guns, and if they catch you, they eat you.", new Mode { comp = new[] { "clown", "jeep" }, force = (n, c) => { c["clown"] = 5 + n * 3; c["jeep"] = n / 2; return c; } }) },
            { "dragon", Set("dragon", "Dragon hunt", "Dragons", "One dragon, and it is enough: it circles, dives with fire, and lands to bite when you are on foot. Goblin packs and an ape or two keep you busy while you work on it. Bring it down and the credits are yours.", new Mode { comp = new[] { "dragon", "gob", "ape" }, force = (n, c) => { c["dragon"] = 1; c["gob"] = 3 + n; c["ape"] = n / 2; return c; } }) },
            { "dragons", Set("dragons", "Wyrm siege", "Dragons", "More than one. Dragons over the island with greys and their jet troopers below; every wave brings another. Aircraft and the heavy guns earn their keep here.", new Mode { comp = new[] { "dragon", "grey", "ejet", "aa" }, force = (n, c) => { c["dragon"] = 1 + ((n + 1) >> 1); c["grey"] = 2 + n; c["ejet"] = 1 + n / 2; c["aa"] = 1; return c; } }) },
            { "dragon_def", Set("dragon_def", "Burning village", "Dragons", "The dragon has come for the houses. It burns them from the air while its goblins keep you off it. Lose half the village and it is over. Every house you keep standing counts.", new Mode { night = true, comp = new[] { "dragon", "gob" }, force = (n, c) => { c["dragon"] = 1; c["gob"] = 6 + n * 3; return c; } }) },
            { "subway", Set("subway", "Subway sweep", "Underground", "You start in the tunnels. Sliders come up the line in the dark with riflemen behind them, and it does not stop. Fight along the platforms and through the stopped trains; the ladder under the shaft is the only way out, if you want it.", new Mode { playable = false }) },
            { "bunkers", Set("bunkers", "Bunker busters", "Underground", "Down the stairwell into the bunker complex. Breach the blast doors, take the rooms, and mind the ammo dumps: a stray round into one clears the room, you included. Both bunkers cleared wins the wave.", new Mode { playable = false }) },
            { "hell", Set("hell", "Hell dive", "Underground", "Straight down to the bottom. A red cavern of lava and fire, apes and their monkeys, and a dragon with room to fly. The ladder is where you fell in.", new Mode { playable = false }) },
        };

        static readonly string[] Creatures = { "gob", "ape", "raptor", "stalk", "trex", "grey", "ufo" };

        /// <summary>What wave n brings (startWave3 + waveMix), filtered by the mode.</summary>
        public static Dictionary<string, int> WaveCounts(Mode mode, int n)
        {
            var c = new Dictionary<string, int>
            {
                { "inf", Mathf.Min(26, 6 + 2 * n) },
                { "jeep", Mathf.Min(7, 1 + Mathf.CeilToInt(n / 2f)) },
                { "tank", Mathf.Min(6, n / 2) },
                { "aa", Mathf.Min(6, Mathf.CeilToInt(n / 2f)) },
                { "eheli", n >= 3 ? Mathf.Min(3, (n - 1) / 2) : 0 },
                { "eplane", n >= 4 ? Mathf.Min(3, (n - 2) / 2) : 0 },
                { "vest", n >= 2 ? Mathf.Min(4, Mathf.CeilToInt(n / 2f)) : 0 },
                { "medic", n >= 3 ? Mathf.Min(3, n / 3) : 0 },
                { "egun", n >= 3 ? Mathf.Min(4, n / 2) : 0 },
                { "ejet", n >= 4 ? Mathf.Min(3, (n - 2) / 2) : 0 },
                { "sam", n >= 4 ? Mathf.Min(4, (n - 2) / 2) : 0 },
                { "bunker", n >= 2 ? Mathf.Min(3, 1 + (n - 2) / 3) : 0 },
                { "gob", 0 }, { "ape", 0 }, { "raptor", 0 }, { "stalk", 0 }, { "trex", 0 }, { "grey", 0 }, { "ufo", 0 }, { "dragon", 0 },
            };
            if (mode != null)
            {
                if (mode.comp != null) { var keys = new List<string>(c.Keys); foreach (var k in keys) if (System.Array.IndexOf(mode.comp, k) < 0) c[k] = 0; }
                if (mode.infK != 1f) c["inf"] = Mathf.RoundToInt(c["inf"] * mode.infK);
                if (mode.force != null) c = mode.force(n, c);
                if (mode.horde) { var keys = new List<string>(c.Keys); foreach (var k in keys) c[k] = Mathf.RoundToInt(c[k] * (1.25f + n * 0.04f)); }
                if (mode.id == "air") { c["eplane"] = 3 + n; c["eheli"] = 1 + n / 2; }
            }
            return c;
        }
    }
}
