using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// A carrier's turret: the heavy cannons on the upper deck (slow glowing bolts) and the quick guns on the hull (three-round
    /// bursts). They track aircraft within range; with nothing to shoot at, the enemy's heavy cannons shell your carrier.
    /// </summary>
    public class ShipGun : MonoBehaviour
    {
        public bool heavy, under;
        public Carrier ship;
        public ShipPart part;
        public Transform turret, barrels;
        float cd, flash;
        const float U = GalacticMode.U;

        public static ShipGun Build(Carrier ship, MaterialLibrary lib, bool heavy, bool under, float lx, float ly, float lz)
        {
            float k = heavy ? 1f : 0.55f;
            var go = new GameObject(heavy ? "Cannon" : "Gun");
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) { metal.SetColor("_BaseColor", new Color(0.3f, 0.31f, 0.34f)); metal.SetFloat("_Metallic", 0.5f); }
            var red = lib != null ? lib.Instance("glow") : null; if (red != null) { red.SetColor("_BaseColor", new Color(1f, 0.2f, 0.15f)); red.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.1f) * 4f); }
            GameObject P(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Material m)
            {
                var p = GameObject.CreatePrimitive(t); p.name = n; p.transform.SetParent(parent, false); p.transform.localPosition = pos * U * k; p.transform.localScale = scale * U * k;
                if (m != null) p.GetComponent<Renderer>().sharedMaterial = m; return p;
            }
            var root = go.transform;
            P(PrimitiveType.Cube, "Base", root, new Vector3(0f, 2f, 0f), new Vector3(16f, 4f, 16f), metal);
            var tur = new GameObject("Turret").transform; tur.SetParent(root, false); tur.localPosition = new Vector3(0f, 6.5f * U * k, 0f);
            P(PrimitiveType.Cube, "Head", tur, Vector3.zero, new Vector3(11f, 5f, 11f), metal);
            var bar = new GameObject("Barrels").transform; bar.SetParent(tur, false); bar.localPosition = new Vector3(0f, 1f * U * k, 0f);
            var b1 = P(PrimitiveType.Cylinder, "Barrel", bar, new Vector3(-2.2f, 0f, 9f), new Vector3(1.6f, 9f, 1.6f), metal); b1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var b2 = P(PrimitiveType.Cylinder, "Barrel", bar, new Vector3(2.2f, 0f, 9f), new Vector3(1.6f, 9f, 1.6f), metal); b2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            P(PrimitiveType.Sphere, "Lamp", tur, new Vector3(0f, 3.2f, 0f), new Vector3(2f, 2f, 2f), red);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.gameObject.layer = 0;
            var g = go.AddComponent<ShipGun>();
            g.heavy = heavy; g.under = under; g.ship = ship; g.turret = tur; g.barrels = bar;
            g.part = ship.AddPart(heavy ? "scannon" : "sgun", lx, ly, lz, (heavy ? 538f : 192f) * GalacticMode.K, heavy ? 0.35f : 0.5f, 1.2f, go);
            if (under) go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            g.cd = Random.Range(1f, 3f);
            ship.guns.Add(g);
            return g;
        }

        void Update()
        {
            if (part == null || !part.alive || ship == null || !ship.alive || ship.dying >= 0f) return;
            float dt = Time.deltaTime;
            cd -= dt;
            var gm = GalacticMode.Instance; if (gm == null) return;
            float range = (heavy ? 950f : 700f) * U;
            Vector3 me = turret.position;
            // a target: the player while airborne, within range, on the right side of the deck
            Transform target = null; Vector3 tvel = Vector3.zero;
            var pl = gm.plane;
            if (pl != null && pl.Active && pl.Airborne && pl.health.alive)
            {
                var d = pl.transform.position - me;
                bool below = pl.transform.position.y < me.y - 4f * U;
                if (d.magnitude < range && (under ? below : !below)) { target = pl.transform; tvel = pl.Velocity; }
            }
            if (target == null && !under)
            {
                foreach (var w in gm.allyPlanes) { if (w == null) continue; var d = w.transform.position - me; if (d.magnitude < range && w.transform.position.y >= me.y - 4f * U) { target = w.transform; tvel = w.Velocity; break; } }
            }
            float speed = (heavy ? 280f : 440f) * U;
            if (target != null)
            {
                var aim = Lead(me, target.position + Vector3.up * 0.5f, tvel, speed);
                Face(aim, dt);
                if (cd <= 0f)
                {
                    if (heavy) { Fire(aim, speed, 24f, 2.4f); cd = Random.Range(2.2f, 3.2f); }
                    else { for (int i = 0; i < 3; i++) Fire(aim + Random.insideUnitSphere * 0.9f, speed, 5f, 2.4f); cd = Random.Range(0.9f, 1.5f); }
                }
            }
            else if (heavy && ship.side == 1 && gm.friendly != null && gm.friendly.alive && gm.friendly.dying < 0f)
            {
                // nothing in the air: shell the other carrier
                var F = gm.friendly.transform.position;
                var aim = F + new Vector3(Random.Range(-280f, 280f) * U, Random.Range(-20f, 30f) * U, (F.z > me.z ? -80f : 80f) * U);
                Face(aim, dt);
                if (cd <= 0f) { Fire(aim, 300f * U, 30f, 16f); cd = Random.Range(3.2f, 4.6f); }
            }
            flash = Mathf.Max(0f, flash - dt * 4f);
        }

        static Vector3 Lead(Vector3 from, Vector3 target, Vector3 vel, float speed)
        {
            float t = Vector3.Distance(from, target) / Mathf.Max(1f, speed) * 0.92f;
            return target + vel * t + Random.insideUnitSphere * (0.05f * Vector3.Distance(from, target) * 0.3f);
        }

        void Face(Vector3 aim, float dt)
        {
            var to = aim - turret.position;
            var flat = Vector3.ProjectOnPlane(to, transform.up);
            if (flat.sqrMagnitude > 0.001f)
            {
                var want = Quaternion.LookRotation(transform.InverseTransformDirection(flat.normalized), Vector3.up);
                turret.localRotation = Quaternion.RotateTowards(turret.localRotation, want, 160f * dt);
            }
            float pitch = -Mathf.Atan2(Vector3.Dot(to, transform.up), flat.magnitude) * Mathf.Rad2Deg;
            barrels.localRotation = Quaternion.RotateTowards(barrels.localRotation, Quaternion.Euler(Mathf.Clamp(pitch, -80f, 20f), 0f, 0f), 120f * dt);
        }

        void Fire(Vector3 aim, float speed, float dmg, float life)
        {
            var from = barrels.position + barrels.forward * 1.4f * (heavy ? 1f : 0.55f);
            var dir = (aim - from).normalized;
            if (Projectiles.Instance == null) return;
            if (heavy) Projectiles.Instance.FireBolt(from, dir, speed, dmg, life, 1, gameObject);
            else Projectiles.Instance.FireRound(from, dir, speed, dmg, speed * life, 1, gameObject, new Color(1f, 0.5f, 0.3f), 0.05f);
            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, heavy ? 2.5f : 1.2f);
            flash = 1f;
        }
    }
}
