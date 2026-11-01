using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Bodies for the creatures, built from blocks the way the original drew its monsters (meshDino3 / human3 variants):
    /// the goblin (small, big head, big ears, a club), the ape (hunched, long arms, knuckle-walks), the raptor (low body,
    /// tail, long neck, two legs), the stalker (tall, thin, pale, long arms) and the tyrannosaur (huge, tail, tiny arms).
    /// Each animates its legs with speed and topples when it dies.
    /// </summary>
    public class CreatureModel : SoldierModelBase
    {
        public string kind = "gob";
        Transform body, head, tail, legL, legR, armL, armR, club;
        float phase, bobK, deathK; bool dead; Vector3 fallDir; Quaternion baseRot = Quaternion.identity;
        float legSwing = 35f, armSwing = 20f, stride = 2.1f;

        public static CreatureModel Create(Transform parent, MaterialLibrary lib, string kind, int layer)
        {
            var go = new GameObject("Model"); go.transform.SetParent(parent, false); go.layer = layer;
            var m = go.AddComponent<CreatureModel>(); m.kind = kind; m.Build(lib, 1);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return m;
        }

        Material Col(MaterialLibrary lib, Color c) { var m = lib.Instance("skin"); if (m != null && m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); return m; }

        static GameObject Part(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t); go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
        static Transform Pivot(string name, Transform parent, Vector3 pos) { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos; return t; }

        public override void Build(MaterialLibrary lib, int teamId)
        {
            team = teamId;
            switch (kind)
            {
                case "gob":
                    {
                        var g = Col(lib, new Color(0.42f, 0.62f, 0.28f)); var d = Col(lib, new Color(0.28f, 0.22f, 0.16f)); var eye = Col(lib, new Color(1f, 0.85f, 0.2f));
                        body = Pivot("Body", transform, new Vector3(0f, 0.62f, 0f));
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.15f, 0f), new Vector3(0.34f, 0.4f, 0.26f), g);
                        Part(PrimitiveType.Cube, "Loincloth", body, new Vector3(0f, -0.08f, 0f), new Vector3(0.36f, 0.14f, 0.28f), d);
                        head = Pivot("Head", body, new Vector3(0f, 0.42f, 0.04f));
                        Part(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 0.12f, 0f), new Vector3(0.34f, 0.3f, 0.32f), g);
                        Part(PrimitiveType.Cube, "EarL", head, new Vector3(-0.26f, 0.18f, 0f), new Vector3(0.2f, 0.08f, 0.05f), g); Part(PrimitiveType.Cube, "EarR", head, new Vector3(0.26f, 0.18f, 0f), new Vector3(0.2f, 0.08f, 0.05f), g);
                        Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.08f, 0.14f, 0.17f), new Vector3(0.07f, 0.05f, 0.02f), eye); Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.08f, 0.14f, 0.17f), new Vector3(0.07f, 0.05f, 0.02f), eye);
                        Part(PrimitiveType.Cube, "Nose", head, new Vector3(0f, 0.06f, 0.2f), new Vector3(0.06f, 0.1f, 0.1f), g);
                        legL = Pivot("LegL", body, new Vector3(-0.1f, -0.12f, 0f)); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.5f, 0.14f), g);
                        legR = Pivot("LegR", body, new Vector3(0.1f, -0.12f, 0f)); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.5f, 0.14f), g);
                        armL = Pivot("ArmL", body, new Vector3(-0.22f, 0.3f, 0f)); Part(PrimitiveType.Cube, "ForeL", armL, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.42f, 0.1f), g);
                        armR = Pivot("ArmR", body, new Vector3(0.22f, 0.3f, 0f)); Part(PrimitiveType.Cube, "ForeR", armR, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.42f, 0.1f), g);
                        club = Part(PrimitiveType.Cube, "Club", armR, new Vector3(0f, -0.42f, 0.2f), new Vector3(0.1f, 0.1f, 0.55f), d).transform;
                        Part(PrimitiveType.Cube, "ClubHead", armR, new Vector3(0f, -0.42f, 0.45f), new Vector3(0.16f, 0.16f, 0.2f), d);
                        legSwing = 45f; armSwing = 30f; stride = 3.2f;
                    }
                    break;
                case "grey":
                    {
                        var g = Col(lib, new Color(0.62f, 0.64f, 0.66f)); var eye = Col(lib, new Color(0.02f, 0.02f, 0.03f)); var gun = Col(lib, new Color(0.75f, 0.78f, 0.82f));
                        body = Pivot("Body", transform, new Vector3(0f, 0.85f, 0f));
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.1f, 0f), new Vector3(0.26f, 0.5f, 0.2f), g);
                        head = Pivot("Head", body, new Vector3(0f, 0.42f, 0.02f));
                        Part(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0.18f, 0f), new Vector3(0.44f, 0.42f, 0.4f), g);
                        Part(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.1f, 0.14f, 0.16f), new Vector3(0.14f, 0.09f, 0.06f), eye); Part(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.1f, 0.14f, 0.16f), new Vector3(0.14f, 0.09f, 0.06f), eye);
                        legL = Pivot("LegL", body, new Vector3(-0.08f, -0.15f, 0f)); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -0.35f, 0f), new Vector3(0.09f, 0.7f, 0.1f), g);
                        legR = Pivot("LegR", body, new Vector3(0.08f, -0.15f, 0f)); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -0.35f, 0f), new Vector3(0.09f, 0.7f, 0.1f), g);
                        armL = Pivot("ArmL", body, new Vector3(-0.17f, 0.3f, 0f)); Part(PrimitiveType.Cube, "ForeL", armL, new Vector3(0f, -0.25f, 0f), new Vector3(0.07f, 0.5f, 0.07f), g);
                        armR = Pivot("ArmR", body, new Vector3(0.17f, 0.3f, 0f)); Part(PrimitiveType.Cube, "ForeR", armR, new Vector3(0f, -0.25f, 0f), new Vector3(0.07f, 0.5f, 0.07f), g);
                        var rg = Part(PrimitiveType.Cube, "RayGun", armR, new Vector3(0f, -0.5f, 0.2f), new Vector3(0.08f, 0.1f, 0.45f), gun);
                        muzzle = Pivot("Muzzle", rg.transform, new Vector3(0f, 0f, 0.6f));
                        legSwing = 50f; armSwing = 15f; stride = 3f;
                    }
                    break;
                case "stalk":
                    {
                        var p = Col(lib, new Color(0.86f, 0.86f, 0.9f)); var eye = Col(lib, new Color(0.05f, 0.05f, 0.06f));
                        body = Pivot("Body", transform, new Vector3(0f, 1.9f, 0f));
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.9f, 0.22f), p);
                        head = Pivot("Head", body, new Vector3(0f, 0.75f, 0.06f));
                        Part(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 0.16f, 0f), new Vector3(0.22f, 0.34f, 0.26f), p);
                        Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.06f, 0.2f, 0.13f), new Vector3(0.06f, 0.08f, 0.02f), eye); Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.06f, 0.2f, 0.13f), new Vector3(0.06f, 0.08f, 0.02f), eye);
                        legL = Pivot("LegL", body, new Vector3(-0.1f, -0.2f, 0f)); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -0.85f, 0f), new Vector3(0.1f, 1.7f, 0.12f), p);
                        legR = Pivot("LegR", body, new Vector3(0.1f, -0.2f, 0f)); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -0.85f, 0f), new Vector3(0.1f, 1.7f, 0.12f), p);
                        armL = Pivot("ArmL", body, new Vector3(-0.2f, 0.6f, 0f)); Part(PrimitiveType.Cube, "ForeL", armL, new Vector3(0f, -0.7f, 0f), new Vector3(0.08f, 1.4f, 0.08f), p); Part(PrimitiveType.Cube, "ClawL", armL, new Vector3(0f, -1.45f, 0.05f), new Vector3(0.1f, 0.2f, 0.16f), eye);
                        armR = Pivot("ArmR", body, new Vector3(0.2f, 0.6f, 0f)); Part(PrimitiveType.Cube, "ForeR", armR, new Vector3(0f, -0.7f, 0f), new Vector3(0.08f, 1.4f, 0.08f), p); Part(PrimitiveType.Cube, "ClawR", armR, new Vector3(0f, -1.45f, 0.05f), new Vector3(0.1f, 0.2f, 0.16f), eye);
                        legSwing = 40f; armSwing = 45f; stride = 1.4f;
                    }
                    break;
                case "ape":
                    {
                        var f = Col(lib, new Color(0.32f, 0.22f, 0.15f)); var s = Col(lib, new Color(0.45f, 0.35f, 0.3f)); var eye = Col(lib, new Color(0.9f, 0.3f, 0.1f));
                        body = Pivot("Body", transform, new Vector3(0f, 1.3f, 0f)); body.localRotation = Quaternion.Euler(28f, 0f, 0f);
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 1.3f, 0.9f), f);
                        Part(PrimitiveType.Cube, "Chest", body, new Vector3(0f, 0.35f, 0.42f), new Vector3(0.9f, 0.8f, 0.2f), s);
                        head = Pivot("Head", body, new Vector3(0f, 1.0f, 0.3f));
                        Part(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.55f, 0.6f), f);
                        Part(PrimitiveType.Cube, "Face", head, new Vector3(0f, 0.1f, 0.3f), new Vector3(0.45f, 0.4f, 0.1f), s);
                        Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.12f, 0.25f, 0.36f), new Vector3(0.1f, 0.08f, 0.02f), eye); Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.12f, 0.25f, 0.36f), new Vector3(0.1f, 0.08f, 0.02f), eye);
                        legL = Pivot("LegL", body, new Vector3(-0.4f, -0.35f, -0.1f)); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -0.45f, 0f), new Vector3(0.4f, 0.9f, 0.45f), f);
                        legR = Pivot("LegR", body, new Vector3(0.4f, -0.35f, -0.1f)); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -0.45f, 0f), new Vector3(0.4f, 0.9f, 0.45f), f);
                        armL = Pivot("ArmL", body, new Vector3(-0.75f, 0.75f, 0.1f)); Part(PrimitiveType.Cube, "ForeL", armL, new Vector3(0f, -0.9f, 0f), new Vector3(0.36f, 1.8f, 0.4f), f); Part(PrimitiveType.Cube, "FistL", armL, new Vector3(0f, -1.85f, 0.05f), new Vector3(0.42f, 0.3f, 0.45f), s);
                        armR = Pivot("ArmR", body, new Vector3(0.75f, 0.75f, 0.1f)); Part(PrimitiveType.Cube, "ForeR", armR, new Vector3(0f, -0.9f, 0f), new Vector3(0.36f, 1.8f, 0.4f), f); Part(PrimitiveType.Cube, "FistR", armR, new Vector3(0f, -1.85f, 0.05f), new Vector3(0.42f, 0.3f, 0.45f), s);
                        legSwing = 30f; armSwing = 40f; stride = 1.3f;
                    }
                    break;
                case "raptor":
                    {
                        var g = Col(lib, new Color(0.4f, 0.55f, 0.28f)); var b = Col(lib, new Color(0.75f, 0.7f, 0.45f)); var eye = Col(lib, new Color(1f, 0.8f, 0.1f));
                        body = Pivot("Body", transform, new Vector3(0f, 1.0f, 0f));
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.6f, 1.3f), g);
                        Part(PrimitiveType.Cube, "Belly", body, new Vector3(0f, -0.2f, 0.1f), new Vector3(0.36f, 0.3f, 1.0f), b);
                        tail = Pivot("Tail", body, new Vector3(0f, 0.05f, -0.6f)); Part(PrimitiveType.Cube, "Tail1", tail, new Vector3(0f, 0f, -0.6f), new Vector3(0.3f, 0.3f, 1.2f), g); Part(PrimitiveType.Cube, "Tail2", tail, new Vector3(0f, 0.05f, -1.5f), new Vector3(0.15f, 0.15f, 0.8f), g);
                        head = Pivot("Head", body, new Vector3(0f, 0.3f, 0.65f)); head.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                        Part(PrimitiveType.Cube, "Neck", head, new Vector3(0f, 0.2f, 0.15f), new Vector3(0.24f, 0.5f, 0.3f), g);
                        Part(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 0.5f, 0.45f), new Vector3(0.3f, 0.28f, 0.75f), g);
                        Part(PrimitiveType.Cube, "Jaw", head, new Vector3(0f, 0.36f, 0.5f), new Vector3(0.26f, 0.1f, 0.6f), b);
                        Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.16f, 0.58f, 0.35f), new Vector3(0.03f, 0.08f, 0.1f), eye); Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.16f, 0.58f, 0.35f), new Vector3(0.03f, 0.08f, 0.1f), eye);
                        legL = Pivot("LegL", body, new Vector3(-0.3f, -0.15f, -0.1f)); Part(PrimitiveType.Cube, "ThighL", legL, new Vector3(0f, -0.3f, 0.05f), new Vector3(0.2f, 0.6f, 0.4f), g); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -0.75f, -0.05f), new Vector3(0.12f, 0.5f, 0.14f), g); Part(PrimitiveType.Cube, "FootL", legL, new Vector3(0f, -1.0f, 0.1f), new Vector3(0.16f, 0.08f, 0.4f), b);
                        legR = Pivot("LegR", body, new Vector3(0.3f, -0.15f, -0.1f)); Part(PrimitiveType.Cube, "ThighR", legR, new Vector3(0f, -0.3f, 0.05f), new Vector3(0.2f, 0.6f, 0.4f), g); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -0.75f, -0.05f), new Vector3(0.12f, 0.5f, 0.14f), g); Part(PrimitiveType.Cube, "FootR", legR, new Vector3(0f, -1.0f, 0.1f), new Vector3(0.16f, 0.08f, 0.4f), b);
                        armL = Pivot("ArmL", body, new Vector3(-0.22f, 0.0f, 0.5f)); Part(PrimitiveType.Cube, "ClawL", armL, new Vector3(0f, -0.2f, 0.05f), new Vector3(0.08f, 0.4f, 0.1f), g);
                        armR = Pivot("ArmR", body, new Vector3(0.22f, 0.0f, 0.5f)); Part(PrimitiveType.Cube, "ClawR", armR, new Vector3(0f, -0.2f, 0.05f), new Vector3(0.08f, 0.4f, 0.1f), g);
                        legSwing = 50f; armSwing = 10f; stride = 2.6f;
                    }
                    break;
                default: // trex
                    {
                        var g = Col(lib, new Color(0.36f, 0.42f, 0.26f)); var b = Col(lib, new Color(0.7f, 0.66f, 0.45f)); var eye = Col(lib, new Color(1f, 0.6f, 0.1f)); var t = Col(lib, new Color(0.95f, 0.95f, 0.9f));
                        body = Pivot("Body", transform, new Vector3(0f, 4.2f, 0f));
                        Part(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0f, 0f), new Vector3(2.2f, 2.6f, 4.5f), g);
                        Part(PrimitiveType.Cube, "Belly", body, new Vector3(0f, -0.8f, 0.3f), new Vector3(1.6f, 1.4f, 3.6f), b);
                        tail = Pivot("Tail", body, new Vector3(0f, 0.3f, -2.2f)); Part(PrimitiveType.Cube, "Tail1", tail, new Vector3(0f, 0f, -2f), new Vector3(1.4f, 1.4f, 4f), g); Part(PrimitiveType.Cube, "Tail2", tail, new Vector3(0f, 0.1f, -5f), new Vector3(0.7f, 0.7f, 2.6f), g);
                        head = Pivot("Head", body, new Vector3(0f, 1.0f, 2.2f));
                        Part(PrimitiveType.Cube, "Neck", head, new Vector3(0f, 0.4f, 0.4f), new Vector3(1.2f, 1.6f, 1.4f), g);
                        Part(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 1.1f, 1.8f), new Vector3(1.4f, 1.3f, 3.2f), g);
                        Part(PrimitiveType.Cube, "Jaw", head, new Vector3(0f, 0.35f, 1.9f), new Vector3(1.2f, 0.5f, 2.8f), b);
                        for (int i = 0; i < 6; i++) { Part(PrimitiveType.Cube, "Tooth", head, new Vector3(-0.5f + i * 0.2f, 0.62f, 2.6f + (i % 2) * 0.3f), new Vector3(0.1f, 0.3f, 0.1f), t); }
                        Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.72f, 1.4f, 1.2f), new Vector3(0.05f, 0.3f, 0.4f), eye); Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.72f, 1.4f, 1.2f), new Vector3(0.05f, 0.3f, 0.4f), eye);
                        legL = Pivot("LegL", body, new Vector3(-1.1f, -0.8f, -0.3f)); Part(PrimitiveType.Cube, "ThighL", legL, new Vector3(0f, -1.2f, 0.2f), new Vector3(0.9f, 2.4f, 1.6f), g); Part(PrimitiveType.Cube, "ShinL", legL, new Vector3(0f, -2.9f, -0.2f), new Vector3(0.6f, 1.6f, 0.7f), g); Part(PrimitiveType.Cube, "FootL", legL, new Vector3(0f, -3.6f, 0.4f), new Vector3(0.8f, 0.3f, 1.6f), b);
                        legR = Pivot("LegR", body, new Vector3(1.1f, -0.8f, -0.3f)); Part(PrimitiveType.Cube, "ThighR", legR, new Vector3(0f, -1.2f, 0.2f), new Vector3(0.9f, 2.4f, 1.6f), g); Part(PrimitiveType.Cube, "ShinR", legR, new Vector3(0f, -2.9f, -0.2f), new Vector3(0.6f, 1.6f, 0.7f), g); Part(PrimitiveType.Cube, "FootR", legR, new Vector3(0f, -3.6f, 0.4f), new Vector3(0.8f, 0.3f, 1.6f), b);
                        armL = Pivot("ArmL", body, new Vector3(-0.9f, 0.2f, 2.0f)); Part(PrimitiveType.Cube, "ClawL", armL, new Vector3(0f, -0.4f, 0.1f), new Vector3(0.25f, 0.8f, 0.3f), g);
                        armR = Pivot("ArmR", body, new Vector3(0.9f, 0.2f, 2.0f)); Part(PrimitiveType.Cube, "ClawR", armR, new Vector3(0f, -0.4f, 0.1f), new Vector3(0.25f, 0.8f, 0.3f), g);
                        legSwing = 35f; armSwing = 10f; stride = 0.55f;
                    }
                    break;
            }
            if (muzzle == null) muzzle = head;
        }

        public override void SetVisible(bool on) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on; }

        public override void Animate(float speed, float aimPitch, float crouch, bool airborne, float dt)
        {
            if (dead) { AnimateDeath(dt); return; }
            float gait = Mathf.Clamp01(speed / 3f);
            phase += speed * dt * stride;
            bobK = Mathf.Lerp(bobK, gait, Mathf.Min(1f, 8f * dt));
            float s = Mathf.Sin(phase);
            if (legL != null) legL.localRotation = Quaternion.Euler(s * legSwing * bobK, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-s * legSwing * bobK, 0f, 0f);
            if (kind == "ape")
            {
                // knuckle-walking: the arms reach forward and back with the legs, and it lurches
                if (armL != null) armL.localRotation = Quaternion.Euler(-30f - s * armSwing * bobK, 0f, -10f);
                if (armR != null) armR.localRotation = Quaternion.Euler(-30f + s * armSwing * bobK, 0f, 10f);
                if (body != null) body.localRotation = Quaternion.Euler(28f + Mathf.Abs(s) * 6f * bobK, 0f, s * 4f * bobK);
            }
            else
            {
                if (armL != null) armL.localRotation = Quaternion.Euler(-s * armSwing * bobK + (kind == "stalk" ? -40f : 0f), 0f, 0f);
                if (armR != null) armR.localRotation = Quaternion.Euler(s * armSwing * bobK + (kind == "stalk" ? -40f : kind == "grey" ? -80f : 0f), 0f, 0f);
                if (body != null) body.localPosition = new Vector3(0f, body.localPosition.y + 0f, 0f) + Vector3.up * 0f;
            }
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f + 1f) * 12f * (0.3f + bobK), 0f);
            if (head != null && kind != "ape") head.localRotation = Quaternion.Euler((kind == "raptor" ? -25f : 0f) + Mathf.Sin(phase * 0.5f) * 4f * bobK, 0f, 0f);
        }

        public override void Die(Vector3 fromDirection)
        {
            if (dead) return;
            dead = true; deathK = 0f;
            var flat = new Vector3(fromDirection.x, 0f, fromDirection.z);
            fallDir = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        }

        void AnimateDeath(float dt)
        {
            deathK = Mathf.Min(1f, deathK + dt * (kind == "trex" ? 0.7f : 1.6f));
            float k = deathK * deathK * (3f - 2f * deathK);
            var axis = Vector3.Cross(Vector3.up, fallDir);
            transform.rotation = Quaternion.AngleAxis(85f * k, axis) * baseRot;
            if (legL != null) legL.localRotation = Quaternion.Euler(-20f * k, 0f, 10f * k); if (legR != null) legR.localRotation = Quaternion.Euler(25f * k, 0f, -10f * k);
            if (armL != null) armL.localRotation = Quaternion.Euler(-60f * k, 0f, -30f * k); if (armR != null) armR.localRotation = Quaternion.Euler(-40f * k, 0f, 30f * k);
            if (head != null) head.localRotation = Quaternion.Euler(20f * k, 15f * k, 0f);
        }

        void LateUpdate() { if (!dead) baseRot = transform.rotation; }
        public override bool IsDead { get { return dead; } }

        public override void Dismember(Vector3 fromDir, bool headshot, bool blast)
        {
            var g = Gore.Instance; if (g == null) return;
            if (headshot && head != null && kind != "trex") { g.Detach(head, fromDir + Vector3.up, 6f); head = null; }
            if (blast) { if (armL != null) { g.Detach(armL, fromDir, 8f); armL = null; } if (Random.value < 0.5f && legR != null) { g.Detach(legR, fromDir, 7f); legR = null; } g.Splat(transform.position, 1.6f); }
            else g.Splat(transform.position, 0.9f);
        }
    }
}
