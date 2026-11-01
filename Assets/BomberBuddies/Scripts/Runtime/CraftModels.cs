using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The aircraft and vehicles as the original drew them (aircraft3 / model3_0): boxes and tapered boxes in an
    /// entity basis of forward/up/right, team-coloured, flat shaded. Units are the original's; the mesh comes out in metres.</summary>
    public static class CraftModels
    {
        public static readonly Color32 You = MeshBuilder.Rgb(76, 91, 46), Friend = MeshBuilder.Rgb(47, 79, 122), Mate = MeshBuilder.Rgb(70, 118, 128), Foe = MeshBuilder.Rgb(58, 66, 74);
        static readonly Color32 Nose = MeshBuilder.Rgb(226, 179, 60), Glass = MeshBuilder.Rgb(150, 200, 215);
        // enemy ground colours (COL3)
        public static readonly Color32 Body = MeshBuilder.Rgb(74, 85, 96), Dark = MeshBuilder.Rgb(47, 56, 63), Mark = MeshBuilder.Rgb(181, 69, 47), Sand = MeshBuilder.Rgb(185, 164, 119);

        static Material Mat(MaterialLibrary lib)
        {
            var m = lib != null ? lib.Get("lowpoly") : null;
            if (m == null) { var sh = Shader.Find("BomberBuddies/LowPoly"); m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit")); }
            return m;
        }

        /// <summary>The fighter-bomber: 36 long, 42 span. Returns the body; the propeller is a child named "Prop".</summary>
        public static GameObject Plane(Transform parent, MaterialLibrary lib, Color32 c, int layer)
        {
            var mb = new MeshBuilder();
            var wing = MeshBuilder.Mul(c, 0.78f);
            mb.Frustum(-7f, 0.4f, 0f, 17f, 2.4f, 2f, 2.5f, 2.5f, -0.4f, c);      // tail cone
            mb.DBox(7.5f, 0f, 0f, 12f, 6f, 5f, c);                               // fuselage
            mb.Frustum(16.2f, 0f, 0f, 5.4f, 6f, 5f, 0.42f, 0.42f, 0f, Nose);     // nose cone
            mb.DBox(2f, -0.6f, 0f, 9f, 1.2f, 42f, wing);                         // wing
            mb.DBox(-13f, 0.4f, 0f, 5f, 1f, 15f, wing);                          // tailplane
            mb.DBox(-13f, 4.4f, 0f, 6f, 7f, 1f, wing);                           // fin
            mb.Frustum(3.5f, 4f, 0f, 9f, 1.6f, 3.2f, 0.5f, 0.9f, 0.4f, Glass);  // canopy
            var go = mb.Spawn("Body", parent, Mat(lib), false);
            go.layer = layer;
            var pm = new MeshBuilder();
            pm.DBox(0f, 0f, 0f, 0.8f, 11f, 1f, MeshBuilder.Rgb(40, 40, 44));
            var prop = pm.Spawn("Prop", go.transform, Mat(lib), false);
            prop.transform.localPosition = new Vector3(0f, 0f, 19.2f * GalacticMode.U);
            prop.layer = layer;
            return go;
        }

        /// <summary>The attack helicopter: 50-unit rotor. Children "Rotor" and "Tail rotor" spin.</summary>
        public static GameObject Heli(Transform parent, MaterialLibrary lib, Color32 c, int layer)
        {
            var mb = new MeshBuilder();
            var dark = MeshBuilder.Rgb(40, 40, 40);
            mb.Frustum(-2f, 0f, 0f, 16f, 6.6f, 6.6f, 1.36f, 1.3f, 0f, c);        // body
            mb.Frustum(-19.5f, 1.5f, 0f, 20f, 1.5f, 1.5f, 2.1f, 2.1f, 0f, c);    // tail boom
            mb.DBox(-28f, 5f, 0f, 4f, 8f, 1.2f, MeshBuilder.Mul(c, 0.8f));       // fin
            mb.Frustum(9.5f, 0.4f, 0f, 7.5f, 8.6f, 8.4f, 0.5f, 0.55f, -1f, Glass);   // canopy
            mb.DBox(0f, -6.5f, 4f, 16f, 1f, 1f, dark); mb.DBox(0f, -6.5f, -4f, 16f, 1f, 1f, dark);   // skids
            mb.DBox(0f, 6f, 0f, 2f, 3f, 2f, dark);                               // mast
            mb.DBox(9f, -5f, 0f, 9f, 1.5f, 1.5f, dark);                          // chin gun
            var go = mb.Spawn("Body", parent, Mat(lib), false);
            go.layer = layer;
            var rm = new MeshBuilder();
            rm.DBox(0f, 0f, 0f, 50f, 0.6f, 3f, MeshBuilder.Rgb(35, 35, 35));
            rm.DBox(0f, 0f, 0f, 3f, 0.6f, 50f, MeshBuilder.Rgb(35, 35, 35));
            var rotor = rm.Spawn("Rotor", go.transform, Mat(lib), false);
            rotor.transform.localPosition = new Vector3(0f, 7.6f * GalacticMode.U, 0f);
            rotor.layer = layer;
            var tm = new MeshBuilder();
            tm.DBox(0f, 0f, 0f, 0.5f, 8f, 0.6f, MeshBuilder.Rgb(35, 35, 35));
            var tail = tm.Spawn("Tail rotor", go.transform, Mat(lib), false);
            tail.transform.localPosition = new Vector3(0.9f * GalacticMode.U, 5f * GalacticMode.U, -28f * GalacticMode.U);
            tail.layer = layer;
            return go;
        }

        /// <summary>Enemy jeep / tank / AA gun in the original's colours. Returns the body; "Turret" and "Barrel" children aim.</summary>
        public static GameObject Vehicle(Transform parent, MaterialLibrary lib, EnemyVehicle.Kind kind, int team, int layer, out Transform turret, out Transform barrel)
        {
            var B = team == 0 ? You : Body; var D = team == 0 ? MeshBuilder.Mul(You, 0.6f) : Dark;
            var mb = new MeshBuilder();
            GameObject go; turret = null; barrel = null;
            switch (kind)
            {
                case EnemyVehicle.Kind.Jeep:
                    mb.DBox(-3.5f, 5.5f, 0f, 15f, 5f, 11f, B);
                    mb.Frustum(7.5f, 5.2f, 0f, 8f, 4.6f, 11f, 0.62f, 0.86f, -0.6f, B);
                    mb.DBox(4f, 9f, 0f, 1.2f, 3f, 10f, Glass);
                    for (int i = 0; i < 4; i++) mb.DBox(i < 2 ? 7f : -7f, 2.6f, i % 2 == 0 ? 5.5f : -5.5f, 5.2f, 5.2f, 2.6f, D);
                    go = mb.Spawn("Body", parent, Mat(lib), false);
                    {
                        var tm = new MeshBuilder(); tm.DBox(0f, 0f, 0f, 3f, 5f, 3f, D); var t = tm.Spawn("Turret", go.transform, Mat(lib), false); t.transform.localPosition = new Vector3(0f, 10f * GalacticMode.U, -3.5f * GalacticMode.U);
                        var gm = new MeshBuilder(); gm.DBox(5f, 0f, 0f, 10f, 1.2f, 1.2f, MeshBuilder.Rgb(25, 25, 25)); var g = gm.Spawn("Barrel", t.transform, Mat(lib), false); g.transform.localPosition = new Vector3(0f, 2.5f * GalacticMode.U, 0f);
                        turret = t.transform; barrel = g.transform;
                    }
                    break;
                case EnemyVehicle.Kind.Tank:
                    mb.Frustum(0f, 3f, 9f, 31f, 6f, 6f, 0.5f, 1f, 1f, D); mb.Frustum(0f, 3f, -9f, 31f, 6f, 6f, 0.5f, 1f, 1f, D);
                    mb.Frustum(0f, 6.5f, 0f, 29f, 6f, 13f, 0.5f, 0.92f, -1f, B);
                    mb.DBox(-9f, 10f, 4f, 5f, 1.2f, 3f, Mark);
                    go = mb.Spawn("Body", parent, Mat(lib), false);
                    {
                        var tm = new MeshBuilder(); tm.Frustum(-0.5f, 0f, 0f, 14f, 5.6f, 11.5f, 0.66f, 0.7f, -0.4f, B); var t = tm.Spawn("Turret", go.transform, Mat(lib), false); t.transform.localPosition = new Vector3(0f, 12f * GalacticMode.U, 0f);
                        var gm = new MeshBuilder(); gm.DBox(10f, 0f, 0f, 20f, 2.2f, 2.2f, D); var g = gm.Spawn("Barrel", t.transform, Mat(lib), false); g.transform.localPosition = new Vector3(0f, 0.6f * GalacticMode.U, 7f * GalacticMode.U);
                        turret = t.transform; barrel = g.transform;
                    }
                    break;
                default:
                    mb.DBox(0f, 2.5f, 0f, 22f, 5f, 22f, Sand);
                    go = mb.Spawn("Body", parent, Mat(lib), false);
                    {
                        var tm = new MeshBuilder(); tm.DBox(0f, 0f, 0f, 7f, 6f, 7f, B); var t = tm.Spawn("Turret", go.transform, Mat(lib), false); t.transform.localPosition = new Vector3(0f, 8f * GalacticMode.U, 0f);
                        var gm = new MeshBuilder(); gm.DBox(8.5f, 0f, 2f, 17f, 1.5f, 1.5f, D); gm.DBox(8.5f, 0f, -2f, 17f, 1.5f, 1.5f, D); var g = gm.Spawn("Barrel", t.transform, Mat(lib), false); g.transform.localPosition = new Vector3(0f, 1f * GalacticMode.U, 4f * GalacticMode.U); g.transform.localRotation = Quaternion.Euler(-50f, 0f, 0f);
                        turret = t.transform; barrel = g.transform;
                    }
                    break;
            }
            go.layer = layer;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return go;
        }
    }
}
