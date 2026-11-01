using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The original's gore (loseLimb, ragShot, splat3): limbs come off in blasts and under heavy fire, heads go on a
    /// headshot, pieces tumble and come to rest, and blood splats stay on the ground. Capped so phones keep up.</summary>
    public class Gore : MonoBehaviour
    {
        public static Gore Instance { get; private set; }
        class Piece { public Transform t; public Vector3 v, spin; public float life, floor; }
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<GameObject> splats = new List<GameObject>();
        Material blood; MaterialLibrary lib;

        public static Gore Create(Transform parent, MaterialLibrary lib)
        {
            var go = new GameObject("Gore"); go.transform.SetParent(parent, false);
            var g = go.AddComponent<Gore>(); g.lib = lib; Instance = g;
            var sh = Shader.Find("BomberBuddies/LowPoly"); g.blood = lib != null ? lib.Get("lowpoly") : null;
            return g;
        }

        /// <summary>A limb (or head) detached from a body: it keeps its mesh and flies off from the hit.</summary>
        public void Detach(Transform limb, Vector3 fromDir, float force)
        {
            if (limb == null || pieces.Count > 40) return;
            limb.SetParent(transform, true);
            var v = (fromDir.normalized * 0.6f + Random.insideUnitSphere * 0.5f + Vector3.up * 0.7f).normalized * force;
            pieces.Add(new Piece { t = limb, v = v, spin = Random.insideUnitSphere * 540f, life = 25f, floor = GameState.GroundHeight != null ? GameState.GroundHeight(limb.position.x, limb.position.z) : limb.position.y - 1f });
            Splat(limb.position, 0.6f);
        }

        /// <summary>Blood on the ground under a point: a dark red patch that stays.</summary>
        public void Splat(Vector3 at, float size)
        {
            if (splats.Count > 90) { Destroy(splats[0]); splats.RemoveAt(0); }
            float g = GameState.GroundHeight != null ? GameState.GroundHeight(at.x, at.z) : at.y;
            if (g < GalacticMode.SeaAt(at.x, at.z) + 0.2f) return;
            var mb = new MeshBuilder();
            float s = size * Random.Range(0.8f, 1.4f) / GalacticMode.U;
            var col = MeshBuilder.Rgb(118, 14, 10);
            // an irregular patch: a few overlapping flat boxes
            for (int i = 0; i < 4; i++) { float ox = Random.Range(-s * 0.35f, s * 0.35f), oz = Random.Range(-s * 0.35f, s * 0.35f), r = s * Random.Range(0.3f, 0.6f); mb.Box(ox - r, ox + r, 0f, 0.08f, oz - r * Random.Range(0.6f, 1f), oz + r * Random.Range(0.6f, 1f), col, true); }
            var go = mb.Spawn("Blood", transform, blood, false, false);
            go.transform.position = new Vector3(at.x, g + 0.03f, at.z); go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            splats.Add(go);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i]; if (p.t == null) { pieces.RemoveAt(i); continue; }
                p.life -= dt;
                if (p.v != Vector3.zero)
                {
                    p.v.y -= 18f * dt;
                    var pos = p.t.position + p.v * dt;
                    if (pos.y <= p.floor + 0.12f) { pos.y = p.floor + 0.12f; p.v = new Vector3(p.v.x * 0.4f, -p.v.y * 0.2f, p.v.z * 0.4f); p.spin *= 0.4f; if (p.v.sqrMagnitude < 0.5f) { p.v = Vector3.zero; p.spin = Vector3.zero; Splat(pos, 0.4f); } }
                    p.t.position = pos; p.t.Rotate(p.spin * dt, Space.World);
                }
                if (p.life <= 0f) { Destroy(p.t.gameObject); pieces.RemoveAt(i); }
            }
        }
    }
}
