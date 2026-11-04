using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The hangar's turntable (showModel / renderShowroom): the class or the weapon being looked at, built from the
    /// game's own model builders on its own layer, turning slowly, drawn into a texture the hangar shows. Drag to turn it round.</summary>
    public class Showroom : MonoBehaviour
    {
        const int Layer = 11;
        Camera cam; RenderTexture rt; Transform model, root; float t, rot, pitch; bool drag; Vector2 last; float dist = 12f, ty = 1f; string shown;
        public RawImage target;

        public static Showroom Create(Transform parent, RawImage target)
        {
            var go = new GameObject("Showroom"); go.transform.SetParent(parent, false); go.transform.position = new Vector3(0f, -900f, 0f);
            var s = go.AddComponent<Showroom>(); s.target = target;
            s.root = new GameObject("Turntable").transform; s.root.SetParent(go.transform, false);
            var cg = new GameObject("Showroom camera"); cg.transform.SetParent(go.transform, false);
            s.cam = cg.AddComponent<Camera>(); s.cam.clearFlags = CameraClearFlags.SolidColor; s.cam.backgroundColor = new Color(0.075f, 0.09f, 0.105f); s.cam.cullingMask = 1 << Layer; s.cam.fieldOfView = 41f; s.cam.nearClipPlane = 0.1f; s.cam.farClipPlane = 300f; s.cam.depth = -5f;
            s.rt = new RenderTexture(1024, 384, 16); s.cam.targetTexture = s.rt; if (target != null) target.texture = s.rt;
            var lg = new GameObject("Showroom light"); lg.transform.SetParent(go.transform, false); var l = lg.AddComponent<Light>(); l.type = LightType.Directional; l.cullingMask = 1 << Layer; l.intensity = 1.1f; l.color = new Color(1f, 0.97f, 0.92f); lg.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return s;
        }

        static void SetLayer(Transform tr) { tr.gameObject.layer = Layer; foreach (Transform c in tr) SetLayer(c); }

        /// <summary>Show a class by its id, or a gun by "gun:" + id.</summary>
        public void Show(string id)
        {
            if (id == shown && model != null) return;
            shown = id; if (model != null) Destroy(model.gameObject);
            model = new GameObject("Model").transform; model.SetParent(root, false);
            var lib = GameState.Library; float U = GalacticMode.U;
            dist = 12f; ty = 1f;
            if (id.StartsWith("gun:")) { BuildGun(id.Substring(4), lib); dist = 2.6f; ty = 0f; }
            else BuildCraftModel(model, id, lib, Layer, out dist, out ty);
            SetLayer(model);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }


        /// <summary>The model for a craft or class by its id, as the showroom draws it (also used for buddies' puppets).</summary>
        public static void BuildCraftModel(Transform model, string id, MaterialLibrary lib, int layer) { float a, b; BuildCraftModel(model, id, lib, layer, out a, out b); }
        public static void BuildCraftModel(Transform model, string id, MaterialLibrary lib, int Layer, out float dist, out float ty)
        {
            dist = 12f; ty = 1f;

                var d = Progress.Def(id);
                if (d == null) { }
                else if (!d.ground)
                {
                    if (id == "dragon") { Transform a, b, c, e; var m = Dragon.BuildDragonModel(model, lib, Layer, out a, out b, out c, out e); dist = 18f; ty = 2f; }
                    else if (id == "ufo") { CraftModels.Saucer(model, lib, Layer); dist = 9f; ty = 0.5f; }
                    else if (id == "heli" || id == "heli2" || id == "transport") { CraftModels.Heli(model, lib, CraftModels.You, Layer); if (id == "transport") model.localScale = new Vector3(1.5f, 1.5f, 1.8f); if (id == "heli2") model.localScale = Vector3.one * 1.2f; dist = 10f; }
                    else { CraftModels.Plane(model, lib, CraftModels.You, Layer); if (id == "b52") { model.localScale = new Vector3(2.6f, 2.2f, 2.6f); dist = 22f; ty = 1.5f; } if (id == "bomber2") model.localScale = Vector3.one * 1.3f; if (id == "ac130") { model.localScale = Vector3.one * 2.6f; dist = 22f; } if (id == "drone") { model.localScale = Vector3.one * 0.3f; dist = 4f; ty = 0.3f; } }
                }
                else if (d.foot)
                {
                    var kit = Progress.KitOf(id);
                    if (kit != null && kit.body != null) { var cm = CreatureModel.Create(model, lib, kit.body, Layer); float k = kit.height / 1.8f; if (kit.body == "trex") { dist = 14f; ty = 3f; } else if (k > 1.3f) { dist = 7f; ty = 1.6f; } else { dist = 4.5f; ty = 0.9f; } }
                    else { SoldierModelBase.Create(model, lib, 0, Layer); dist = 4.2f; ty = 0.9f; }
                }
                else
                {
                    Transform tt, bb;
                    var kind = id == "tank" || id == "arty" ? EnemyVehicle.Kind.Tank : EnemyVehicle.Kind.Jeep;
                    var body = CraftModels.Vehicle(model, lib, kind, 0, Layer, out tt, out bb);
                    if (id == "bike") body.transform.localScale = new Vector3(0.45f, 0.7f, 0.75f);
                    if (id == "clowncar") body.transform.localScale = new Vector3(0.7f, 0.8f, 0.6f);
                    if (id == "trike" || id == "trexMount" || id == "trikeMount" || id == "raptorMount") { GameObject.Destroy(body); CreatureModel.Create(model, lib, id == "raptorMount" ? "raptor" : "trex", Layer); dist = id == "raptorMount" ? 5f : 12f; ty = 2f; }
                    else dist = 8f;
                }
                    }

        void BuildGun(string gun, MaterialLibrary lib)
        {
            // a plain gun shape in the showroom's colours: receiver, barrel, stock; a blade or a rock where that is what it is
            var g = Progress.GunDef(gun); var mat = lib != null ? lib.Get("lowpoly") : null;
            var mb = new MeshBuilder { scale = 1f };
            if (g.melee && gun == "blade") { mb.DBox(0f, 0f, 0.3f, 0.05f, 0.9f, 0.1f, MeshBuilder.Emissive(MeshBuilder.Rgb(120, 220, 255))); mb.DBox(0f, 0f, -0.3f, 0.08f, 0.25f, 0.08f, MeshBuilder.Rgb(40, 40, 50)); }
            else if (gun == "club") { mb.DBox(0f, 0f, 0f, 0.1f, 0.1f, 0.9f, MeshBuilder.Rgb(200, 190, 170)); mb.DBox(0f, 0f, 0.45f, 0.22f, 0.22f, 0.3f, MeshBuilder.Rgb(210, 200, 180)); }
            else if (gun == "rock") { mb.DBox(0f, 0f, 0f, 0.35f, 0.3f, 0.4f, MeshBuilder.Rgb(120, 115, 105)); }
            else if (gun == "spear") { mb.DBox(0f, 0f, 0f, 0.05f, 0.05f, 1.6f, MeshBuilder.Rgb(150, 110, 70)); mb.DBox(0f, 0f, 0.85f, 0.08f, 0.12f, 0.3f, MeshBuilder.Rgb(200, 200, 205)); }
            else if (gun == "bite") { mb.DBox(0f, 0f, 0f, 0.5f, 0.2f, 0.5f, MeshBuilder.Rgb(230, 220, 190)); for (int i = 0; i < 5; i++) mb.DBox(-0.2f + i * 0.1f, -0.15f, 0.2f, 0.04f, 0.14f, 0.04f, MeshBuilder.Rgb(240, 235, 225)); }
            else if (gun == "breath") { mb.DBox(0f, 0f, 0f, 0.6f, 0.25f, 0.9f, MeshBuilder.Rgb(96, 40, 36)); mb.DBox(0f, -0.05f, 0.7f, 0.3f, 0.15f, 0.5f, MeshBuilder.Emissive(MeshBuilder.Rgb(255, 150, 40))); }
            else
            {
                var metal = MeshBuilder.Rgb(40, 40, 46); var wood = MeshBuilder.Rgb(70, 50, 36); bool big = !g.foot;
                float L = big ? 1.6f : g.id == "sniper" || g.id == "laser" ? 1.3f : g.id == "duals" ? 0.5f : g.id == "sawn" ? 0.7f : 1f;
                mb.DBox(0f, 0f, 0f, 0.09f, 0.14f, L * 0.5f, metal); mb.DBox(0f, 0.02f, L * 0.55f, 0.04f, 0.04f, L * 0.5f, metal);
                if (!big && g.id != "duals") mb.DBox(0f, -0.03f, -L * 0.38f, 0.07f, 0.13f, L * 0.3f, wood);
                if (g.id != "duals" && g.id != "sawn") mb.DBox(0f, -0.14f, 0.05f, 0.05f, 0.18f, 0.08f, metal);
                if (g.scope || g.id == "sniper") mb.DBox(0f, 0.11f, 0.1f, 0.05f, 0.05f, 0.3f, metal);
                if (g.pellets > 1) mb.DBox(0f, 0.02f, L * 0.55f, 0.06f, 0.06f, L * 0.45f, metal);
                if (big) mb.DBox(0f, -0.1f, -0.2f, 0.16f, 0.1f, 0.5f, metal);
            }
            mb.Spawn("Gun", model, mat, false).layer = Layer;
        }

        public void OnDrag(Vector2 delta) { drag = true; rot += delta.x * 0.012f; pitch = Mathf.Clamp(pitch - delta.y * 0.012f, -1.75f, 1.25f); }
        public void OnRelease() { drag = false; }

        void Update()
        {
            if (model == null) return;
            float dt = Time.unscaledDeltaTime; if (!drag) t += dt;
            model.localRotation = Quaternion.Euler(0f, t * 0.7f * Mathf.Rad2Deg, 0f);
            float cy = -2.2f - rot, op = 0.31f + pitch;
            var centre = root.position + Vector3.up * ty;
            var eye = centre + new Vector3(Mathf.Cos(cy) * Mathf.Cos(op), Mathf.Sin(op), Mathf.Sin(cy) * Mathf.Cos(op)) * dist;
            cam.transform.position = eye; cam.transform.LookAt(centre);
        }

        void OnDestroy() { if (rt != null) rt.Release(); }
    }

    /// <summary>Drag on the hangar's preview turns the model (the original's pointer handlers on #shopPrev).</summary>
    public class ShowroomDrag : MonoBehaviour, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerDownHandler
    {
        public Showroom room;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { }
        public void OnDrag(UnityEngine.EventSystems.PointerEventData e) { if (room != null) room.OnDrag(e.delta); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { if (room != null) room.OnRelease(); }
    }
}
