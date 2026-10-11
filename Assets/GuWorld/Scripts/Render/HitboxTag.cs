using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>Marks a thing's colliders with what it is (the builders put it on houses, props, trees).</summary>
    public class HitboxTag : MonoBehaviour
    {
        public HitboxKind Kind;
    }
}
