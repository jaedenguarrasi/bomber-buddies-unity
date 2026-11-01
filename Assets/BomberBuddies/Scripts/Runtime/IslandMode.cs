using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The island modes' extra rules on top of the waves: Conquest (five command posts; posts you hold drain the enemy's
    /// reinforcements and theirs drain yours, run them to zero) and Hold the Tower (the tallest tower must stand through eight
    /// waves; the army goes for it). Also hands every rifleman an objective so the fight has a shape.
    /// </summary>
    public class IslandMode : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public static IslandMode Instance { get; private set; }
        public string modeId = "battle";
        public MaterialLibrary lib;
        public World3 world;
        public EnemySpawner spawner;
        public readonly List<CommandPost> posts = new List<CommandPost>();
        public float yourTickets = 200f, theirTickets = 200f;
        public Health tower;
        Transform towerBeacon;
        float objT, allyT = 20f, lineT, endT = -1f, spawnT = 4f, timeLeft;
        public bool over;
        // strike / assassin / convoy / air
        readonly List<Health> depots = new List<Health>();
        Health vip; Vector3 exitPoint; Transform exitHeli;
        readonly List<EnemyVehicle> trucks = new List<EnemyVehicle>(); Vector3 convoyGoal; int trucksThrough;
        int downed; const int AirGoal = 40;
        public bool ModeDrivesSpawns { get { return modeId == "conquest" || modeId == "assassin" || modeId == "air" || modeId == "convoy" || modeId == "strike"; } }

        public static IslandMode Create(Transform parent, MaterialLibrary lib, World3 world, EnemySpawner spawner, string modeId)
        {
            var go = new GameObject("Island mode");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<IslandMode>();
            m.lib = lib; m.world = world; m.spawner = spawner; m.modeId = modeId;
            Instance = m;
            m.Setup();
            if (m.ModeDrivesSpawns && spawner != null) spawner.enabled = false;
            return m;
        }

        void Setup()
        {
            if (modeId == "conquest")
            {
                // five posts spread over the island, on dry open ground
                string[] names = { "Alpha", "Bravo", "Charlie", "Delta", "Echo" };
                float R = World3.WORLD3 * U * World3.Spread * 0.26f;
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f + 0.4f;
                    var at = LandNear(new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R));
                    posts.Add(CommandPost.Build(transform, lib, names[i], at, i == 0 ? 1 : i < 3 ? 0 : -1));
                }
                if (GameState.Hud != null) GameState.Hud.Announce("Conquest: take the posts, run their reinforcements to zero", 5f);
            }
            else if (modeId == "strike")
            {
                // five fuel depots (a bunker with 2.5× hp), each ringed by an AA gun, a SAM and two riflemen, 500–1000 units out
                timeLeft = 360f;
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f), r = Random.Range(500f, 1000f) * U * World3.Spread;
                    var q = LandNear(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    var d = EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Bunker, q, 1);
                    d.name = "Fuel depot"; d.health.max *= 2.5f; d.health.current = d.health.max;
                    depots.Add(d.health);
                    EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.AA, LandNear(q + Random.insideUnitSphere.Flat() * 70f * U), 1);
                    EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Sam, LandNear(q + Random.insideUnitSphere.Flat() * 70f * U), 1);
                    for (int k = 0; k < 2; k++) { var ai = spawner.Spawn(LandNear(q + Random.insideUnitSphere.Flat() * 70f * U), false, 1); ai.attackRange = 50f; ai.sightRange = 90f; }
                }
                spawnT = 30f;
                if (GameState.Hud != null) GameState.Hud.Announce("Strike: five fuel depots, six minutes", 5f);
            }
            else if (modeId == "assassin")
            {
                // the general somewhere on the island with four bodyguards, walking for his helicopter
                timeLeft = 300f;
                var g = LandNear(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 600f * U * World3.Spread);
                var ai = spawner.Spawn(g, false, 1); ai.name = "The general"; ai.attackRange = 40f; ai.health.max = 220f; ai.health.current = 220f; ai.walkSpeed = 1.5f;
                vip = ai.health;
                for (int k = 0; k < 4; k++) { var b = spawner.Spawn(LandNear(g + Random.insideUnitSphere.Flat() * 40f * U), false, 1); b.attackRange = 50f; b.sightRange = 90f; b.damage = 7f; }
                exitPoint = LandNear(-g.normalized * 700f * U * World3.Spread);
                var heli = CraftModels.Heli(transform, lib, CraftModels.Foe, 9); heli.transform.position = exitPoint + Vector3.up * 0.9f; heli.name = "Escape helicopter";
                exitHeli = heli.transform;
                spawnT = 20f;
                if (GameState.Hud != null) GameState.Hud.Announce("Assassination: the general is marked in gold. Five minutes.", 5f);
            }
            else if (modeId == "convoy")
            {
                var s0 = LandNear(new Vector3(-1150f * U * World3.Spread, 0f, Random.Range(-300f, 300f) * U * World3.Spread));
                var s1 = LandNear(new Vector3(1150f * U * World3.Spread, 0f, Random.Range(-300f, 300f) * U * World3.Spread));
                convoyGoal = s1;
                for (int i = 0; i < 3; i++)
                {
                    var t = EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Jeep, LandNear(s0 + new Vector3(-i * 40f * U, 0f, i * 8f * U)), 0);
                    t.name = "Truck " + (i + 1); t.health.max *= 3f; t.health.current = t.health.max; t.convoyGoal = convoyGoal; t.speed = 32f * U;
                    trucks.Add(t);
                }
                spawnT = 6f;
                if (GameState.Hud != null) GameState.Hud.Announce("Convoy: get two of three trucks across the island", 5f);
            }
            else if (modeId == "air")
            {
                spawnT = 3f;
                if (GameState.Hud != null) GameState.Hud.Announce("Air Superiority: shoot down forty", 5f);
            }
            else if (modeId == "defend")
            {
                // the tallest, widest standing tower (the original's floors × (half-width + 10))
                Vector3 best = Vector3.zero; float bestScore = -1f;
                foreach (var t in world.towerCells)
                {
                    int k = (int)t.z * World3.N3 + (int)t.x;
                    float score = world.bfl[k] * (world.bhw[k] + 10f);
                    if (score > bestScore) { bestScore = score; best = t; }
                }
                if (bestScore > 0f)
                {
                    int k = (int)best.z * World3.N3 + (int)best.x;
                    var pos = World3.ToWorld((int)best.x * World3.BS, world.hm[k], (int)best.z * World3.BS);
                    float h = world.bfl[k] * World3.FLOOR_H, hw = world.bhw[k] - 1f;
                    var go = new GameObject("The Tower");
                    go.transform.SetParent(transform, false);
                    go.transform.position = pos + Vector3.up * h * 0.5f * U;
                    var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(hw * 2f * U + 0.5f, h * U, hw * 2f * U + 0.5f);
                    tower = go.AddComponent<Health>(); tower.team = 0; tower.max = 1500f * GalacticMode.K; tower.current = tower.max; tower.stationary = true; tower.aimHeight = 3f;
                    var hz = go.AddComponent<HitZone>(); hz.owner = tower; hz.multiplier = 1f;
                    tower.OnDied += info => End(false, "The tower has fallen.");
                    // the blue beacon on the roof
                    var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(beacon.GetComponent<Collider>());
                    beacon.transform.SetParent(transform, false); beacon.transform.position = pos + Vector3.up * (h + 60f) * U; beacon.transform.localScale = new Vector3(2.2f * U, 120f * U, 2.2f * U);
                    var m = lib != null ? lib.Instance("glow") : null; if (m != null) { m.SetColor("_BaseColor", new Color(120f / 255f, 200f / 255f, 1f)); m.SetColor("_EmissionColor", new Color(0.4f, 0.75f, 1f) * 4f); beacon.GetComponent<Renderer>().sharedMaterial = m; }
                    towerBeacon = beacon.transform;
                    if (GameState.Hud != null) GameState.Hud.Announce("Hold the Tower: keep the marked tower standing through eight waves", 5f);
                }
            }
        }

        static string Clock(float t) { int m = Mathf.FloorToInt(t / 60f), sec = Mathf.FloorToInt(t % 60f); return m + ":" + sec.ToString("00"); }

        int AirAlive() { int n = 0; foreach (var h in Health.All) if (h != null && h.alive && h.team == 1 && h.isAir && h.GetComponent<EnemyPlane>() != null) n++; return n; }

        void SpawnAirFromEdge(bool heli)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = 1200f * U * World3.Spread;
            var at = new Vector3(Mathf.Cos(a) * r, 500f * U, Mathf.Sin(a) * r);
            EnemyPlane.Build(transform, lib, at, Mathf.Atan2(-at.x, -at.z), 1, heli);
        }

        Vector3 LandNear(Vector3 at)
        {
            float sea = World3.SEA * U;
            for (float r = 0f; r < 300f; r += 10f)
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f + r * 0.13f;
                    var p = at + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float h = world.GroundMetres(p.x, p.z);
                    if (h > sea + 1.5f) { p.y = h; return p; }
                }
            at.y = world.GroundMetres(at.x, at.z);
            return at;
        }

        public int PostsHeld { get { int n = 0; foreach (var p in posts) if (p.own == 1) n++; return n; } }
        public int PostsTheirs { get { int n = 0; foreach (var p in posts) if (p.own == -1) n++; return n; } }

        void End(bool won, string text)
        {
            if (over) return;
            over = true; endT = 0f;
            if (PlayerManager.Instance != null) PlayerManager.Instance.over = true;
            if (spawner != null) spawner.enabled = false;
            if (GameState.Hud != null) GameState.Hud.Announce(text, 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (over)
            {
                endT += dt;
                if (endT > 8f && InputBridge.Instance != null && (InputBridge.Instance.JumpPressed || InputBridge.Instance.Fire)) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }
            var pm = PlayerManager.Instance;
            // objectives: every rifleman heads for the nearest post that is not his side's, or the tower
            objT -= dt;
            if (objT <= 0f)
            {
                objT = 2f;
                foreach (var h in Health.All)
                {
                    if (h == null || !h.alive) continue;
                    var inf = h.GetComponent<EnemySoldier>(); if (inf == null || inf.guard) continue;
                    if (tower != null && h.team == 1) { inf.objective = tower.transform.position; continue; }
                    CommandPost best = null; float bd = 1e9f;
                    foreach (var p in posts) { int want = h.team == 1 ? -1 : 1; if (p.own == want) continue; float d = Vector3.Distance(p.transform.position, h.transform.position); if (d < bd) { bd = d; best = p; } }
                    inf.objective = best != null ? (Vector3?)best.transform.position : null;
                }
            }
            if (timeLeft > 0f && (modeId == "strike" || modeId == "assassin"))
            {
                timeLeft -= dt;
                if (timeLeft <= 0f) { timeLeft = 0f; End(false, modeId == "strike" ? "Out of time: depots left standing." : "Out of time: the general got away."); return; }
            }
            int enemiesAlive = Health.Count(1, Vector3.zero, 9999f, false, false);
            if (modeId == "strike")
            {
                int left = 0; foreach (var d in depots) if (d != null && d.alive) left++;
                if (left == 0) { GameState.Earn(3000); End(true, "All five depots destroyed!"); return; }
                spawnT -= dt;
                if (spawnT <= 0f) { spawnT = Random.Range(25f, 35f); if (AirAlive() < 3) SpawnAirFromEdge(Random.value < 0.5f); }
            }
            else if (modeId == "assassin")
            {
                if (vip != null && vip.alive)
                {
                    var inf = vip.GetComponent<EnemySoldier>(); if (inf != null) inf.objective = exitPoint;
                    if (Vector3.Distance(vip.transform.position, exitPoint) < 30f * U) { End(false, "The general reached his helicopter and got away."); return; }
                }
                else if (vip != null) { GameState.Earn(2000); End(true, "Target eliminated!"); return; }
                spawnT -= dt;
                if (spawnT <= 0f) { spawnT = Random.Range(20f, 30f); if (enemiesAlive < 20 && vip != null) for (int i = 0; i < 3; i++) { var ai = spawner.Spawn(LandNear(vip.transform.position + Random.insideUnitSphere.Flat() * 200f * U), false, 1); ai.attackRange = 50f; ai.sightRange = 90f; } }
            }
            else if (modeId == "air")
            {
                spawnT -= dt;
                if (spawnT <= 0f) { spawnT = Random.Range(5f, 8f); if (AirAlive() < 7) SpawnAirFromEdge(Random.value < 0.35f); }
                downed = GameState.Kills;
                if (downed >= AirGoal) { GameState.Earn(2500); End(true, "Air superiority: 40 shot down!"); return; }
            }
            else if (modeId == "convoy")
            {
                int alive2 = 0; EnemyVehicle lead = null;
                foreach (var t in trucks)
                {
                    if (t == null || !t.health.alive) continue; alive2++;
                    if (!t.arrived) { if (Vector3.Distance(t.transform.position, convoyGoal) < 60f * U) { t.arrived = true; t.convoyGoal = null; trucksThrough++; if (GameState.Hud != null) GameState.Hud.Comms("A truck made it! " + trucksThrough + " across"); } else if (lead == null) lead = t; }
                }
                spawnT -= dt;
                if (spawnT <= 0f)
                {
                    spawnT = Random.Range(10f, 16f);
                    if (lead != null && enemiesAlive < 24)
                    {
                        var dir = (convoyGoal - lead.transform.position).normalized;
                        var ax = lead.transform.position + dir * 360f * U;
                        for (int i = 0; i < 3; i++) { var ai = spawner.Spawn(LandNear(ax + Random.insideUnitSphere.Flat() * 120f * U), false, 1); ai.attackRange = 50f; ai.sightRange = 90f; }
                        EnemyVehicle.Build(transform, lib, Random.value < 0.5f ? EnemyVehicle.Kind.Jeep : EnemyVehicle.Kind.Tank, LandNear(ax + Random.insideUnitSphere.Flat() * 120f * U), 1);
                        if (GameState.Hud != null) GameState.Hud.Comms("Ambush ahead!");
                    }
                }
                if (trucksThrough >= 2) { GameState.Earn(2500); End(true, "Convoy through! " + trucksThrough + " trucks made it."); return; }
                else if (alive2 + trucksThrough < 2) { End(false, "The convoy was destroyed."); return; }
            }
            if (modeId == "conquest")
            {
                // the enemy's reinforcements come in at their posts every 14–20 s: four riflemen and a tank or a jeep
                spawnT -= dt;
                if (spawnT <= 0f)
                {
                    spawnT = Random.Range(14f, 20f);
                    if (enemiesAlive < 26)
                    {
                        var src = new List<CommandPost>(); foreach (var p in posts) if (p.own == -1) src.Add(p);
                        var P = src.Count > 0 ? src[Random.Range(0, src.Count)] : posts[Random.Range(0, posts.Count)];
                        for (int i = 0; i < 4; i++) { var ai = spawner.Spawn(LandNear(P.transform.position + Random.insideUnitSphere.Flat() * 60f * U), false, 1); ai.attackRange = 50f; ai.sightRange = 90f; }
                        EnemyVehicle.Build(transform, lib, Random.value < 0.4f ? EnemyVehicle.Kind.Tank : EnemyVehicle.Kind.Jeep, LandNear(P.transform.position + Random.insideUnitSphere.Flat() * 60f * U), 1);
                    }
                }
                bool here = false; var foot = pm != null && pm.OnFoot ? pm.foot : null;
                foreach (var post in posts)
                {
                    here = foot != null && foot.health.alive && Vector3.Distance(foot.transform.position, post.transform.position) < CommandPost.Radius;
                    int allies = Health.Count(0, post.transform.position, CommandPost.Radius, true, true) - (here ? 1 : 0);
                    string banner = post.Tick(dt, here, Mathf.Max(0, allies));
                    if (banner != null && GameState.Hud != null) { GameState.Hud.Announce(banner); if (banner.Contains("ours")) GameState.Earn(200); }
                }
                theirTickets -= PostsHeld * 0.9f * dt;
                yourTickets -= PostsTheirs * 0.9f * dt;
                if (theirTickets <= 0f) { GameState.Earn(2500); End(true, "Conquest won: the enemy has nothing left to send."); }
                else if (yourTickets <= 0f) End(false, "Your reinforcements are spent.");
                // a squad for you at a held post now and then
                allyT -= dt;
                if (allyT <= 0f)
                {
                    allyT = Random.Range(22f, 30f);
                    CommandPost held = null; foreach (var p in posts) if (p.own == 1) { held = p; break; }
                    if (held != null && spawner != null && Health.Count(0, Vector3.zero, 5000f, true, true) < 14)
                        for (int i = 0; i < 2; i++) { var at = LandNear(held.transform.position + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f))); var ai = spawner.Spawn(at, false, 0); ai.attackRange = 50f; ai.sightRange = 90f; ai.damage = 7f * GalacticMode.K; }
                }
            }
            if (modeId == "defend")
            {
                if (towerBeacon != null) towerBeacon.Rotate(0f, 0.8f * Mathf.Rad2Deg * dt, 0f);
                if (GameState.Wave > 8 && spawner != null && spawner.Remaining == 0) End(true, "The tower stands! Eight waves held.");
            }
            lineT -= dt;
            if (lineT <= 0f && GameState.Hud != null)
            {
                lineT = 0.25f;
                if (modeId == "strike") { int left = 0; foreach (var d in depots) if (d != null && d.alive) left++; GameState.Hud.SetModeLine(Clock(timeLeft) + " · depots " + left + " left"); }
                else if (modeId == "assassin") GameState.Hud.SetModeLine(Clock(timeLeft) + " · the general " + (vip != null && vip.alive ? Mathf.RoundToInt(Vector3.Distance(vip.transform.position, exitPoint) / U) + " from his helicopter" : "is down"));
                else if (modeId == "air") GameState.Hud.SetModeLine("Shot down " + downed + " / " + AirGoal);
                else if (modeId == "convoy") GameState.Hud.SetModeLine("Trucks across " + trucksThrough + " / 3");
                else if (modeId == "conquest") GameState.Hud.SetModeLine("Posts " + PostsHeld + "/" + posts.Count + " · yours " + Mathf.CeilToInt(yourTickets) + " · theirs " + Mathf.CeilToInt(theirTickets));
                else if (modeId == "defend" && tower != null) GameState.Hud.SetModeLine("Tower " + Mathf.RoundToInt(tower.Fraction * 100f) + "% · wave " + GameState.Wave + "/8");
            }
        }
    }
}
