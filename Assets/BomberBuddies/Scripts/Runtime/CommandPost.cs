using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// A command post on the island: a pole with a flag and a ring of markers, radius 70. Stand on it with no enemy there and it
    /// is yours in 7 s; the enemy takes it back in 9 s. Held posts bring in your carrier's strikes and reinforcements.
    /// </summary>
    public class CommandPost : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public const float Radius = 70f * U;
        public string postName;
        public int own;          // 1 yours, -1 theirs, 0 nobody
        public float prog;       // -1..1
        Transform flag;
        Renderer flagR, poleLight;
        Material flagMat;
        readonly Transform[] markers = new Transform[16];
        Material markerMat;

        public static CommandPost Build(Transform parent, MaterialLibrary lib, string name, Vector3 at, int startOwn)
        {
            var go = new GameObject("Post " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var p = go.AddComponent<CommandPost>();
            p.postName = name; p.own = startOwn; p.prog = startOwn;
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) metal.SetColor("_BaseColor", new Color(0.4f, 0.42f, 0.45f));
            var concrete = lib != null ? lib.Get("concrete") : null;
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; g.transform.SetParent(go.transform, false); g.transform.localPosition = pos; g.transform.localScale = scale;
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            P(PrimitiveType.Cube, "Base", new Vector3(0f, 1.5f * U, 0f), new Vector3(24f * U, 3f * U, 24f * U), concrete);
            P(PrimitiveType.Cylinder, "Pole", new Vector3(0f, 17f * U + 3f * U, 0f), new Vector3(1.2f * U, 17f * U, 1.2f * U), metal);
            p.flagMat = lib != null ? lib.Instance("glow") : null;
            var f = P(PrimitiveType.Cube, "Flag", Vector3.zero, new Vector3(10f * U, 5.5f * U, 0.3f * U), p.flagMat);
            Destroy(f.GetComponent<Collider>());
            p.flag = f.transform; p.flagR = f.GetComponent<Renderer>();
            p.markerMat = lib != null ? lib.Instance("glow") : null;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var m = P(PrimitiveType.Cube, "Marker", new Vector3(Mathf.Cos(a) * 68f * U, 0f, Mathf.Sin(a) * 68f * U), new Vector3(1.6f * U, 6f * U, 1.6f * U), p.markerMat);
                Destroy(m.GetComponent<Collider>());
                m.transform.position = new Vector3(m.transform.position.x, (GameState.GroundHeight != null ? GameState.GroundHeight(m.transform.position.x, m.transform.position.z) : at.y) + 3f * U, m.transform.position.z);
                p.markers[i] = m.transform;
            }
            p.Recolour();
            return p;
        }

        void Recolour()
        {
            var c = own == 1 ? new Color(80f / 255f, 150f / 255f, 240f / 255f) : own == -1 ? new Color(220f / 255f, 70f / 255f, 50f / 255f) : new Color(0.92f, 0.92f, 0.92f);
            if (flagMat != null) { flagMat.SetColor("_BaseColor", c); flagMat.SetColor("_EmissionColor", c * 1.5f); }
            if (markerMat != null) { markerMat.SetColor("_BaseColor", c); markerMat.SetColor("_EmissionColor", c * 2f); }
        }

        /// <summary>One frame of capture logic. Returns a banner when ownership changes.</summary>
        public string Tick(float dt, bool playerHere, int allies)
        {
            int mine = (playerHere ? 1 : 0) + allies;
            int theirs = Health.Count(1, transform.position, Radius, true, true);
            if (mine > 0 && theirs == 0) prog = Mathf.Min(1f, prog + dt / 7f);
            else if (theirs > 0 && mine == 0) prog = Mathf.Max(-1f, prog - dt / 9f);
            int newOwn = prog >= 1f ? 1 : prog <= -1f ? -1 : 0;
            string banner = null;
            if (newOwn != own)
            {
                banner = newOwn == 1 ? "Command post " + postName + " is ours!" : newOwn == -1 ? "The enemy has taken " + postName : postName + " is contested";
                own = newOwn;
                Recolour();
            }
            if (flag != null)
            {
                float h = (3f + 8f + Mathf.Abs(prog) * 22f) * U;
                flag.localPosition = new Vector3(5.5f * U, h, 0f);
                flag.localRotation = Quaternion.Euler(0f, Time.time * 20f, 0f);
                flag.localPosition = flag.localRotation * new Vector3(5.5f * U, 0f, 0f) + Vector3.up * h;
            }
            return banner;
        }
    }
}
