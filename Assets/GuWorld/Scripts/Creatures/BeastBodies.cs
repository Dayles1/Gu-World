using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// The bodies of the beasts that are not wolves, built of simple shapes like the wolf's: a wild boar — a stocky barrel
    /// on short legs, a bristled ridge, a snout and tusks (a boar king's gold); a black bear — big and round, thick-legged,
    /// with round ears; a jade eye stone monkey — hunched on long arms, a pale face, eyes of glowing jade, a tail curled up;
    /// a dark venomous snake — a banded body of segments that sways as it slithers, its head raised. Their legs are the
    /// wolf's rig (a hip and a knee each), so they trot as it does; the snake's segments sway instead.
    /// </summary>
    public partial class Beast
    {
        /// <summary>The snake's body, head first: each segment hangs from the one before it.</summary>
        Transform[] segments;

        static Material ivory;
        static Material stoneGrey;

        void BuildBody(Material fur, Material furDark, Material furLight)
        {
            ivory ??= Paint("Tusk", new Color(0.92f, 0.88f, 0.78f));

            switch (Kind.Shape)
            {
                case BeastShape.Boar:
                    BuildBoar(fur, furDark, furLight);
                    break;
                case BeastShape.Bear:
                    BuildBear(fur, furDark, furLight);
                    break;
                case BeastShape.Monkey:
                    BuildMonkey(fur, furDark, furLight);
                    break;
                case BeastShape.Snake:
                    BuildSnake(fur, furDark, furLight);
                    break;
            }
        }

        /// <summary>A leg: its hip (or shoulder) joint at a point, the upper part, the knee joint, the lower part and the paw (or hoof).</summary>
        void Leg(int i, Vector3 at, float upper, float lower, float thick, Material skin, Material paw)
        {
            hips[i] = new GameObject("Hip").transform;
            hips[i].SetParent(figure, false);
            hips[i].localPosition = at;
            Part(PrimitiveType.Capsule, hips[i], new Vector3(0, -upper / 2, 0), Vector3.zero, new Vector3(thick, upper / 2, thick), skin);
            knees[i] = new GameObject("Knee").transform;
            knees[i].SetParent(hips[i], false);
            knees[i].localPosition = new Vector3(0, -upper, 0);
            Part(PrimitiveType.Capsule, knees[i], new Vector3(0, -lower / 2, 0), Vector3.zero, new Vector3(thick * 0.8f, lower / 2, thick * 0.8f), skin);
            Part(PrimitiveType.Sphere, knees[i], new Vector3(0, -lower, 0.02f), Vector3.zero, new Vector3(thick * 0.95f, thick * 0.45f, thick * 1.15f), paw);
        }

        void Eyes(Transform on, float apart, float up, float forward, float size)
        {
            foreach (var side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Sphere, on, new Vector3(side * apart, up, forward), Vector3.zero, Vector3.one * size, eyeGlow);
            }
        }

        void BuildBoar(Material fur, Material furDark, Material furLight)
        {
            // A stocky barrel, high shoulders, a bristled ridge down the back.
            Part(PrimitiveType.Capsule, figure, new Vector3(0, 0.55f, -0.05f), new Vector3(90, 0, 0), new Vector3(0.5f, 0.5f, 0.48f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.6f, 0.26f), Vector3.zero, new Vector3(0.54f, 0.58f, 0.56f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.82f, 0.1f), Vector3.zero, new Vector3(0.14f, 0.16f, 0.86f), furDark);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.42f, 0.05f), Vector3.zero, new Vector3(0.38f, 0.22f, 0.7f), furLight);

            // The head low before it: a long skull, a snout ending in a flat disc, tusks curving up, small ears.
            head = new GameObject("Head").transform;
            head.SetParent(figure, false);
            head.localPosition = new Vector3(0, 0.56f, 0.56f);
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0, 0.06f), Vector3.zero, new Vector3(0.36f, 0.34f, 0.42f), fur);
            Part(PrimitiveType.Capsule, head, new Vector3(0, -0.06f, 0.28f), new Vector3(90, 0, 0), new Vector3(0.19f, 0.15f, 0.17f), fur);
            Part(PrimitiveType.Cylinder, head, new Vector3(0, -0.06f, 0.43f), new Vector3(90, 0, 0), new Vector3(0.14f, 0.012f, 0.12f), furLight);
            var tusk = Kind.Crown ? gold : ivory;
            var tuskSize = Kind.Crown ? 1.5f : 1;

            foreach (var side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Capsule, head, new Vector3(side * 0.09f, -0.08f, 0.36f), new Vector3(-45, 0, side * -25), new Vector3(0.03f, 0.07f, 0.03f) * tuskSize, tusk);
                Part(PrimitiveType.Cube, head, new Vector3(side * 0.13f, 0.15f, -0.02f), new Vector3(-25, 0, side * -35), new Vector3(0.08f, 0.11f, 0.03f), furDark);
            }

            Eyes(head, 0.12f, 0.05f, 0.2f, 0.04f);

            // A king's mane of pale bristles over the shoulders.
            if (Kind.Tier > 0)
            {
                Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.84f, 0.3f), Vector3.zero, new Vector3(0.4f, 0.28f, 0.5f), furDark);
            }

            tail = new GameObject("Tail").transform;
            tail.SetParent(figure, false);
            tail.localPosition = new Vector3(0, 0.62f, -0.56f);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, -0.08f, -0.04f), new Vector3(-30, 0, 0), new Vector3(0.04f, 0.1f, 0.04f), furDark);

            var front = 0.3f;
            var back = -0.36f;
            Leg(0, new Vector3(-0.16f, 0.42f, front), 0.21f, 0.19f, 0.13f, fur, furDark);
            Leg(1, new Vector3(0.16f, 0.42f, front), 0.21f, 0.19f, 0.13f, fur, furDark);
            Leg(2, new Vector3(-0.16f, 0.42f, back), 0.21f, 0.19f, 0.13f, fur, furDark);
            Leg(3, new Vector3(0.16f, 0.42f, back), 0.21f, 0.19f, 0.13f, fur, furDark);
        }

        void BuildBear(Material fur, Material furDark, Material furLight)
        {
            // Big and round: a heavy body, a hump over the shoulders.
            Part(PrimitiveType.Capsule, figure, new Vector3(0, 0.76f, -0.06f), new Vector3(90, 0, 0), new Vector3(0.74f, 0.62f, 0.68f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.9f, 0.26f), Vector3.zero, new Vector3(0.7f, 0.62f, 0.62f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.6f, 0.08f), Vector3.zero, new Vector3(0.54f, 0.36f, 0.8f), furDark);

            // A round head, a short pale snout, round ears.
            head = new GameObject("Head").transform;
            head.SetParent(figure, false);
            head.localPosition = new Vector3(0, 0.9f, 0.64f);
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.zero, new Vector3(0.42f, 0.4f, 0.42f), fur);
            Part(PrimitiveType.Capsule, head, new Vector3(0, -0.07f, 0.22f), new Vector3(90, 0, 0), new Vector3(0.2f, 0.13f, 0.17f), furLight);
            Part(PrimitiveType.Sphere, head, new Vector3(0, -0.03f, 0.35f), Vector3.zero, new Vector3(0.08f, 0.06f, 0.06f), nose);

            foreach (var side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.16f, 0.18f, -0.02f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.06f), fur);
            }

            Eyes(head, 0.1f, 0.06f, 0.17f, 0.04f);

            // A white crescent on the chest, as black bears have.
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.82f, 0.52f), new Vector3(20, 0, 0), new Vector3(0.36f, 0.08f, 0.06f), furLight);

            tail = new GameObject("Tail").transform;
            tail.SetParent(figure, false);
            tail.localPosition = new Vector3(0, 0.86f, -0.72f);
            Part(PrimitiveType.Sphere, tail, Vector3.zero, Vector3.zero, new Vector3(0.14f, 0.14f, 0.12f), fur);

            Leg(0, new Vector3(-0.24f, 0.62f, 0.38f), 0.33f, 0.27f, 0.24f, fur, furDark);
            Leg(1, new Vector3(0.24f, 0.62f, 0.38f), 0.33f, 0.27f, 0.24f, fur, furDark);
            Leg(2, new Vector3(-0.24f, 0.62f, -0.42f), 0.33f, 0.27f, 0.24f, fur, furDark);
            Leg(3, new Vector3(0.24f, 0.62f, -0.42f), 0.33f, 0.27f, 0.24f, fur, furDark);
        }

        void BuildMonkey(Material fur, Material furDark, Material furLight)
        {
            // Hunched on long arms: the body slanting up to the shoulders.
            Part(PrimitiveType.Capsule, figure, new Vector3(0, 0.66f, -0.04f), new Vector3(55, 0, 0), new Vector3(0.34f, 0.36f, 0.3f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.82f, 0.16f), Vector3.zero, new Vector3(0.4f, 0.4f, 0.34f), furDark);

            // A round head, a pale face, jade eyes, a heavy brow, round ears.
            head = new GameObject("Head").transform;
            head.SetParent(figure, false);
            head.localPosition = new Vector3(0, 1.04f, 0.3f);
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.zero, new Vector3(0.3f, 0.3f, 0.28f), fur);
            Part(PrimitiveType.Sphere, head, new Vector3(0, -0.03f, 0.09f), Vector3.zero, new Vector3(0.22f, 0.22f, 0.13f), furLight);
            Part(PrimitiveType.Cube, head, new Vector3(0, 0.07f, 0.13f), Vector3.zero, new Vector3(0.21f, 0.04f, 0.06f), furDark);

            foreach (var side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.15f, 0.02f, 0), Vector3.zero, new Vector3(0.07f, 0.09f, 0.04f), furLight);
            }

            Eyes(head, 0.055f, 0.025f, 0.15f, 0.05f);

            // A long tail curled up behind.
            tail = new GameObject("Tail").transform;
            tail.SetParent(figure, false);
            tail.localPosition = new Vector3(0, 0.6f, -0.3f);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, 0.16f, -0.14f), new Vector3(-40, 0, 0), new Vector3(0.05f, 0.2f, 0.05f), fur);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, 0.42f, -0.18f), new Vector3(20, 0, 0), new Vector3(0.045f, 0.15f, 0.045f), furDark);

            // Long arms in front, short legs behind.
            Leg(0, new Vector3(-0.18f, 0.84f, 0.2f), 0.42f, 0.38f, 0.09f, fur, furDark);
            Leg(1, new Vector3(0.18f, 0.84f, 0.2f), 0.42f, 0.38f, 0.09f, fur, furDark);
            Leg(2, new Vector3(-0.15f, 0.5f, -0.22f), 0.26f, 0.23f, 0.1f, fur, furDark);
            Leg(3, new Vector3(0.15f, 0.5f, -0.22f), 0.26f, 0.23f, 0.1f, fur, furDark);
        }

        void BuildSnake(Material fur, Material furDark, Material furLight)
        {
            // A banded body of ten segments, thick behind the head and thinning to the tail.
            const int Count = 10;
            const float Length = 0.27f;
            segments = new Transform[Count];
            var parent = figure;

            for (var i = 0; i < Count; i++)
            {
                var segment = new GameObject("Segment").transform;
                segment.SetParent(parent, false);
                segment.localPosition = i == 0 ? new Vector3(0, 0.1f, 0.55f) : new Vector3(0, 0, -Length);
                var thick = Mathf.Lerp(0.2f, 0.06f, i / (float)(Count - 1));
                Part(PrimitiveType.Capsule, segment, new Vector3(0, 0, -Length / 2), new Vector3(90, 0, 0), new Vector3(thick, Length * 0.62f, thick * 0.85f), i % 2 == 0 ? fur : furDark);
                segments[i] = segment;
                parent = segment;
            }

            // The head raised before it on its neck: flat and wide, yellow eyes, a forked red tongue.
            head = new GameObject("Head").transform;
            head.SetParent(segments[0], false);
            head.localPosition = new Vector3(0, 0.16f, 0.12f);
            Part(PrimitiveType.Capsule, head, new Vector3(0, -0.08f, -0.06f), new Vector3(30, 0, 0), new Vector3(0.12f, 0.1f, 0.11f), fur);
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0, 0.04f), Vector3.zero, new Vector3(0.18f, 0.1f, 0.24f), furDark);
            Part(PrimitiveType.Sphere, head, new Vector3(0, -0.03f, 0.05f), Vector3.zero, new Vector3(0.15f, 0.06f, 0.2f), furLight);
            Eyes(head, 0.065f, 0.03f, 0.1f, 0.035f);
            Part(PrimitiveType.Cube, head, new Vector3(0, -0.02f, 0.2f), Vector3.zero, new Vector3(0.012f, 0.008f, 0.1f), Paint("Tongue", new Color(0.7f, 0.1f, 0.12f)));
        }

        /// <summary>The snake's sway: a wave running down its body, wider as it goes faster.</summary>
        void Slither(float pace)
        {
            if (segments == null)
            {
                return;
            }

            var amplitude = 12 + 14 * pace;

            for (var i = 1; i < segments.Length; i++)
            {
                segments[i].localRotation = Quaternion.Euler(0, Mathf.Sin(stride * 1.3f - i * 0.75f) * amplitude, 0);
            }
        }

        /// <summary>A stone monkey's body turns to grey rock as it dies.</summary>
        void Petrify()
        {
            stoneGrey ??= Paint("Beast Stone", new Color(0.42f, 0.42f, 0.4f));

            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = new[] { stoneGrey };
            }

            Body.Anime.Apply(figure.gameObject);
            Cultivation.GuGlow.Flash(transform.position + Vector3.up * Height * 0.5f, 1.2f * Kind.Scale, new Color(0.55f, 0.55f, 0.5f), 0.4f);
        }
    }
}
