using System;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>A dropship from one of the carriers: 11 s down to the landing spot, 2 s on the ground unloading, then 9 s back up
    /// and gone. Either side's; the cargo is whatever the mode hands it.</summary>
    public class Dropship : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public int side;
        public Action<Vector3> cargo;
        Vector3 start, land;
        float t;
        bool unloaded;
        Transform model;
        Light glow;

        public static Dropship Launch(Transform parent, MaterialLibrary lib, int side, Vector3 from, Vector3 to)
        {
            var go = new GameObject(side == 0 ? "Your dropship" : "Enemy dropship");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<Dropship>();
            d.side = side; d.start = from; d.land = to;
            go.transform.position = from;
            d.BuildModel(lib);
            return d;
        }

        void BuildModel(MaterialLibrary lib)
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            var paint = lib != null ? lib.Instance("gunmetal") : null; if (paint != null) { paint.SetColor("_BaseColor", side == 0 ? new Color(0.45f, 0.5f, 0.56f) : new Color(0.38f, 0.34f, 0.36f)); paint.SetFloat("_Metallic", 0.3f); }
            var engineC = side == 0 ? new Color(120f / 255f, 190f / 255f, 1f) : new Color(1f, 90f / 255f, 60f / 255f);
            var glowM = lib != null ? lib.Instance("glow") : null; if (glowM != null) { glowM.SetColor("_BaseColor", engineC); glowM.SetColor("_EmissionColor", engineC * 5f); }
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; Destroy(g.GetComponent<Collider>()); g.transform.SetParent(model, false); g.transform.localPosition = pos * U; g.transform.localScale = scale * U;
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            P(PrimitiveType.Cube, "Body", new Vector3(0f, 4.5f, 0f), new Vector3(16f, 9f, 34f), paint);
            P(PrimitiveType.Cube, "Cockpit", new Vector3(0f, 7f, 14f), new Vector3(10f, 5f, 8f), paint);
            P(PrimitiveType.Cube, "Pod L", new Vector3(-12f, 5f, -2f), new Vector3(7f, 6f, 20f), paint);
            P(PrimitiveType.Cube, "Pod R", new Vector3(12f, 5f, -2f), new Vector3(7f, 6f, 20f), paint);
            P(PrimitiveType.Cube, "Engine L", new Vector3(-12f, 5f, -13f), new Vector3(5f, 4f, 2f), glowM);
            P(PrimitiveType.Cube, "Engine R", new Vector3(12f, 5f, -13f), new Vector3(5f, 4f, 2f), glowM);
            glow = new GameObject("Glow").AddComponent<Light>(); glow.transform.SetParent(model, false); glow.transform.localPosition = new Vector3(0f, 2f * U, -14f * U);
            glow.type = LightType.Point; glow.color = engineC; glow.range = 12f; glow.intensity = 3f; glow.shadows = LightShadows.None;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            const float T1 = 11f, T2 = 13f;
            Vector3 p;
            if (t < T1)
            {
                float u = t / T1;
                float s = u * u * (3f - 2f * u);
                p = Vector3.Lerp(start, land, s);
                float yk = Mathf.Clamp01(u / 0.91f); yk = yk * yk * (3f - 2f * yk);
                p.y = Mathf.Lerp(start.y, land.y, yk);
                model.localRotation = Quaternion.Euler(0.12f * Mathf.Rad2Deg, 0f, 0f);
            }
            else if (t < T2)
            {
                p = land;
                model.localRotation = Quaternion.identity;
                if (!unloaded)
                {
                    unloaded = true;
                    if (Gunfire.Instance != null) for (int i = 0; i < 12; i++) Gunfire.Instance.Impact(land + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0.2f, UnityEngine.Random.Range(-3f, 3f)), Vector3.up, false);
                    if (cargo != null) cargo(land);
                }
            }
            else
            {
                float u = (t - T2) / 9f;
                p = land + Vector3.up * u * u * 3600f * U;
                if (u >= 1f) { Destroy(gameObject); return; }
            }
            var dir = land - start; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            transform.position = p;
        }
    }
}
