using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// The hero's picture until the models (Assets/GuWorld/Models, client/body/)
    /// are brought over: a plain figure of primitives, 1.8 m tall, facing +Z,
    /// its feet at the parent's origin. Only a picture — no colliders; the
    /// body's physics is the CharacterController.
    /// </summary>
    public static class HeroFigure
    {
        public static Transform Build(Transform parent, Material material)
        {
            var figure = new GameObject("Figure").transform;
            figure.SetParent(parent, false);

            Part(figure, PrimitiveType.Capsule, "Torso", new Vector3(0, 1.2f, 0), new Vector3(0.42f, 0.36f, 0.26f), material);
            Part(figure, PrimitiveType.Sphere, "Head", new Vector3(0, 1.65f, 0), Vector3.one * 0.24f, material);
            // The nose shows which way the hero faces.
            Part(figure, PrimitiveType.Cube, "Nose", new Vector3(0, 1.65f, 0.12f), new Vector3(0.04f, 0.05f, 0.06f), material);
            Part(figure, PrimitiveType.Capsule, "Left leg", new Vector3(-0.11f, 0.45f, 0), new Vector3(0.15f, 0.45f, 0.15f), material);
            Part(figure, PrimitiveType.Capsule, "Right leg", new Vector3(0.11f, 0.45f, 0), new Vector3(0.15f, 0.45f, 0.15f), material);
            Part(figure, PrimitiveType.Capsule, "Left arm", new Vector3(-0.29f, 1.15f, 0), new Vector3(0.1f, 0.33f, 0.1f), material);
            Part(figure, PrimitiveType.Capsule, "Right arm", new Vector3(0.29f, 1.15f, 0), new Vector3(0.1f, 0.33f, 0.1f), material);

            return figure;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            // At once, not at the end of the frame: the body must not bump into its own picture.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
