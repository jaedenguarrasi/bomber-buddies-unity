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
        float objT, allyT = 20f, lineT, endT = -1f;
        public bool over;

        public static IslandMode Create(Transform parent, MaterialLibrary lib, World3 world, EnemySpawner spawner, string modeId)
        {
            var go = new GameObject("Island mode");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<IslandMode>();
            m.lib = lib; m.world = world; m.spawner = spawner; m.modeId = modeId;
            Instance = m;
            m.Setup();
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
            if (modeId == "conquest")
            {
                bool here = false; var foot = pm != null && pm.OnFoot ? pm.foot : null;
                foreach (var post in posts)
                {
                    here = foot != null && foot.health.alive && Vector3.Distance(foot.transform.position, post.transform.position) < CommandPost.Radius;
                    int allies = Health.Count(0, post.transform.position, CommandPost.Radius, true, true) - (here ? 1 : 0);
                    string banner = post.Tick(dt, here, Mathf.Max(0, allies));
                    if (banner != null && GameState.Hud != null) { GameState.Hud.Announce(banner); if (banner.Contains("ours")) GameState.Earn(400); }
                }
                theirTickets -= PostsHeld * 0.5f * dt;
                yourTickets -= PostsTheirs * 0.5f * dt;
                if (theirTickets <= 0f) End(true, "Conquest won! Their reinforcements are spent.");
                else if (yourTickets <= 0f) End(false, "Your reinforcements are spent.");
                // a squad for you at a held post now and then
                allyT -= dt;
                if (allyT <= 0f)
                {
                    allyT = 30f;
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
                if (modeId == "conquest") GameState.Hud.SetModeLine("Posts " + PostsHeld + "/" + posts.Count + " · yours " + Mathf.CeilToInt(yourTickets) + " · theirs " + Mathf.CeilToInt(theirTickets));
                else if (modeId == "defend" && tower != null) GameState.Hud.SetModeLine("Tower " + Mathf.RoundToInt(tower.Fraction * 100f) + "% · wave " + GameState.Wave + "/8");
            }
        }
    }
}
