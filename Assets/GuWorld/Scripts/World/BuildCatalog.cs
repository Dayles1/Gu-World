using System;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// What the builder (WorldBuilder, F9) can put in the world: the artist's models, each a ready prefab (its near
    /// and far shapes, solid by its full shape, trees climbable), by section, with its footprint. Made by the editor
    /// (BuildCatalogMaker) from Models/Village and Models/Trees before every build; lives in Resources.
    /// </summary>
    public class BuildCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            /// <summary>The model's name (the saved worlds keep it).</summary>
            public string Id;
            public string Label;
            public string Section;
            public GameObject Prefab;
            /// <summary>Its footprint across (X) and deep (Z), its height, and the footprint's middle from its origin, m.</summary>
            public Vector2 Size;
            public float Height;
            public Vector2 Middle;
            /// <summary>Drawn between two points: a stair stretched to fit (`Stretch`), or laid again and again along the way (`Repeat`).</summary>
            public bool Stretch;
            public bool Repeat;
        }

        public Entry[] Entries = new Entry[0];

        static BuildCatalog loaded;

        public static BuildCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<BuildCatalog>("BuildCatalog");

        public Entry Find(string id) => Array.Find(Entries, e => e.Id == id);
    }
}
