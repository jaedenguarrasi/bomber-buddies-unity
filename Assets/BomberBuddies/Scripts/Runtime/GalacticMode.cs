using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Galactic Assault: two carriers high above the island, yours and theirs, hangars facing each other. Fly out of your hangar,
    /// knock out the shield generator on the ground, fly into their hangar, get out, and fight through to a core. Their cannons
    /// shell your carrier the whole time; lose it and you lose. All the numbers come from the original game.
    /// </summary>
    public class GalacticMode : MonoBehaviour
    {
        /// <summary>Metres per original unit: a 15-unit soldier is 1.8 m tall.</summary>
        public const float U = 0.12f;
        public const float BombG = 230f * U, BombVMax = 250f * U, CloudY = 1900f * U;
        public static GalacticMode Instance { get; private set; }

        public MaterialLibrary lib;
        public Terrain terrain;
        public FootController foot;
        public CameraRig rig;
        public Camera cam;
        public EnemySpawner spawner;
        public PlaneController plane;
        public Carrier friendly, enemy;
        public ShipPart generator;
        public readonly List<PlaneController> allyPlanes = new List<PlaneController>();
        public readonly List<EnemyPlane> fighters = new List<EnemyPlane>();
        public float shake;
        public bool over;
        Transform beam;
        float fighterT = 6f, modeLineT, endT = -1f;
        bool respawning, onFoot;
        readonly List<Vector3> feet = new List<Vector3>();

        public static GalacticMode Create(Transform parent, MaterialLibrary lib, Terrain terrain, FootController foot, CameraRig rig, Camera cam, EnemySpawner spawner)
        {
            var go = new GameObject("Galactic Assault");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GalacticMode>();
            g.lib = lib; g.terrain = terrain; g.foot = foot; g.rig = rig; g.cam = cam; g.spawner = spawner;
            Instance = g;
            g.Setup();
            return g;
        }

        // ---- the world ----
        public float TerrainHeight(float x, float z) { return terrain != null ? IslandBuilder.GroundHeight(terrain, x, z) : 0f; }

        /// <summary>The highest thing under a point: a carrier's top or the ground.</summary>
        public float ObstacleHeight(float x, float z)
        {
            float h = TerrainHeight(x, z);
            foreach (var s in new[] { friendly, enemy })
            {
                if (s == null || !s.Solid) continue;
                var l = s.W2L(new Vector3(x, 0f, z));
                if (Mathf.Abs(l.x) < 430f && Mathf.Abs(l.z) < 95f) h = Mathf.Max(h, s.transform.position.y + 130f * U);
            }
            return h;
        }

        public Carrier HangarAt(Vector3 w)
        {
            if (friendly != null && friendly.HangarAt(w)) return friendly;
            if (enemy != null && enemy.HangarAt(w) && !enemy.shield) return enemy;
            return null;
        }

        /// <summary>Where someone on foot can stand: a ship deck when one is under them, else the island.</summary>
        public float GroundHeight(float x, float z)
        {
            var p = foot != null ? foot.transform.position : new Vector3(x, 0f, z);
            p.x = x; p.z = z;
            foreach (var s in new[] { friendly, enemy })
            {
                if (s == null) continue;
                float f = s.FloorAt(p);
                if (!float.IsNaN(f) && p.y > f - 3f) return f;
            }
            return TerrainHeight(x, z);
        }

        public void Shake(float k) { shake = Mathf.Max(shake, k); }

        void Setup()
        {
            // the carriers: 2100 units apart, the enemy 100 higher, hangars facing each other
            friendly = Carrier.Build(transform, lib, 0, new Vector3(0f, 3600f * U, -1050f * U));
            enemy = Carrier.Build(transform, lib, 1, new Vector3(0f, 3700f * U, 1050f * U));
            friendly.OnDestroyed += s => End(false);
            enemy.OnDestroyed += s => End(true);
            BuildCloudDeck();
            ArmEnemyShip();
            BuildGenerator();
            plane = PlaneController.Build(transform, lib, cam);
            plane.health.OnDied += info => { if (!respawning && !over) StartCoroutine(Respawn("Shot down")); };
            if (foot != null)
            {
                foot.groundHeight = GroundHeight;
                foot.health.OnDied += info => { if (!respawning && !over && onFoot) StartCoroutine(Respawn("You're down")); };
            }
            PlaceInHangar();
            if (GameState.Hud != null)
            {
                GameState.Hud.Announce("Your carrier is here to back the ground war: destroy their shield generator, then board their ship", 5f);
                GameState.Hud.SetFlight(plane);
            }
        }

        void BuildCloudDeck()
        {
            var m = lib != null ? lib.Instance("water") : null;
            if (m != null) { m.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.97f, 0.88f)); m.SetFloat("_Smoothness", 0.2f); }
            for (int i = 0; i < 2; i++)
            {
                var deck = GameObject.CreatePrimitive(PrimitiveType.Plane);
                deck.name = "Cloud deck";
                deck.transform.SetParent(transform, false);
                deck.transform.position = new Vector3(0f, CloudY + (i == 0 ? 0f : -0.5f), 0f);
                deck.transform.localScale = new Vector3(300f, 1f, 300f);
                if (i == 1) deck.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                Destroy(deck.GetComponent<Collider>());
                var r = deck.GetComponent<Renderer>(); if (m != null) r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }

        void ArmEnemyShip()
        {
            var E = enemy;
            // heavy cannons on the upper deck, quick guns on the hull top and under the belly
            ShipGun.Build(E, lib, true, false, -220, 55, -38); ShipGun.Build(E, lib, true, false, -220, 55, 38);
            ShipGun.Build(E, lib, true, false, 150, 55, -38); ShipGun.Build(E, lib, true, false, 150, 55, 38);
            ShipGun.Build(E, lib, false, false, -300, 25, -72); ShipGun.Build(E, lib, false, false, -300, 25, 72);
            ShipGun.Build(E, lib, false, false, 300, 25, -72); ShipGun.Build(E, lib, false, false, 300, 25, 72);
            ShipGun.Build(E, lib, false, true, -100, -45, 0); ShipGun.Build(E, lib, false, true, 220, -45, 0);
            // the two cores
            BuildCore(E, -298, "engine", new Color(1f, 0.55f, 0.2f));
            BuildCore(E, 292, "computer", new Color(0.35f, 1f, 0.85f));
            // fifteen guards at their posts
            float[,] g = { { 10, -10 }, { 60, 25 }, { 100, -30 }, { 30, 30 }, { -100, -6 }, { -180, 6 }, { -240, 0 }, { 170, -6 }, { 230, 6 }, { -300, -40 }, { -300, 40 }, { -320, 0 }, { 290, -38 }, { 290, 38 }, { 320, 0 } };
            if (spawner != null)
            {
                for (int i = 0; i < g.GetLength(0); i++)
                {
                    var at = E.L2W(g[i, 0], -30f, g[i, 1]);
                    var ai = spawner.Spawn(at);
                    ai.guard = true;
                    var agent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false;
                    ai.health.max = 160f; ai.health.current = 160f;
                    ai.transform.SetParent(E.transform, true);
                    var face = g[i, 0] < -40f ? E.LocalDir(1, 0, 0) : g[i, 0] > 120f ? E.LocalDir(-1, 0, 0) : E.MouthDirection;
                    ai.SetFacing(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
                    E.guards.Add(ai);
                }
            }
        }

        void BuildCore(Carrier E, float lx, string name, Color glow)
        {
            var go = new GameObject("Core " + name);
            var m = lib != null ? lib.Instance("glow") : null; if (m != null) { m.SetColor("_BaseColor", glow * 0.8f); m.SetColor("_EmissionColor", glow * 4f); }
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) metal.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f));
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material mat)
            {
                var p = GameObject.CreatePrimitive(t); p.name = n; p.transform.SetParent(go.transform, false); p.transform.localPosition = pos * U; p.transform.localScale = scale * U;
                if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat; return p;
            }
            P(PrimitiveType.Cylinder, "Base", new Vector3(0f, 1.5f, 0f), new Vector3(26f, 1.5f, 26f), metal);
            P(PrimitiveType.Cylinder, "Column", new Vector3(0f, 14f, 0f), new Vector3(16f, 12f, 16f), m);
            P(PrimitiveType.Cylinder, "Cap", new Vector3(0f, 26.5f, 0f), new Vector3(22f, 1.5f, 22f), metal);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI * 0.5f; P(PrimitiveType.Cube, "Pylon", new Vector3(Mathf.Cos(a) * 12f, 14f, Mathf.Sin(a) * 12f), new Vector3(2f, 24f, 2f), metal); }
            var light = new GameObject("Glow").AddComponent<Light>(); light.transform.SetParent(go.transform, false); light.transform.localPosition = new Vector3(0f, 14f * U, 0f);
            light.type = LightType.Point; light.color = glow; light.range = 9f; light.intensity = 3f; light.shadows = LightShadows.None;
            var part = E.AddPart("score", lx, -30f, 0f, 1152f, 1f, 1f, go);
            part.OnDied += p =>
            {
                if (GameState.Hud != null) GameState.Hud.Announce("The " + name + " core is destroyed!", 4f);
                if (enemy != null && enemy.alive && enemy.dying < 0f) { enemy.StartDying(); if (GameState.Hud != null) GameState.Hud.Announce("Their ship is going down!", 4f); }
            };
        }

        void BuildGenerator()
        {
            // on the island under the enemy ship (nudged onto land if it would be in the sea)
            var at = new Vector3(90f * U, 0f, -(-760f) * U);
            for (int tries = 0; tries < 40 && TerrainHeight(at.x, at.z) < 10f; tries++) at += new Vector3(0f, 0f, -8f);
            at.y = TerrainHeight(at.x, at.z);
            var go = new GameObject("Shield generator");
            go.transform.SetParent(transform, false);
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) metal.SetColor("_BaseColor", new Color(0.35f, 0.36f, 0.4f));
            var blue = lib != null ? lib.Instance("glow") : null; if (blue != null) { blue.SetColor("_BaseColor", new Color(0.4f, 0.7f, 1f)); blue.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 4f); }
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material mat)
            {
                var p = GameObject.CreatePrimitive(t); p.name = n; p.transform.SetParent(go.transform, false); p.transform.localPosition = pos * U; p.transform.localScale = scale * U;
                if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat; return p;
            }
            P(PrimitiveType.Cube, "Base", new Vector3(0f, 4f, 0f), new Vector3(36f, 8f, 36f), metal);
            P(PrimitiveType.Cube, "Block", new Vector3(0f, 13f, 0f), new Vector3(22f, 10f, 22f), metal);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f; P(PrimitiveType.Cube, "Pylon", new Vector3(Mathf.Cos(a) * 15f, 16f, Mathf.Sin(a) * 15f), new Vector3(3f, 24f, 3f), metal); }
            P(PrimitiveType.Sphere, "Dome", new Vector3(0f, 24f, 0f), new Vector3(16f, 16f, 16f), blue);
            var pos = at;
            var part = go.AddComponent<ShipPart>();
            part.Setup("shieldgen", 2048f, 0.5f, 1.3f, enemy);
            go.transform.position = pos;
            generator = part;
            // the beam up to the ship
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.name = "Shield beam"; Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(transform, false);
            float top = enemy.transform.position.y - 45f * U;
            b.transform.position = new Vector3(pos.x, (pos.y + 32f * U + top) * 0.5f, pos.z);
            b.transform.localScale = new Vector3(1.6f * U * 2f, (top - pos.y - 32f * U) * 0.5f, 1.6f * U * 2f);
            var bm = lib != null ? lib.Instance("water") : null; if (bm != null) bm.SetColor("_BaseColor", new Color(120f / 255f, 200f / 255f, 1f, 0.5f));
            if (bm != null) b.GetComponent<Renderer>().sharedMaterial = bm;
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam = b.transform;
            part.OnDied += p =>
            {
                if (enemy != null) enemy.DropShield();
                if (beam != null) Destroy(beam.gameObject);
                if (GameState.Hud != null) GameState.Hud.Announce("Shield generator destroyed! Their hangar is open: climb up, fly in, and destroy the engine or computer room", 5f);
            };
        }

        // ---- the player ----
        void PlaceInHangar()
        {
            var F = friendly;
            var pos = F.L2W(20f + Random.Range(-25f, 25f), F.FloorY + 4f, -20f);
            var dir = F.MouthDirection;
            plane.Place(pos, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, F, 0f);
            SetOnFoot(false);
        }

        void SetOnFoot(bool on)
        {
            onFoot = on;
            if (foot != null) foot.gameObject.SetActive(on);
            if (rig != null) rig.enabled = on;
            if (GameState.Hud != null) GameState.Hud.SetFlight(on ? null : plane);
        }

        /// <summary>Out of the docked plane onto the hangar deck.</summary>
        public void ExitToFoot()
        {
            if (plane == null || plane.docked == null || foot == null) return;
            var S = plane.docked;
            plane.Park();
            var right = Quaternion.Euler(0f, 90f, 0f) * plane.Forward; right.y = 0f;
            var pos = plane.transform.position + right.normalized * 14f * U;
            pos.y = S.L2W(0f, S.FloorY, 0f).y + 0.1f;
            SetOnFoot(true);
            foot.Teleport(pos, plane.yaw * Mathf.Rad2Deg);
            foot.ControlsLocked = false;
            if (GameState.Hud != null) GameState.Hud.Announce("On foot. F / GET IN to board the plane again");
        }

        /// <summary>Back into the parked plane when standing beside it.</summary>
        public bool TryGetIn()
        {
            if (plane == null || foot == null || plane.Active) return false;
            if (Vector3.Distance(foot.transform.position, plane.transform.position) > 18f * U) { if (GameState.Hud != null) GameState.Hud.Announce("No vehicle close enough to get in"); return false; }
            float hpK = plane.health.Fraction;
            plane.Place(plane.transform.position, plane.yaw * Mathf.Rad2Deg, plane.docked, 0f);
            plane.health.current = plane.health.max * Mathf.Clamp(hpK, 0.05f, 1f);
            SetOnFoot(false);
            if (GameState.Hud != null) GameState.Hud.Announce("In the fighter-bomber");
            return true;
        }

        IEnumerator Respawn(string why)
        {
            respawning = true;
            GameState.Deaths++;
            if (GameState.Hud != null) GameState.Hud.Announce(why);
            if (onFoot && foot != null) { foot.ControlsLocked = true; if (foot.model != null) foot.model.Die(Vector3.forward); }
            yield return new WaitForSeconds(2.6f);
            if (over) yield break;
            if (foot != null)
            {
                var old = foot.model;
                foot.model = SoldierModelBase.Create(foot.transform, lib, 0, 8);
                if (old != null) Destroy(old.gameObject);
                foot.health.ResetFull();
                if (foot.rifle != null) foot.rifle.ammo = foot.rifle.magSize;
                foot.ControlsLocked = false;
            }
            if (friendly != null && friendly.alive && friendly.dying < 0f) PlaceInHangar();
            else
            {
                // no carrier to come back to: straight into the air over the island
                plane.Place(new Vector3(Random.Range(-60f, 60f), 320f * U + 60f, -300f), 0f, null, 180f);
                SetOnFoot(false);
            }
            respawning = false;
        }

        void End(bool won)
        {
            if (over) return;
            over = true; endT = 0f;
            if (GameState.Hud != null) GameState.Hud.Announce(won ? "The enemy ship is down! Galactic Assault won." : "Your ship was destroyed.", 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            shake = Mathf.Max(0f, shake - dt * 2.2f);
            feet.Clear();
            if (onFoot && foot != null && foot.gameObject.activeInHierarchy) feet.Add(foot.transform.position);
            if (friendly != null) friendly.Tick(dt, feet);
            if (enemy != null) enemy.Tick(dt, feet);
            fighters.RemoveAll(f => f == null);

            if (over)
            {
                endT += dt;
                if (endT > 8f && (InputBridge.Instance != null && (InputBridge.Instance.JumpPressed || InputBridge.Instance.Fire))) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }

            // on foot: get back in, fell off the ship
            if (onFoot && foot != null)
            {
                if (InputBridge.Instance != null && InputBridge.Instance.InteractPressed) TryGetIn();
                if (foot.transform.position.y < CloudY && !respawning && foot.health.alive)
                {
                    foot.health.TakeDamage(new DamageInfo { amount = 9999f, point = foot.transform.position, direction = Vector3.down, source = gameObject });
                }
            }

            // the enemy launches fighters while its ship lives
            if (enemy != null && enemy.alive && enemy.dying < 0f)
            {
                fighterT -= dt;
                if (fighterT <= 0f)
                {
                    fighterT = Random.Range(9f, 14f);
                    if (fighters.Count < 5)
                    {
                        var at = enemy.L2W(40f, -12f, -100f);
                        var dir = enemy.MouthDirection;
                        fighters.Add(EnemyPlane.Build(transform, lib, at, Mathf.Atan2(dir.x, dir.z)));
                    }
                }
            }
            // your carrier: sunk by their cannons
            if (friendly != null && friendly.alive && friendly.dying < 0f && friendly.home <= 0f)
            {
                friendly.StartDying();
                if (GameState.Hud != null) GameState.Hud.Announce("Your ship is going down!", 4f);
            }

            // the status line
            modeLineT -= dt;
            if (modeLineT <= 0f && GameState.Hud != null)
            {
                modeLineT = 0.25f;
                string line = "YOUR SHIP " + Mathf.RoundToInt((friendly != null ? friendly.home : 0f) * 100f) + "%";
                if (enemy != null && enemy.alive)
                {
                    int cannons = 0; foreach (var g in enemy.guns) if (g.heavy && g.part.alive) cannons++;
                    line += "   ·   " + (enemy.shield ? "Shield generator up" : (enemy.dying >= 0f ? "Their ship is falling" : "Shield down! Board their ship")) + "   ·   cannons " + cannons + "/4";
                }
                GameState.Hud.SetModeLine(line);
            }
        }
    }
}
