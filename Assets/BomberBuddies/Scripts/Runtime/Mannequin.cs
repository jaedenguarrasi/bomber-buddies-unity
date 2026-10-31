using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The stand-in soldier: a jointed figure built from primitives, animated in code (walk, run, crouch, aim, jump, death).
    /// Everything that will later come from a rigged model is behind the same handful of calls, so the switch to Mixamo
    /// bodies is a swap of this class, not of the game around it.
    /// </summary>
    public class Mannequin : SoldierModelBase
    {
        public Transform pelvis, upperBody, head, rifle;
        public Transform thighL, thighR, shinL, shinR, armL, armR, foreL, foreR;
        public Renderer[] renderers = new Renderer[0];

        float phase, bobK, deathK;
        bool dead;
        static readonly Dictionary<int, Material> uniformByTeam = new Dictionary<int, Material>();

        static Material Uniform(MaterialLibrary lib, int team)
        {
            Material m;
            if (uniformByTeam.TryGetValue(team, out m) && m != null) return m;
            m = lib.Instance("uniform");
            if (m != null)
            {
                var camo = team == 0
                    ? ProceduralTextures.Camo(31, new Color(0.17f, 0.2f, 0.12f), new Color(0.3f, 0.35f, 0.2f), new Color(0.45f, 0.42f, 0.27f), new Color(0.36f, 0.3f, 0.2f))
                    : ProceduralTextures.Camo(47, new Color(0.18f, 0.18f, 0.2f), new Color(0.3f, 0.3f, 0.33f), new Color(0.44f, 0.44f, 0.47f), new Color(0.26f, 0.24f, 0.3f));
                m.mainTexture = camo;
                m.mainTextureScale = new Vector2(2.5f, 2.5f);
            }
            uniformByTeam[team] = m;
            return m;
        }

        static GameObject Part(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A limb hanging from a joint: the pivot sits at the joint, the capsule hangs below it.</summary>
        static Transform Limb(string name, Transform parent, Vector3 joint, float length, float width, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = joint;
            Part(PrimitiveType.Capsule, name + " Mesh", pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length * 0.5f, width), mat);
            return pivot;
        }

        public override void Build(MaterialLibrary lib, int teamId)
        {
            team = teamId;
            var uniform = Uniform(lib, team);
            var skin = lib.Get("skin");
            var dark = lib.Get("dark");
            var helmetMat = lib.Get(team == 0 ? "helmet" : "helmet2");
            var metal = lib.Get("gunmetal");
            var wood = lib.Get("wood");

            pelvis = new GameObject("Pelvis").transform; pelvis.SetParent(transform, false); pelvis.localPosition = new Vector3(0f, 0.95f, 0f);
            Part(PrimitiveType.Cube, "Hips", pelvis, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.22f, 0.24f), uniform);
            upperBody = new GameObject("UpperBody").transform; upperBody.SetParent(pelvis, false); upperBody.localPosition = new Vector3(0f, 0.12f, 0f);
            Part(PrimitiveType.Cube, "Torso", upperBody, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.56f, 0.27f), uniform);
            Part(PrimitiveType.Cube, "Vest", upperBody, new Vector3(0f, 0.3f, 0.02f), new Vector3(0.38f, 0.42f, 0.33f), dark);
            Part(PrimitiveType.Cube, "Pouch", upperBody, new Vector3(-0.1f, 0.18f, 0.18f), new Vector3(0.1f, 0.12f, 0.07f), dark);
            Part(PrimitiveType.Cube, "Pouch", upperBody, new Vector3(0.1f, 0.18f, 0.18f), new Vector3(0.1f, 0.12f, 0.07f), dark);
            Part(PrimitiveType.Cube, "Pack", upperBody, new Vector3(0f, 0.3f, -0.2f), new Vector3(0.3f, 0.34f, 0.14f), uniform);

            head = new GameObject("Head").transform; head.SetParent(upperBody, false); head.localPosition = new Vector3(0f, 0.6f, 0f);
            Part(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.12f, 0.01f), new Vector3(0.22f, 0.25f, 0.24f), skin);
            Part(PrimitiveType.Sphere, "Helmet", head, new Vector3(0f, 0.17f, -0.01f), new Vector3(0.29f, 0.24f, 0.3f), helmetMat);
            Part(PrimitiveType.Cube, "Goggles", head, new Vector3(0f, 0.13f, 0.12f), new Vector3(0.17f, 0.05f, 0.03f), dark);
            Part(PrimitiveType.Cube, "Neck", head, new Vector3(0f, 0.0f, 0f), new Vector3(0.1f, 0.08f, 0.1f), skin);

            armL = Limb("ArmL", upperBody, new Vector3(-0.26f, 0.5f, 0f), 0.3f, 0.11f, uniform);
            armR = Limb("ArmR", upperBody, new Vector3(0.26f, 0.5f, 0f), 0.3f, 0.11f, uniform);
            foreL = Limb("ForeL", armL, new Vector3(0f, -0.3f, 0f), 0.3f, 0.1f, uniform);
            foreR = Limb("ForeR", armR, new Vector3(0f, -0.3f, 0f), 0.3f, 0.1f, uniform);
            Part(PrimitiveType.Sphere, "HandL", foreL, new Vector3(0f, -0.33f, 0f), new Vector3(0.1f, 0.1f, 0.1f), dark);
            Part(PrimitiveType.Sphere, "HandR", foreR, new Vector3(0f, -0.33f, 0f), new Vector3(0.1f, 0.1f, 0.1f), dark);

            thighL = Limb("ThighL", pelvis, new Vector3(-0.11f, -0.05f, 0f), 0.46f, 0.15f, uniform);
            thighR = Limb("ThighR", pelvis, new Vector3(0.11f, -0.05f, 0f), 0.46f, 0.15f, uniform);
            shinL = Limb("ShinL", thighL, new Vector3(0f, -0.46f, 0f), 0.44f, 0.12f, uniform);
            shinR = Limb("ShinR", thighR, new Vector3(0f, -0.46f, 0f), 0.44f, 0.12f, uniform);
            Part(PrimitiveType.Cube, "BootL", shinL, new Vector3(0f, -0.46f, 0.04f), new Vector3(0.13f, 0.1f, 0.26f), dark);
            Part(PrimitiveType.Cube, "BootR", shinR, new Vector3(0f, -0.46f, 0.04f), new Vector3(0.13f, 0.1f, 0.26f), dark);

            // the rifle, carried at the ready: stock at the right shoulder, barrel forward
            rifle = new GameObject("Rifle").transform; rifle.SetParent(upperBody, false);
            rifle.localPosition = new Vector3(0.12f, 0.42f, 0.22f); rifle.localRotation = Quaternion.identity;
            Part(PrimitiveType.Cube, "Receiver", rifle, new Vector3(0f, 0f, 0.1f), new Vector3(0.05f, 0.09f, 0.5f), metal);
            Part(PrimitiveType.Cube, "Stock", rifle, new Vector3(0f, -0.02f, -0.25f), new Vector3(0.05f, 0.1f, 0.22f), wood);
            Part(PrimitiveType.Cube, "Handguard", rifle, new Vector3(0f, -0.005f, 0.42f), new Vector3(0.055f, 0.07f, 0.2f), wood);
            Part(PrimitiveType.Cube, "Magazine", rifle, new Vector3(0f, -0.1f, 0.12f), new Vector3(0.04f, 0.14f, 0.07f), metal);
            var barrel = Part(PrimitiveType.Cylinder, "Barrel", rifle, new Vector3(0f, 0.02f, 0.68f), new Vector3(0.025f, 0.17f, 0.025f), metal);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, "Sight", rifle, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.03f, 0.04f, 0.1f), metal);
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(rifle, false); muzzle.localPosition = new Vector3(0f, 0.02f, 0.86f);

            renderers = GetComponentsInChildren<Renderer>(true);
            Animate(0f, 0f, 0f, false, 0f);
        }

        public override void SetVisible(bool on)
        {
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = on;
        }

        /// <summary>Poses the figure: speed in m/s along the ground, aim pitch in degrees (up positive), crouch 0..1, in the air or not.</summary>
        public override void Animate(float speed, float aimPitch, float crouch, bool airborne, float dt)
        {
            if (dead) { AnimateDeath(dt); return; }
            float gait = Mathf.Clamp01(speed / 3.5f);
            float stride = speed > 5.5f ? 1.25f : 1f;
            phase += speed * dt * 2.1f;
            bobK = Mathf.Lerp(bobK, gait, Mathf.Min(1f, 10f * dt));
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float swing = 38f * gait * stride;

            if (airborne)
            {
                thighL.localRotation = Quaternion.Euler(-25f, 0f, 0f); shinL.localRotation = Quaternion.Euler(35f, 0f, 0f);
                thighR.localRotation = Quaternion.Euler(12f, 0f, 0f); shinR.localRotation = Quaternion.Euler(55f, 0f, 0f);
            }
            else if (crouch > 0.5f)
            {
                thighL.localRotation = Quaternion.Euler(-75f + s * 12f * gait, 0f, 6f); shinL.localRotation = Quaternion.Euler(95f, 0f, 0f);
                thighR.localRotation = Quaternion.Euler(-75f - s * 12f * gait, 0f, -6f); shinR.localRotation = Quaternion.Euler(95f, 0f, 0f);
            }
            else
            {
                thighL.localRotation = Quaternion.Euler(s * swing, 0f, 3f);
                thighR.localRotation = Quaternion.Euler(-s * swing, 0f, -3f);
                shinL.localRotation = Quaternion.Euler(Mathf.Max(0f, -c) * 55f * gait + 4f, 0f, 0f);
                shinR.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * 55f * gait + 4f, 0f, 0f);
            }

            float pelvisY = 0.95f - 0.42f * crouch - (airborne ? 0.05f : 0f) + Mathf.Abs(s) * 0.035f * bobK;
            pelvis.localPosition = new Vector3(0f, pelvisY, 0f);
            pelvis.localRotation = Quaternion.Euler(0f, s * 4f * bobK, 0f);

            float lean = 6f * gait + 18f * crouch;
            float pitch = Mathf.Clamp(-aimPitch, -45f, 50f) * 0.75f;
            upperBody.localRotation = Quaternion.Euler(lean + pitch, -s * 3f * bobK, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Clamp(-aimPitch, -40f, 40f) * 0.25f - lean * 0.5f, 0f, 0f);

            // arms hold the rifle: the right hand on the grip, the left out on the handguard
            armR.localRotation = Quaternion.Euler(-68f + s * 2f * bobK, -18f, 10f);
            foreR.localRotation = Quaternion.Euler(-60f, 10f, 0f);
            armL.localRotation = Quaternion.Euler(-70f - s * 2f * bobK, 40f, -14f);
            foreL.localRotation = Quaternion.Euler(-45f, -40f, 0f);
        }

        public override void Die(Vector3 fromDirection)
        {
            if (dead) return;
            dead = true;
            deathK = 0f;
            var flat = new Vector3(fromDirection.x, 0f, fromDirection.z);
            fallDir = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        }

        Vector3 fallDir;

        void AnimateDeath(float dt)
        {
            deathK = Mathf.Min(1f, deathK + dt * 1.6f);
            float k = deathK * deathK * (3f - 2f * deathK);
            // the body topples the way the shot pushed it, knees folding, and comes to rest flat
            var axis = Vector3.Cross(Vector3.up, fallDir);
            transform.rotation = Quaternion.AngleAxis(88f * k, axis) * baseRot;
            pelvis.localPosition = new Vector3(0f, 0.95f - 0.2f * k, 0f);
            thighL.localRotation = Quaternion.Euler(-30f * k, 0f, 8f); thighR.localRotation = Quaternion.Euler(20f * k, 0f, -8f);
            shinL.localRotation = Quaternion.Euler(40f * k, 0f, 0f); shinR.localRotation = Quaternion.Euler(15f * k, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-70f + 110f * k, 40f - 60f * k, -14f); armR.localRotation = Quaternion.Euler(-68f + 30f * k, -18f, 10f + 50f * k);
            upperBody.localRotation = Quaternion.Euler(10f * k, 15f * k, 0f);
        }

        Quaternion baseRot = Quaternion.identity;
        void LateUpdate() { if (!dead) baseRot = transform.rotation; }
        public override bool IsDead { get { return dead; } }
    }
}
