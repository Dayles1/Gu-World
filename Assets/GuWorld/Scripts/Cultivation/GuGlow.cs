using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// A glowing sphere that a Gu leaves for a moment (a blast, a heal, a ward): it swells
    /// to its size and fades; one that follows something (a ward round the hero, a skin's
    /// glow) stays on it until it is let go.
    /// </summary>
    public class GuGlow : MonoBehaviour
    {
        Material material;
        Color colour;
        float size;
        float time;
        float age;
        Transform follow;
        bool held;

        static Mesh sphere;

        /// <summary>A flash: swelling to `radius` and gone in `time` s.</summary>
        public static GuGlow Flash(Vector3 at, float radius, Color colour, float time = 0.45f) => Make(at, radius, colour, time, null, false);

        /// <summary>A glow round something, held until Release.</summary>
        public static GuGlow Hold(Transform on, float radius, Color colour) => Make(on.position + Vector3.up, radius, colour, 0.25f, on, true);

        static GuGlow Make(Vector3 at, float radius, Color colour, float time, Transform follow, bool held)
        {
            var go = new GameObject("Gu Glow");
            go.transform.position = at;
            var glow = go.AddComponent<GuGlow>();
            glow.colour = colour;
            glow.size = Mathf.Max(0.1f, radius) * 2;
            glow.time = Mathf.Max(0.05f, time);
            glow.follow = follow;
            glow.held = held;
            glow.material = GuSkills.Glow(colour);
            go.AddComponent<MeshFilter>().sharedMesh = Sphere();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glow.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            glow.Step(0);

            return glow;
        }

        /// <summary>Lets a held glow fade.</summary>
        public void Release()
        {
            held = false;
            age = 0;
        }

        void Update() => Step(Time.deltaTime);

        void Step(float dt)
        {
            age += dt;

            if (follow != null)
            {
                transform.position = follow.position + Vector3.up * 0.95f;
            }

            if (held)
            {
                // Breathing gently round its owner.
                transform.localScale = Vector3.one * size * (1 + 0.03f * Mathf.Sin(age * 3));
                material.color = colour * (0.22f + 0.05f * Mathf.Sin(age * 2));

                return;
            }

            var t = Mathf.Clamp01(age / time);
            transform.localScale = Vector3.one * size * (follow != null ? 1 : Mathf.Lerp(0.35f, 1, Mathf.Sqrt(t)));
            material.color = colour * (follow != null ? 0.25f : 0.9f) * (1 - t);

            if (t >= 1)
            {
                Destroy(material);
                Destroy(gameObject);
            }
        }

        static Mesh Sphere()
        {
            if (sphere != null)
            {
                return sphere;
            }

            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);

            return sphere;
        }
    }
}
