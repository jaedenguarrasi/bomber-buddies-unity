using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The materials the game builds its world from. They are real assets (made once by the editor bootstrap) so the shaders
    /// and their variants are included in every build; the runtime code only ever asks for them by name.
    /// </summary>
    public class MaterialLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string name;
            public Material material;
        }

        public Entry[] entries = new Entry[0];

        public Material Get(string name)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].name == name && entries[i].material != null) return entries[i].material;
            }
            Debug.LogWarning("MaterialLibrary: no material named '" + name + "'");
            return entries.Length > 0 && entries[0] != null ? entries[0].material : null;
        }

        /// <summary>A fresh copy of a library material, safe to recolour or retexture without touching the asset.</summary>
        public Material Instance(string name)
        {
            var m = Get(name);
            return m != null ? new Material(m) : null;
        }
    }
}
