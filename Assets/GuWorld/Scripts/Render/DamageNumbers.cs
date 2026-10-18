using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// How much a blow did, shown where it landed: a number rising out of the wound and fading — white on
    /// what the hero strikes, gold and larger on a vital part, red on the hero. Always faces the camera and
    /// keeps a readable size however far off it is.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        const float Life = 0.85f;

        TextMesh text;
        MeshRenderer view;
        Color colour;
        float age;
        float size;
        Vector3 drift;

        static Font font;

        public static readonly Color Dealt = new Color(1f, 0.97f, 0.9f);
        public static readonly Color Vital = new Color(1f, 0.78f, 0.3f);
        public static readonly Color Taken = new Color(1f, 0.38f, 0.32f);

        /// <summary>A number at a point: `big` for a vital part or a finishing blow.</summary>
        public static void Show(Vector3 at, float amount, Color colour, bool big = false)
        {
            if (amount < 0.5f)
            {
                return;
            }

            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Damage Number");
            go.transform.position = at + new Vector3(Random.Range(-0.15f, 0.15f), 0.1f, Random.Range(-0.15f, 0.15f));
            var number = go.AddComponent<DamageNumbers>();
            number.text = go.AddComponent<TextMesh>();
            number.text.font = font;
            number.text.text = Mathf.RoundToInt(amount).ToString();
            number.text.fontSize = 64;
            number.text.characterSize = 0.05f;
            number.text.anchor = TextAnchor.MiddleCenter;
            number.text.alignment = TextAlignment.Center;
            number.text.fontStyle = big ? FontStyle.Bold : FontStyle.Normal;
            number.view = go.GetComponent<MeshRenderer>();
            number.view.sharedMaterial = font.material;
            number.view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            number.colour = colour;
            number.size = big ? 1.5f : 1;
            number.drift = new Vector3(Random.Range(-0.3f, 0.3f), 1.3f, 0);
            number.Step(0);
        }

        void LateUpdate() => Step(Time.unscaledDeltaTime);

        void Step(float dt)
        {
            age += dt;
            var t = age / Life;

            if (t >= 1)
            {
                Destroy(gameObject);

                return;
            }

            var camera = Camera.main;
            transform.position += drift * dt * (1 - t);

            if (camera != null)
            {
                transform.rotation = camera.transform.rotation;
                // The same size on the screen near or far (a little larger as it pops out).
                var distance = Vector3.Distance(camera.transform.position, transform.position);
                var pop = 1 + 0.5f * Mathf.Clamp01(1 - age / 0.12f);
                transform.localScale = Vector3.one * Mathf.Clamp(distance * 0.09f, 0.18f, 3f) * size * pop;
            }

            text.color = new Color(colour.r, colour.g, colour.b, 1 - Mathf.Clamp01((t - 0.55f) / 0.45f));
        }
    }
}
