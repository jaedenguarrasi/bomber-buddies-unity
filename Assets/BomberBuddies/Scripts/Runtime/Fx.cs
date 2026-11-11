using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The original's particles, ported as they are: every blast, puff of dust and shower of dirt is a little cube or a soft
    /// eight-sided puff with the original's own speeds, sizes, lives and colours (its part3 / updateFx3 / fx3Explosion / dust3 /
    /// debris3). Kinds: 0 smoke (drifts up, see-through), 1 debris (a cube that falls), 2 spark (a glowing cube that falls),
    /// 3 flash (a glowing puff), 4 dust (rolls out, hangs, settles). Speeds and sizes are given in the original's units.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }
        const float U = GalacticMode.U;
        const int MaxParts = 900;

        struct P { public Vector3 p, v; public float life, max, size; public byte type; public Color32 c; }
        readonly List<P> parts = new List<P>(MaxParts);
        Material solidMat, softMat;
        Mesh solid, soft;
        readonly List<Vector3> sv = new List<Vector3>(), sn = new List<Vector3>(), fv = new List<Vector3>();
        readonly List<Color32> sc = new List<Color32>(), fc = new List<Color32>();
        readonly List<Vector2> fu = new List<Vector2>();
        readonly List<int> st = new List<int>(), ft = new List<int>();

        public static Fx Create(Transform parent, MaterialLibrary lib)
        {
            var go = new GameObject("Fx"); go.transform.SetParent(parent, false);
            var f = go.AddComponent<Fx>();
            f.solidMat = lib != null ? lib.Get("lowpoly") : null;
            var sm = lib != null ? lib.Get("soft") : null;
            if (sm == null || sm.shader == null || sm.shader.name != "BomberBuddies/Soft")
            {
                var sh = Shader.Find("BomberBuddies/Soft");
                sm = sh != null ? new Material(sh) : (lib != null ? lib.Instance("particle") : null);
            }
            f.softMat = sm;
            f.solid = new Mesh { name = "Fx cubes" }; f.solid.MarkDynamic();
            f.soft = new Mesh { name = "Fx puffs" }; f.soft.MarkDynamic();
            Instance = f;
            return f;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static float Rnd(float a, float b) { return Random.Range(a, b); }
        static float Ground(Vector3 p) { return World3.Instance != null ? World3.Instance.GroundMetres(p.x, p.z) : -1e9f; }

        /// <summary>One particle: where (world metres), how fast (original units a second), how long, how big (original units), which kind, what colour.</summary>
        public void Part(Vector3 at, float vx, float vy, float vz, float life, float size, int type, float r, float g, float b)
        {
            if (parts.Count >= MaxParts) return;
            parts.Add(new P { p = at, v = new Vector3(vx, vy, vz), life = life, max = life, size = size, type = (byte)type, c = new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), 255) });
        }

        /// <summary>The original's blast: a white-hot flash, a boil of fire and black smoke, dirt thrown up if it is near the ground, and a few sparks. r is the original's blast size in its units.</summary>
        public void Explosion(Vector3 at, float r)
        {
            float k = r / 30f;
            Part(at, 0f, 0f, 0f, 0.22f, r * 1.3f, 3, 255, 241, 196);
            int n = Mathf.RoundToInt(8f + 8f * k);
            for (int i = 0; i < n; i++)
            {
                bool hot = Random.value < 0.4f;
                Part(at + new Vector3(Rnd(-r, r) * 0.4f, Rnd(0f, r) * 0.4f, Rnd(-r, r) * 0.4f) * U, Rnd(-30f, 30f) * k, Rnd(10f, 70f) * k, Rnd(-30f, 30f) * k, Rnd(0.8f, 1.7f), Rnd(6f, 12f) * (0.6f + k * 0.5f), 0, hot ? 240 : 74, hot ? 162 : 67, hot ? 60 : 61);
            }
            float above = (at.y - Ground(at)) / U;
            if (above < r * 1.5f)
            {
                n = Mathf.RoundToInt(6f + 12f * k);
                for (int i = 0; i < n; i++) Part(at + new Vector3(Rnd(-r, r) * 0.5f, 2f, Rnd(-r, r) * 0.5f) * U, Rnd(-110f, 110f), Rnd(90f, 260f) * (0.5f + k * 0.5f), Rnd(-110f, 110f), Rnd(0.8f, 1.6f), Rnd(2.5f, 5f), 1, 134, 96, 60);
            }
            for (int i = 0; i < 5; i++) Part(at, Rnd(-180f, 180f), Rnd(40f, 240f), Rnd(-180f, 180f), Rnd(0.25f, 0.5f), 2.2f, 2, 255, 210, 122);
            // the earth goes up: a cloud of dust that rolls out and hangs (the original does this where a blast craters the ground)
            if (above < r * 0.9f) { var g = at; g.y = Ground(at) + 1f * U; Dust(g, 6f + r * 0.35f, r * 0.7f, r * 2.2f + 20f, 10f + r * 0.4f, new Color32(140, 122, 94, 255), 2.4f + r * 0.03f); }
        }

        /// <summary>A cloud of dust: n puffs within r, rolling outward at about `outSpeed`, lifted by up to `up`; it slows, hangs and settles.</summary>
        public void Dust(Vector3 at, float n, float r, float outSpeed, float up, Color32 col, float life = 3f)
        {
            int q = Mathf.Max(1, Mathf.RoundToInt(n));
            for (int i = 0; i < q; i++)
            {
                float a = Rnd(0f, Mathf.PI * 2f), d = r * Mathf.Sqrt(Random.value), ca = Mathf.Cos(a), sa = Mathf.Sin(a), kk = Rnd(0.9f, 1.08f), sp = outSpeed * Rnd(0.5f, 1.2f);
                Part(at + new Vector3(ca * d, Rnd(0f, 2f), sa * d) * U, ca * sp + Rnd(-6f, 6f), Rnd(1f, up), sa * sp + Rnd(-6f, 6f), life * Rnd(0.7f, 1.3f), Rnd(3f, 6f) * (0.6f + r * 0.03f), 4, col.r * kk, col.g * kk, col.b * kk);
            }
        }

        /// <summary>Bits of a wall or a machine flying off: one in four is a shard of glass.</summary>
        public void Debris(Vector3 at, int n, Color32 col, float spread, float up)
        {
            for (int i = 0; i < n; i++)
            {
                bool gl = i % 4 == 0; var c = gl ? new Color32(150, 190, 205, 255) : col; float kk = 0.6f + Random.value * 0.5f;
                Part(at + new Vector3(Rnd(-spread, spread), Rnd(-6f, 6f), Rnd(-spread, spread)) * U, Rnd(-90f, 90f), Rnd(-20f, up), Rnd(-90f, 90f), Rnd(1f, 2.2f), gl ? Rnd(1f, 2f) : Rnd(2.5f, 6f), 1, c.r * kk, c.g * kk, c.b * kk);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            float drag0 = Mathf.Pow(0.97f, dt * 60f), drag4 = Mathf.Exp(-1.7f * dt);
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var q = parts[i]; q.life -= dt;
                if (q.life <= 0f) { parts[i] = parts[parts.Count - 1]; parts.RemoveAt(parts.Count - 1); continue; }
                if (q.type == 1 || q.type == 2) { q.v.y -= 420f * dt; if (q.p.y < Ground(q.p)) q.life = Mathf.Min(q.life, 0.05f); }
                else if (q.type == 0) { q.v.x *= drag0; q.v.z *= drag0; q.v.y = q.v.y * drag0 + 8f * dt; }
                else if (q.type == 4) { q.v.x *= drag4; q.v.z *= drag4; q.v.y = Mathf.Max(-3f, q.v.y * drag4 - 2f * dt); }
                q.p += q.v * (U * dt);
                if (q.type == 4) { float gy = Ground(q.p) + q.size * 0.35f * U; if (q.p.y < gy) { q.p.y = gy; q.v.y = 0f; } }
                parts[i] = q;
            }
        }

        void LateUpdate()
        {
            sv.Clear(); sn.Clear(); sc.Clear(); st.Clear(); fv.Clear(); fc.Clear(); fu.Clear(); ft.Clear();
            if (parts.Count > 0)
            {
                var sun = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : new Vector3(0.4f, 0.8f, 0.4f).normalized;
                var cam = GameState.Rig != null && GameState.Rig.cam != null ? GameState.Rig.cam.transform.position : Vector3.zero;
                for (int i = 0; i < parts.Count; i++)
                {
                    var q = parts[i]; float k = q.life / q.max;
                    if (q.type == 1 || q.type == 2) { Cube(q.p, q.size * U, q.type == 2 ? new Color32(q.c.r, q.c.g, q.c.b, 254) : q.c); continue; }
                    // puffs right in the lens fade out, as the original's do
                    float dist = Vector3.Distance(q.p, cam) / U, nf = Mathf.Clamp01((dist - 12f) / 28f); if (nf <= 0f) continue;
                    if (q.type == 3) { Puff(q.p, q.size * (1.25f - k * 0.6f) * U, q.c, sun, nf); continue; }
                    float sz = q.type == 4 ? q.size * (0.55f + (1f - k) * 1.6f) * (0.45f + 0.55f * Mathf.Min(1f, (1f - k) * 7f)) : q.size * 0.72f * (1f + (1f - k) * 0.8f) * (0.4f + 0.6f * Mathf.Min(1f, k * 3f));
                    float al = nf * Mathf.Min(1f, k * (q.type == 4 ? 2.2f : 1.6f));
                    SoftPuff(q.p, sz * 1.15f * U, q.c, al, (q.max * 7.1f + q.size * 3.3f) % (Mathf.PI * 2f), sun);
                }
            }
            solid.Clear(); soft.Clear();
            if (sv.Count > 0 && solidMat != null)
            {
                solid.SetVertices(sv); solid.SetNormals(sn); solid.SetColors(sc); solid.SetTriangles(st, 0); solid.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
                Graphics.DrawMesh(solid, Matrix4x4.identity, solidMat, gameObject.layer, null, 0, null, false, false);
            }
            if (fv.Count > 0 && softMat != null)
            {
                soft.SetVertices(fv); soft.SetColors(fc); soft.SetUVs(0, fu); soft.SetTriangles(ft, 0); soft.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
                Graphics.DrawMesh(soft, Matrix4x4.identity, softMat, gameObject.layer, null, 0, null, false, false);
            }
        }

        static readonly Vector3[] CubeN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        void Cube(Vector3 c, float s, Color32 col)
        {
            float h = s * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                var n = CubeN[f]; var a = new Vector3(n.y, n.z, n.x); var b = Vector3.Cross(n, a);
                int i = sv.Count;
                sv.Add(c + (n - a - b) * h); sv.Add(c + (n + a - b) * h); sv.Add(c + (n + a + b) * h); sv.Add(c + (n - a + b) * h);
                sn.Add(n); sn.Add(n); sn.Add(n); sn.Add(n); sc.Add(col); sc.Add(col); sc.Add(col); sc.Add(col);
                st.Add(i); st.Add(i + 1); st.Add(i + 2); st.Add(i); st.Add(i + 2); st.Add(i + 3);
            }
        }

        // the original's dsoft: eight faces, each fading from a solid middle to clear points, so a cloud of them has no hard edges
        void SoftPuff(Vector3 c, float s, Color32 col, float alpha, float yaw, Vector3 sun)
        {
            float h = s * 0.72f, cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw); byte va = (byte)Mathf.Clamp(Mathf.RoundToInt(253f * alpha), 0, 253);
            var lit = new Vector2(0f, 0f);
            for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) for (int d = -1; d <= 1; d += 2)
            {
                float sh = 0.68f + 0.32f * Mathf.Max(0f, (a * sun.x + b * sun.y + d * sun.z) * 0.577f);
                var cm = new Color32((byte)(col.r * sh), (byte)(col.g * sh), (byte)(col.b * sh), va); var ce = new Color32(cm.r, cm.g, cm.b, 0);
                var A = new Vector3(c.x + cy * a * h, c.y, c.z + sy * a * h); var B = new Vector3(c.x, c.y + b * h, c.z); var C = new Vector3(c.x - sy * d * h, c.y, c.z + cy * d * h);
                var M = new Vector3((A.x + C.x + c.x) / 3f, c.y + b * h * 0.34f, (A.z + C.z + c.z) / 3f);
                int i = fv.Count;
                fv.Add(M); fv.Add(A); fv.Add(B); fv.Add(C);
                fc.Add(cm); fc.Add(ce); fc.Add(ce); fc.Add(ce); fu.Add(lit); fu.Add(lit); fu.Add(lit); fu.Add(lit);
                ft.Add(i); ft.Add(i + 1); ft.Add(i + 2); ft.Add(i); ft.Add(i + 2); ft.Add(i + 3); ft.Add(i); ft.Add(i + 3); ft.Add(i + 1);
            }
        }

        // the original's dpuff: a glowing eight-sided flash
        void Puff(Vector3 c, float s, Color32 col, Vector3 sun, float alpha)
        {
            float h = s * 0.66f; var glow = new Vector2(1f, 0f); byte va = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * alpha), 0, 255);
            for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) for (int d = -1; d <= 1; d += 2)
            {
                float sh = 0.62f + 0.38f * Mathf.Max(0f, (a * sun.x + b * sun.y + d * sun.z) * 0.577f);
                var cc = new Color32((byte)(col.r * sh), (byte)(col.g * sh), (byte)(col.b * sh), va);
                int i = fv.Count;
                fv.Add(new Vector3(c.x + a * h, c.y, c.z)); fv.Add(new Vector3(c.x, c.y + b * h, c.z)); fv.Add(new Vector3(c.x, c.y, c.z + d * h));
                fc.Add(cc); fc.Add(cc); fc.Add(cc); fu.Add(glow); fu.Add(glow); fu.Add(glow);
                ft.Add(i); ft.Add(i + 1); ft.Add(i + 2);
            }
        }
    }
}
