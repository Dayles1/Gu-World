// GU WORLD ATLAS — runtime data for imported objects. Place under Assets/GuAtlas/Runtime/.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuAtlas
{
    [Serializable] public class GaVec { public float x, y, z; }
    [Serializable] public class GaProp { public string key; public string type; public string value; }
    [Serializable] public class GaObject
    {
        public string id, name, type, category, prefab, region, layer, parent, rarity, canonStatus, loreRef;
        public GaVec position; public float rotationY; public GaVec scale;
        public float radius, width, length, height;
        public int danger, rankMin, rankMax;
        public GaVec[] path; public GaProp[] properties; public string[] tags;
    }
    [Serializable] public class GaRelation { public string id, type, from, to, props; }
    [Serializable] public class GaHeightmap { public string file; public int resolution; public float minHeight, maxHeight; }
    [Serializable] public class GaWorldInfo { public string name; public float sizeMeters; public float seaLevel; public GaHeightmap heightmap; }
    [Serializable] public class GaLayer { public string id, name; public float altitudeMeters; }
    [Serializable] public class GaFile
    {
        public string format; public int version;
        public GaWorldInfo world; public GaLayer[] layers;
        public GaObject[] objects; public GaRelation[] relations;
    }

    public class GuAtlasObject : MonoBehaviour
    {
        public string atlasId, atlasType, category, region, layer, loreRef, rarity, canonStatus;
        public int danger, rankMin, rankMax;
        public List<GaProp> properties = new List<GaProp>();
        public string Get(string key) { foreach (var p in properties) if (p.key == key) return p.value; return null; }
    }

    public class GuAtlasRelations : MonoBehaviour
    {
        public List<GaRelation> relations = new List<GaRelation>();
    }
}
