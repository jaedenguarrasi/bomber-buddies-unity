using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Builds the original game's kind of geometry: boxes, n-sided prisms and cones, squashed blobs and loose triangles, one flat
    /// colour per face, into one mesh with vertex colours and per-face normals. Positions are in original units; Scale converts.
    /// </summary>
    public class MeshBuilder
    {
        public float scale = GalacticMode.U;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<Vector2> uvs = new List<Vector2>();
        /// <summary>The surface written with each corner, as the original's MAT: 1 plain, 2 metal panels, 3 brick, 4 plaster or concrete, 5 roof tiles, 6 wood, 7 grass and ground, 8 water, 9 leaves, 10 glass, 11 rubble and rock, 12 skin, 13 camouflage cloth, 14 scales, 15 leather and kit, 16 fur.</summary>
        public float mat = 1f;
        readonly List<int> tris = new List<int>();
        public int VertexCount { get { return verts.Count; } }

        public static Color32 Rgb(int r, int g, int b, byte a = 255) { return new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), a); }
        public static Color32 Mul(Color32 c, float k) { return new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * k), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * k), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * k), 0, 255), c.a); }
        public static Color32 Mix(Color32 a, Color32 b, float t) { return new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)), (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)), (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)), a.a); }
        /// <summary>Alpha 254 marks an emissive (unlit) vertex, as in the original.</summary>
        public static Color32 Emissive(Color32 c) { c.a = 254; return c; }

        /// <summary>A triangle with a flat normal; winding counter-clockwise seen from outside (Unity's convention is clockwise, so the normal is computed and the order fixed).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = verts.Count;
            verts.Add(a * scale); verts.Add(b * scale); verts.Add(c * scale);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(col); colors.Add(col); colors.Add(col);
            var uv = new Vector2(mat, 0f); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>A triangle with its own colour and normal at each corner (the original's smooth ground).</summary>
        public void TriSmooth(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc, Vector3 na, Vector3 nb, Vector3 nc)
        {
            int i = verts.Count;
            verts.Add(a * scale); verts.Add(b * scale); verts.Add(c * scale);
            normals.Add(na); normals.Add(nb); normals.Add(nc);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            var uv = new Vector2(mat, 0f); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>A quad a-b-c-d with the normal of (b-a)×(d-a), given with Unity's clockwise winding seen from the front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            Tri(a, b, c, col); Tri(a, c, d, col);
        }

        /// <summary>An axis-aligned box from min to max corners, one colour, optionally shaded per face like the original's box drawer.</summary>
        public void Box(Vector3 min, Vector3 max, Color32 col, bool shade = false, bool top = true, bool bottom = true)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            var cT = col; var cB = shade ? Mul(col, 0.7f) : col; var cX = shade ? Mul(col, 0.85f) : col; var cZ = shade ? Mul(col, 0.8f) : col;
            if (top) Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), cT);
            if (bottom) Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), cB);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), cX);   // +x
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), cX);   // -x
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), cZ);   // -z
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), cZ);   // +z
        }

        public void Box(float x0, float x1, float y0, float y1, float z0, float z1, Color32 col, bool shade = false) { Box(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), col, shade); }

        /// <summary>A box of size (length, height, width) rotated by yaw about its centre (cx, cy, cz).</summary>
        public void BoxYaw(float cx, float cy, float cz, float len, float h, float wid, float yaw, Color32 col, bool shade = false)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            Vector3 P(float dx, float dy, float dz) { return new Vector3(cx + dx * c - dz * s, cy + dy, cz + dx * s + dz * c); }
            float hl = len * 0.5f, hh = h * 0.5f, hw = wid * 0.5f;
            var cT = col; var cB = shade ? Mul(col, 0.7f) : col; var cX = shade ? Mul(col, 0.85f) : col; var cZ = shade ? Mul(col, 0.8f) : col;
            Quad(P(-hl, hh, -hw), P(-hl, hh, hw), P(hl, hh, hw), P(hl, hh, -hw), cT);
            Quad(P(-hl, -hh, -hw), P(hl, -hh, -hw), P(hl, -hh, hw), P(-hl, -hh, hw), cB);
            Quad(P(hl, -hh, -hw), P(hl, hh, -hw), P(hl, hh, hw), P(hl, -hh, hw), cX);
            Quad(P(-hl, -hh, hw), P(-hl, hh, hw), P(-hl, hh, -hw), P(-hl, -hh, -hw), cX);
            Quad(P(-hl, -hh, -hw), P(-hl, hh, -hw), P(hl, hh, -hw), P(hl, -hh, -hw), cZ);
            Quad(P(hl, -hh, hw), P(hl, hh, hw), P(-hl, hh, hw), P(-hl, -hh, hw), cZ);
        }

        /// <summary>The original's dbox/dfrus in an entity basis: a box at offset (forward ox, up oy, right oz) of size (length, height,
        /// width); the front face scaled by (ky, kz) and lifted by fo makes a tapered nose or tail. Forward is +z, right is +x.</summary>
        public void Frustum(float ox, float oy, float oz, float len, float h, float w, float ky, float kz, float fo, Color32 col, bool shade = true)
        {
            float hl = len * 0.5f, hh = h * 0.5f, hw = w * 0.5f;
            Vector3 P(float f, float u, float r) { return new Vector3(oz + r, oy + u, ox + f); }
            // back face corners (b) and front face corners (f): indices 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right, seen from the front
            var b0 = P(-hl, -hh, -hw); var b1 = P(-hl, hh, -hw); var b2 = P(-hl, hh, hw); var b3 = P(-hl, -hh, hw);
            var f0 = P(hl, -hh * ky + fo, -hw * kz); var f1 = P(hl, hh * ky + fo, -hw * kz); var f2 = P(hl, hh * ky + fo, hw * kz); var f3 = P(hl, -hh * ky + fo, hw * kz);
            var cT = col; var cB = shade ? Mul(col, 0.7f) : col; var cS = shade ? Mul(col, 0.85f) : col; var cF = shade ? Mul(col, 0.8f) : col;
            Quad(f0, f3, f2, f1, cF);          // front
            Quad(b1, b2, b3, b0, cF);          // back
            Quad(f1, f2, b2, b1, cT);          // top
            Quad(b0, b3, f3, f0, cB);          // bottom
            Quad(b3, b2, f2, f3, cS);          // right (+x)
            Quad(b1, b0, f0, f1, cS);          // left (-x)
        }

        public void DBox(float ox, float oy, float oz, float len, float h, float w, Color32 col, bool shade = true) { Frustum(ox, oy, oz, len, h, w, 1f, 1f, 0f, col, shade); }

        /// <summary>The original's prism3: an n-sided frustum from radius r0 at y0 to r1 at y1 (r1 = 0 makes a cone), per-side flat normals, radii stretched by (ex, ez).</summary>
        public void Prism(float cx, float y0, float y1, float cz, float r0, float r1, int n, float rot, float ex, float ez, Color32 col, bool capBottom = true)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = rot + i * Mathf.PI * 2f / n, a1 = rot + (i + 1) * Mathf.PI * 2f / n;
                var b0 = new Vector3(cx + Mathf.Cos(a0) * r0 * ex, y0, cz + Mathf.Sin(a0) * r0 * ez);
                var b1 = new Vector3(cx + Mathf.Cos(a1) * r0 * ex, y0, cz + Mathf.Sin(a1) * r0 * ez);
                var t0 = new Vector3(cx + Mathf.Cos(a0) * r1 * ex, y1, cz + Mathf.Sin(a0) * r1 * ez);
                var t1 = new Vector3(cx + Mathf.Cos(a1) * r1 * ex, y1, cz + Mathf.Sin(a1) * r1 * ez);
                if (r1 > 0.001f) Quad(b0, t0, t1, b1, col); else Tri(b0, new Vector3(cx, y1, cz), b1, col);
                if (r1 > 0.001f) Tri(new Vector3(cx, y1, cz), t1, t0, col);
                if (capBottom && r0 > 0.001f) Tri(new Vector3(cx, y0, cz), b0, b1, col);
            }
        }

        public void Prism(float cx, float y0, float y1, float cz, float r0, float r1, int n, float rot, Color32 col) { Prism(cx, y0, y1, cz, r0, r1, n, rot, 1f, 1f, col); }

        /// <summary>The original's blob3: a squashed low-poly ball from four stacked prisms.</summary>
        public void Blob(float cx, float cy, float cz, float rx, float ry, float rz, int n, Color32 col)
        {
            Prism(cx, cy - 0.55f * ry, cy, cz, 0.62f, 1f, n, 0.3f, rx, rz, Mul(col, 0.9f), false);
            Prism(cx, cy, cy + 0.6f * ry, cz, 1f, 0.72f, n, 0.3f, rx, rz, col, false);
            Prism(cx, cy + 0.6f * ry, cy + ry, cz, 0.72f, 0f, n, 0.3f, rx, rz, col, false);
            Prism(cx, cy - 0.56f * ry, cy - 0.55f * ry, cz, 0f, 0.62f, n, 0.3f, rx, rz, Mul(col, 0.7f), true);
        }

        public Mesh Build(string name = "mesh")
        {
            var m = new Mesh { name = name };
            m.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts); m.SetNormals(normals); m.SetColors(colors); m.SetUVs(3, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        public void Clear() { verts.Clear(); normals.Clear(); colors.Clear(); uvs.Clear(); tris.Clear(); }

        /// <summary>A game object showing this mesh with the low-poly material, optionally with a mesh collider.</summary>
        public GameObject Spawn(string name, Transform parent, Material mat, bool collider, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = Build(name);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
            return go;
        }
    }
}
