using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>Shared shooting effects: tracers, muzzle flashes, impact sparks and dust. One pool for everyone who fires.</summary>
    public class Gunfire : MonoBehaviour
    {
        public static Gunfire Instance { get; private set; }

        class Tracer { public LineRenderer line; public float t; public float life; }
        readonly List<Tracer> tracers = new List<Tracer>();
        readonly Stack<LineRenderer> pool = new Stack<LineRenderer>();
        class Flash { public Light light; public float t; }
        readonly List<Flash> flashes = new List<Flash>();
        readonly Stack<Light> flashPool = new Stack<Light>();
        ParticleSystem sparks, dust;
        Material tracerMat;

        public static Gunfire Create(Transform parent, MaterialLibrary lib)
        {
            var go = new GameObject("Gunfire");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<Gunfire>();
            g.tracerMat = lib != null ? lib.Get("tracer") : null;
            g.sparks = g.MakeParticles("Sparks", lib != null ? lib.Get("particle") : null, new Color(1f, 0.85f, 0.5f), 0.05f, 0.35f, 7f, 0.6f);
            g.dust = g.MakeParticles("Dust", lib != null ? lib.Get("particle") : null, new Color(0.55f, 0.5f, 0.42f, 0.7f), 0.25f, 0.9f, 2.5f, 0.15f);
            Instance = g;
            return g;
        }

        ParticleSystem MakeParticles(string name, Material mat, Color color, float size, float life, float speed, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.02f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        public void TracerLine(Vector3 from, Vector3 to, Color color, float width = 0.03f, float life = 0.07f)
        {
            LineRenderer lr = pool.Count > 0 ? pool.Pop() : NewLine();
            lr.gameObject.SetActive(true);
            lr.startColor = color; lr.endColor = new Color(color.r, color.g, color.b, 0.15f);
            lr.startWidth = width; lr.endWidth = width * 0.5f;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            tracers.Add(new Tracer { line = lr, t = 0f, life = life });
        }

        LineRenderer NewLine()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (tracerMat != null) lr.sharedMaterial = tracerMat;
            return lr;
        }

        public void MuzzleFlash(Vector3 at, Vector3 forward, float scale = 1f)
        {
            Light l = flashPool.Count > 0 ? flashPool.Pop() : NewFlashLight();
            l.gameObject.SetActive(true);
            l.transform.position = at + forward * 0.15f;
            l.intensity = 25f * scale;
            l.range = 6f * scale;
            flashes.Add(new Flash { light = l, t = 0f });
            sparks.Emit(new ParticleSystem.EmitParams { position = at, velocity = forward * 4f, startSize = 0.09f * scale, startLifetime = 0.05f, startColor = new Color(1f, 0.75f, 0.35f) }, 1);
        }

        Light NewFlashLight()
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.4f);
            l.shadows = LightShadows.None;
            return l;
        }

        public void Impact(Vector3 point, Vector3 normal, bool flesh)
        {
            var dir = normal.sqrMagnitude > 0.01f ? normal : Vector3.up;
            if (flesh)
            {
                dust.Emit(new ParticleSystem.EmitParams { position = point, velocity = dir * 1.5f, startColor = new Color(0.5f, 0.05f, 0.05f, 0.9f), startSize = 0.12f, startLifetime = 0.5f }, 6);
            }
            else
            {
                sparks.transform.position = point;
                sparks.transform.rotation = Quaternion.LookRotation(dir);
                sparks.Emit(5);
                dust.transform.position = point;
                dust.transform.rotation = Quaternion.LookRotation(dir);
                dust.Emit(4);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                var t = tracers[i]; t.t += dt;
                float a = 1f - t.t / t.life;
                if (a <= 0f) { t.line.gameObject.SetActive(false); pool.Push(t.line); tracers.RemoveAt(i); continue; }
                var c = t.line.startColor; t.line.startColor = new Color(c.r, c.g, c.b, a); t.line.endColor = new Color(c.r, c.g, c.b, a * 0.2f);
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i]; f.t += dt;
                if (f.t > 0.06f) { f.light.gameObject.SetActive(false); flashPool.Push(f.light); flashes.RemoveAt(i); continue; }
                f.light.intensity *= 0.6f;
            }
        }
    }
}
