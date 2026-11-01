using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Everything that flies through the air and hurts: aircraft gun rounds, the carriers' cannon bolts, bombs. Stepped by hand
    /// (a ray from the last position to the new one), drawn as short tracers, so thousands cost nothing. Also the explosions.
    /// </summary>
    public class Projectiles : MonoBehaviour
    {
        public static Projectiles Instance { get; private set; }

        public class Shot
        {
            public Vector3 pos, vel;
            public float life, damage, width, gravity;
            public int team;              // 0 player side, 1 enemy
            public bool bolt, bomb, foot;
            public float blastCore, blastRadius, blastDamage;
            public Color color;
            public GameObject source;
            public Transform visual;      // bombs and bolts carry a little mesh
        }

        readonly List<Shot> shots = new List<Shot>();
        readonly Stack<Transform> boltPool = new Stack<Transform>(), bombPool = new Stack<Transform>();
        Material boltMat, bombMat, fireMat;
        class Boom { public Transform t; public Light light; public float age, size; }
        readonly List<Boom> booms = new List<Boom>();
        readonly Stack<Transform> boomPool = new Stack<Transform>();

        public static Projectiles Create(Transform parent, MaterialLibrary lib)
        {
            var go = new GameObject("Projectiles");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Projectiles>();
            p.boltMat = lib != null ? lib.Instance("glow") : null;
            if (p.boltMat != null) { p.boltMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.25f)); p.boltMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.15f) * 6f); }
            p.bombMat = lib != null ? lib.Get("gunmetal") : null;
            p.fireMat = lib != null ? lib.Instance("glow") : null;
            if (p.fireMat != null) { p.fireMat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.2f)); p.fireMat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.12f) * 5f); }
            Instance = p;
            return p;
        }

        public int Count { get { return shots.Count; } }

        /// <summary>A gun round: straight, fast, with a little drop.</summary>
        public void FireRound(Vector3 from, Vector3 dir, float speed, float damage, float range, int team, GameObject source, Color color, float width = 0.03f, bool foot = false)
        {
            shots.Add(new Shot { pos = from, vel = dir.normalized * speed, life = range / Mathf.Max(1f, speed), damage = damage, width = width, team = team, source = source, color = color, gravity = 4f, foot = foot });
        }

        /// <summary>A cannon bolt: slow, glowing, no drop.</summary>
        public void FireBolt(Vector3 from, Vector3 dir, float speed, float damage, float life, int team, GameObject source)
        {
            var s = new Shot { pos = from, vel = dir.normalized * speed, life = life, damage = damage, width = 0.25f, team = team, source = source, color = new Color(1f, 0.6f, 0.3f), bolt = true, gravity = 0f };
            s.visual = TakeVisual(boltPool, "Bolt", PrimitiveType.Sphere, 0.5f, boltMat);
            shots.Add(s);
        }

        public void DropBomb(Vector3 from, Vector3 vel, float coreR, float dmgR, float dmg, GameObject source)
        {
            var s = new Shot { pos = from, vel = vel, life = 30f, damage = 0f, width = 0f, team = 0, source = source, bomb = true, gravity = GalacticMode.BombG, blastCore = coreR, blastRadius = dmgR, blastDamage = dmg, color = Color.clear };
            s.visual = TakeVisual(bombPool, "Bomb", PrimitiveType.Capsule, 0.35f, bombMat);
            s.visual.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            shots.Add(s);
        }

        /// <summary>A tank shell or a carrier's bombardment bolt: flies, drops a little, and bursts where it lands.</summary>
        public void FireShell(Vector3 from, Vector3 dir, float speed, float coreR, float dmgR, float dmg, int team, GameObject source, bool bolt = false, float gravity = -1f)
        {
            var s = new Shot { pos = from, vel = dir.normalized * speed, life = 16f, damage = 0f, width = bolt ? 0.25f : 0.08f, team = team, source = source, bomb = true, bolt = bolt, gravity = gravity >= 0f ? gravity : (bolt ? 0f : 60f * GalacticMode.U), blastCore = coreR, blastRadius = dmgR, blastDamage = dmg, color = new Color(1f, 0.6f, 0.3f) };
            if (bolt) s.visual = TakeVisual(boltPool, "Bolt", PrimitiveType.Sphere, 0.5f, boltMat);
            shots.Add(s);
        }

        Transform TakeVisual(Stack<Transform> pool, string name, PrimitiveType type, float size, Material mat)
        {
            Transform t;
            if (pool.Count > 0) { t = pool.Pop(); t.gameObject.SetActive(true); }
            else
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                var c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
                if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            t.localScale = Vector3.one * size;
            return t;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                s.life -= dt;
                s.vel.y -= s.gravity * dt;
                if (s.bomb && s.damage == 0f && s.width == 0f)
                {
                    // bombs: terminal speed and a touch of horizontal drag, like the original
                    if (s.vel.y < -GalacticMode.BombVMax) s.vel.y = -GalacticMode.BombVMax;
                    s.vel.x *= Mathf.Max(0f, 1f - 0.3f * dt); s.vel.z *= Mathf.Max(0f, 1f - 0.3f * dt);
                }
                var next = s.pos + s.vel * dt;
                var seg = next - s.pos;
                float len = seg.magnitude;
                RaycastHit hit;
                int mask = s.team == 0 ? ~(1 << 8) : ~(1 << 9);
                bool done = false;
                if (len > 0.0001f && Physics.SphereCast(s.pos, Mathf.Max(0.02f, s.width * 0.5f), seg / len, out hit, len, mask, QueryTriggerInteraction.Ignore))
                {
                    done = true;
                    next = hit.point;
                    if (s.bomb) Explode(hit.point + hit.normal * 0.3f, s.blastCore, s.blastRadius, s.blastDamage, s.source, s.team);
                    else
                    {
                        var d = hit.collider.GetComponent<IDamageable>();
                        if (d == null) d = hit.collider.GetComponentInParent<IDamageable>();
                        if (d != null)
                        {
                            d.TakeDamage(new DamageInfo { amount = s.damage, point = hit.point, direction = s.vel.normalized, source = s.source, foot = s.foot });
                            if (s.team == 0 && GameState.Hud != null && s.source != null && s.source.GetComponent<PlaneController>() != null) GameState.Hud.HitMarker(false);
                        }
                        var ship = hit.collider.GetComponentInParent<Carrier>();
                        if (ship != null) ship.HullHit(hit.point, s);
                        if (Gunfire.Instance != null) Gunfire.Instance.Impact(hit.point, hit.normal, d != null && ship == null);
                        if (s.bolt) Flash(hit.point, 1.6f);
                    }
                }
                if (Gunfire.Instance != null && s.width > 0f)
                {
                    var tail = s.pos - s.vel.normalized * Mathf.Min(len * 3f + 0.6f, s.bolt ? 2.5f : 1.2f);
                    Gunfire.Instance.TracerLine(tail, next, s.color, s.width, 0.05f);
                }
                s.pos = next;
                if (s.visual != null)
                {
                    s.visual.position = s.pos;
                    if (s.vel.sqrMagnitude > 0.01f) s.visual.rotation = Quaternion.LookRotation(s.vel) * Quaternion.Euler(90f, 0f, 0f);
                }
                if (done || s.life <= 0f || s.pos.y < -50f)
                {
                    if (s.bomb && !done) Explode(s.pos, s.blastCore, s.blastRadius, s.blastDamage, s.source, s.team);
                    if (s.visual != null) { s.visual.gameObject.SetActive(false); (s.bomb && !s.bolt ? bombPool : boltPool).Push(s.visual); }
                    shots.RemoveAt(i);
                }
            }
            for (int i = booms.Count - 1; i >= 0; i--)
            {
                var b = booms[i]; b.age += dt;
                float k = b.age / 0.7f;
                if (k >= 1f) { b.t.gameObject.SetActive(false); boomPool.Push(b.t); booms.RemoveAt(i); continue; }
                b.t.localScale = Vector3.one * b.size * (0.3f + 0.7f * Mathf.Sqrt(k));
                if (b.light != null) b.light.intensity = Mathf.Lerp(b.size * 3f, 0f, k);
            }
        }

        void Flash(Vector3 at, float size)
        {
            Transform t;
            if (boomPool.Count > 0) { t = boomPool.Pop(); t.gameObject.SetActive(true); }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Boom";
                var c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
                if (fireMat != null) go.GetComponent<Renderer>().sharedMaterial = fireMat;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.3f); l.shadows = LightShadows.None;
                t = go.transform;
            }
            t.position = at;
            var light = t.GetComponent<Light>(); light.range = size * 6f;
            booms.Add(new Boom { t = t, light = light, age = 0f, size = size });
        }

        /// <summary>Blast damage: full inside coreR, falling to nothing at dmgR. Ship parts use their bomb multiplier.</summary>
        public void Explode(Vector3 at, float coreR, float dmgR, float dmg, GameObject source, int team)
        {
            Flash(at, Mathf.Max(1f, dmgR * 0.45f));
            var seen = new HashSet<Object>();
            foreach (var c in Physics.OverlapSphere(at, dmgR, ~0, QueryTriggerInteraction.Ignore))
            {
                var d = c.GetComponent<IDamageable>(); if (d == null) d = c.GetComponentInParent<IDamageable>();
                if (d == null) continue;
                var hz = d as HitZone;
                Object key = hz != null ? (Object)hz.owner : (Object)(d as Component);
                if (key == null || !seen.Add(key)) continue;
                float dist = Vector3.Distance(at, c.ClosestPoint(at));
                float k = dist <= coreR ? 1f : 1f - Mathf.Clamp01((dist - coreR) / Mathf.Max(0.01f, dmgR - coreR));
                if (k <= 0f) continue;
                var target = hz != null ? (IDamageable)hz.owner : d;
                if (target == null) continue;
                target.TakeDamage(new DamageInfo { amount = dmg * k, point = c.ClosestPoint(at), direction = (c.transform.position - at).normalized, source = source, explosive = true });
            }
            if (GameState.Rig != null && GameState.Rig.cam != null)
            {
                float shake = Mathf.Clamp01(1f - Vector3.Distance(at, GameState.Rig.cam.transform.position) / (dmgR * 3f));
                if (shake > 0f && GalacticMode.Instance != null) GalacticMode.Instance.Shake(shake * 0.6f);
            }
        }
    }
}
