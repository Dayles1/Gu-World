using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.UI
{
    /// <summary>
    /// The game's icons, drawn in code: white shapes on clear, tinted by whatever shows them
    /// (gold on lacquer, the rank's colour, grey when out of reach). Each is a signed distance
    /// field — negative inside — sampled into a texture once, with soft edges at any size.
    /// Things (the Moonlight Gu's crescent, a primeval stone, a moon orchid…), the menus'
    /// marks (the aperture, the bag, the map…) and small signs (a lock, the vital Gu's heart).
    /// </summary>
    public static class Icons
    {
        const int Size = 128;

        static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();

        /// <summary>Every icon there is, by name.</summary>
        static readonly Dictionary<string, Func<Vector2, float>> shapes = new Dictionary<string, Func<Vector2, float>>
        {
            // Things.
            ["moon"] = p => Cut(Circle(p, new Vector2(-0.06f, 0), 0.8f), Circle(p, new Vector2(0.3f, 0.16f), 0.66f)),
            ["jade"] = p => Cut(Polygon(p, Regular(6, 0.82f, 0)), Ring(Polygon(p, Regular(6, 0.44f, 0)), 0.06f)),
            ["stone"] = Stone,
            ["orchid"] = Orchid,
            ["rock"] = p => Cut(Polygon(p, new[] { V(-0.8f, -0.55f), V(-0.55f, 0.35f), V(-0.05f, 0.75f), V(0.6f, 0.45f), V(0.82f, -0.2f), V(0.35f, -0.7f) }),
                Segment(p, V(-0.05f, 0.75f), V(0.05f, -0.1f), 0.05f)),
            ["bowl"] = p => Union(Union(Max(Circle(p, V(0, 0.05f), 0.78f), p.y - 0.05f), Box(p, V(0, -0.78f), V(0.3f, 0.06f))),
                Union(Segment(p, V(0.05f, 0.85f), V(0.55f, 0.15f), 0.05f), Segment(p, V(-0.25f, 0.85f), V(0.35f, 0.15f), 0.05f))),
            ["jar"] = p => Union(Union(Circle(p, V(0, -0.22f), 0.6f), Box(p, V(0, 0.42f), V(0.22f, 0.22f))), Box(p, V(0, 0.68f), V(0.34f, 0.08f))),
            ["coin"] = p => Cut(Circle(p, Vector2.zero, 0.82f), Box(p, Vector2.zero, V(0.22f, 0.22f))),
            ["petal"] = p => Ellipse(p, V(0, 0), V(0.42f, 0.8f)),
            // The menus.
            ["play"] = p => Polygon(p, new[] { V(-0.45f, -0.7f), V(0.72f, 0), V(-0.45f, 0.7f) }),
            ["book"] = p => Union(Polygon(p, new[] { V(-0.85f, -0.55f), V(-0.85f, 0.6f), V(-0.06f, 0.48f), V(-0.06f, -0.68f) }),
                Polygon(p, new[] { V(0.06f, -0.68f), V(0.06f, 0.48f), V(0.85f, 0.6f), V(0.85f, -0.55f) })),
            ["gear"] = Gear,
            ["info"] = p => Union(Ring(Circle(p, Vector2.zero, 0.78f), 0.09f), Union(Circle(p, V(0, 0.38f), 0.11f), Box(p, V(0, -0.15f), V(0.08f, 0.3f)))),
            // A door frame open on its right, and an arrow going out through it.
            ["exit"] = p => Union(Cut(Ring(Box(p, V(-0.25f, 0), V(0.45f, 0.72f)), 0.08f), Box(p, V(0.2f, 0), V(0.14f, 0.3f))),
                Union(Segment(p, V(-0.1f, 0), V(0.8f, 0), 0.08f), Union(Segment(p, V(0.8f, 0), V(0.5f, 0.3f), 0.08f), Segment(p, V(0.8f, 0), V(0.5f, -0.3f), 0.08f)))),
            ["home"] = p => Union(Polygon(p, new[] { V(-0.85f, 0.05f), V(0, 0.8f), V(0.85f, 0.05f) }), Cut(Box(p, V(0, -0.38f), V(0.6f, 0.42f)), Box(p, V(0, -0.52f), V(0.16f, 0.3f)))),
            ["ring"] = p => Union(Ring(Circle(p, Vector2.zero, 0.7f), 0.12f), Circle(p, Vector2.zero, 0.2f)),
            ["bag"] = p => Union(Union(Circle(p, V(0, -0.22f), 0.62f), Polygon(p, new[] { V(-0.3f, 0.25f), V(0.3f, 0.25f), V(0.45f, 0.65f), V(-0.45f, 0.65f) })),
                Box(p, V(0, 0.3f), V(0.38f, 0.05f))),
            ["map"] = p => Polygon(p, new[] { V(-0.85f, -0.6f), V(-0.85f, 0.6f), V(-0.3f, 0.75f), V(0.3f, 0.6f), V(0.85f, 0.75f), V(0.85f, -0.45f), V(0.3f, -0.6f), V(-0.3f, -0.45f) }),
            ["body"] = p => Union(Circle(p, V(0, 0.55f), 0.24f), Union(Segment(p, V(0, 0.18f), V(0, -0.3f), 0.2f),
                Union(Segment(p, V(-0.12f, -0.35f), V(-0.22f, -0.85f), 0.11f), Segment(p, V(0.12f, -0.35f), V(0.22f, -0.85f), 0.11f))) ),
            ["gu"] = Bug,
            // Signs.
            ["lock"] = p => Union(Cut(Box(p, V(0, -0.3f), V(0.55f, 0.42f)), Circle(p, V(0, -0.25f), 0.1f)), Max(Ring(Circle(p, V(0, 0.15f), 0.38f), 0.09f), -p.y + 0.12f)),
            ["heart"] = Heart,
            ["star"] = p => Polygon(p, Star(5, 0.85f, 0.38f)),
            ["chat"] = p => Union(Box(p, V(0, 0.15f), V(0.82f, 0.5f)), Polygon(p, new[] { V(-0.45f, -0.3f), V(-0.55f, -0.8f), V(0, -0.3f) })),
            ["wolf"] = Wolf,
            ["fist"] = Fist,
            ["salve"] = p => Union(Cut(Box(p, V(0, -0.22f), V(0.56f, 0.4f)) - 0.1f, Box(p, V(0, 0.2f), V(0.8f, 0.03f))),
                Union(Box(p, V(0, 0.36f), V(0.62f, 0.12f)) - 0.06f, Box(p, V(0, 0.6f), V(0.16f, 0.1f)))),
            ["bun"] = Bun,
            ["grass"] = p => Union(Union(Segment(p, V(0, -0.82f), V(0.04f, 0.8f), 0.085f), Segment(p, V(-0.12f, -0.82f), V(-0.62f, 0.42f), 0.075f)),
                Union(Segment(p, V(0.12f, -0.82f), V(0.66f, 0.3f), 0.075f), Box(p, V(0, -0.82f), V(0.3f, 0.06f)))),
            ["gourd"] = p => Cut(Union(Union(Circle(p, V(0, -0.36f), 0.5f), Circle(p, V(0, 0.28f), 0.3f)), Box(p, V(0, 0.7f), V(0.09f, 0.14f))),
                Box(p, V(0, 0.02f), V(0.4f, 0.035f))),
            ["pin"] = p => Union(Circle(p, V(0, 0.3f), 0.55f), Polygon(p, new[] { V(-0.47f, 0.02f), V(0.47f, 0.02f), V(0, -0.92f) })),
            // The catalogue's Gu, by what they do, and their food.
            ["shield"] = p => Cut(Polygon(p, new[] { V(-0.7f, 0.72f), V(0.7f, 0.72f), V(0.7f, 0.05f), V(0, -0.85f), V(-0.7f, 0.05f) }),
                Ring(Polygon(p, new[] { V(-0.44f, 0.5f), V(0.44f, 0.5f), V(0.44f, 0.05f), V(0, -0.55f), V(-0.44f, 0.05f) }), 0.06f)),
            ["spear"] = p => Union(Segment(p, V(-0.72f, -0.72f), V(0.35f, 0.35f), 0.08f), Polygon(p, new[] { V(0.85f, 0.85f), V(0.22f, 0.5f), V(0.5f, 0.22f) })),
            ["flame"] = p => Union(Cut(Circle(p, V(0, -0.3f), 0.55f), Circle(p, V(0.05f, -0.5f), 0.2f)),
                Polygon(p, new[] { V(-0.5f, -0.15f), V(0.05f, 0.9f), V(0.52f, -0.12f) })),
            ["flake"] = p => Union(Union(Segment(p, V(0, -0.85f), V(0, 0.85f), 0.08f), Segment(p, V(-0.74f, -0.42f), V(0.74f, 0.42f), 0.08f)),
                Union(Segment(p, V(-0.74f, 0.42f), V(0.74f, -0.42f), 0.08f), Circle(p, Vector2.zero, 0.2f))),
            ["drop"] = p => Union(Circle(p, V(0, -0.28f), 0.55f), Polygon(p, new[] { V(-0.48f, -0.05f), V(0, 0.88f), V(0.48f, -0.05f) })),
            ["bolt"] = p => Polygon(p, new[] { V(0.2f, 0.9f), V(-0.55f, -0.05f), V(-0.05f, -0.05f), V(-0.25f, -0.9f), V(0.55f, 0.15f), V(0.05f, 0.15f) }),
            ["eye"] = p => Union(Cut(Max(Circle(p, V(0, -0.55f), 0.95f), Circle(p, V(0, 0.55f), 0.95f)), Circle(p, Vector2.zero, 0.38f)), Circle(p, Vector2.zero, 0.2f)),
            ["wing"] = p => Union(Union(Ellipse(p, V(-0.1f, 0.35f), V(0.75f, 0.22f)), Ellipse(p, V(0.05f, 0), V(0.62f, 0.18f))),
                Ellipse(p, V(0.2f, -0.32f), V(0.45f, 0.15f))),
            ["bone"] = p => Union(Segment(p, V(-0.5f, -0.5f), V(0.5f, 0.5f), 0.14f),
                Union(Union(Circle(p, V(-0.62f, -0.4f), 0.17f), Circle(p, V(-0.4f, -0.62f), 0.17f)), Union(Circle(p, V(0.62f, 0.4f), 0.17f), Circle(p, V(0.4f, 0.62f), 0.17f)))),
            ["worm"] = p => Union(Union(Circle(p, V(-0.5f, -0.35f), 0.26f), Circle(p, V(-0.12f, -0.12f), 0.3f)), Union(Circle(p, V(0.28f, 0.12f), 0.3f), Circle(p, V(0.58f, 0.45f), 0.24f))),
            ["meat"] = p => Union(Ellipse(p, V(-0.15f, 0.12f), V(0.6f, 0.45f)), Union(Segment(p, V(0.25f, -0.15f), V(0.7f, -0.62f), 0.1f), Circle(p, V(0.75f, -0.7f), 0.14f))),
            ["sand"] = p => Union(Union(Circle(p, V(-0.45f, -0.4f), 0.22f), Circle(p, V(0.1f, -0.45f), 0.25f)), Union(Union(Circle(p, V(0.55f, -0.35f), 0.2f), Circle(p, V(-0.15f, 0.1f), 0.24f)), Circle(p, V(0.35f, 0.25f), 0.2f))),
            ["coal"] = p => Polygon(p, new[] { V(-0.75f, -0.4f), V(-0.4f, 0.45f), V(0.2f, 0.65f), V(0.75f, 0.2f), V(0.6f, -0.55f), V(-0.1f, -0.7f) }),
        };

        /// <summary>A thing's own colour, for its icon in a cell (a Gu's crystal blue, jade's green…); white for the menus' marks.</summary>
        static readonly Dictionary<string, Color> tints = new Dictionary<string, Color>
        {
            ["moon"] = new Color(0.66f, 0.84f, 1f),
            ["jade"] = new Color(0.52f, 0.88f, 0.62f),
            ["stone"] = new Color(0.94f, 0.92f, 0.86f),
            ["orchid"] = new Color(0.8f, 0.74f, 0.98f),
            ["rock"] = new Color(0.46f, 0.76f, 0.56f),
            ["bowl"] = new Color(0.96f, 0.8f, 0.56f),
            ["jar"] = new Color(0.62f, 0.86f, 0.5f),
            ["wolf"] = new Color(0.9f, 0.86f, 0.76f),
            ["salve"] = new Color(0.96f, 0.72f, 0.46f),
            ["bun"] = new Color(0.98f, 0.94f, 0.84f),
            ["grass"] = new Color(0.7f, 0.92f, 0.62f),
            ["gourd"] = new Color(0.95f, 0.6f, 0.48f),
            ["shield"] = new Color(0.94f, 0.76f, 0.42f),
            ["spear"] = new Color(0.96f, 0.93f, 0.82f),
            ["flame"] = new Color(1f, 0.58f, 0.3f),
            ["flake"] = new Color(0.74f, 0.92f, 1f),
            ["drop"] = new Color(0.45f, 0.7f, 1f),
            ["bolt"] = new Color(0.78f, 0.7f, 1f),
            ["eye"] = new Color(0.7f, 0.66f, 0.86f),
            ["wing"] = new Color(0.75f, 1f, 0.88f),
            ["bone"] = new Color(0.95f, 0.92f, 0.82f),
            ["worm"] = new Color(1f, 0.9f, 0.7f),
            ["meat"] = new Color(0.9f, 0.45f, 0.42f),
            ["sand"] = new Color(0.62f, 0.6f, 0.6f),
            ["coal"] = new Color(0.45f, 0.42f, 0.42f),
            ["gu"] = new Color(0.8f, 0.9f, 0.7f),
            ["star"] = new Color(1f, 0.95f, 0.65f),
            ["fist"] = new Color(0.98f, 0.82f, 0.5f),
            ["heart"] = new Color(1f, 0.55f, 0.55f),
        };

        /// <summary>The colour an icon is shown in where a thing is shown (white: the menus' own).</summary>
        public static Color Tint(string name) => name != null && tints.TryGetValue(name, out var tint) ? tint : Color.white;

        /// <summary>The icon of a name (null for none or an unknown one).</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name) || !shapes.TryGetValue(name, out var shape))
            {
                return null;
            }

            if (made.TryGetValue(name, out var sprite) && sprite != null)
            {
                return sprite;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Icon " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[Size * Size];
            // One pixel of the texture in the shapes' units (they span −1…1).
            const float pixel = 2f / Size;

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) * pixel - 1, (y + 0.5f) * pixel - 1);
                    var alpha = Mathf.Clamp01(0.5f - shape(p) / pixel);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            made[name] = sprite;

            return sprite;
        }

        public static bool Has(string name) => name != null && shapes.ContainsKey(name);

        // ── The shapes ────────────────────────────────────────────────

        /// <summary>A fist seen from the knuckles' side: four curled fingers, the thumb across them, the wrist below.</summary>
        static float Fist(Vector2 p)
        {
            var fingers = Box(p, V(0.06f, 0.22f), V(0.5f, 0.3f)) - 0.14f;
            // The gaps between the fingers, down to the thumb.
            for (var i = 0; i < 3; i++)
            {
                var x = -0.24f + i * 0.27f;
                fingers = Cut(fingers, Segment(p, V(x, 0.12f), V(x, 0.7f), 0.03f));
            }

            var thumb = Segment(p, V(-0.5f, -0.06f), V(0.18f, -0.1f), 0.13f);
            var wrist = Box(p, V(-0.02f, -0.55f), V(0.34f, 0.28f));

            return Union(Union(fingers, Cut(thumb, Segment(p, V(-0.5f, 0.1f), V(0.3f, 0.1f), 0.02f))), wrist);
        }

        /// <summary>A primeval stone: a cut gem, its facets drawn as fine gaps.</summary>
        static float Stone(Vector2 p)
        {
            var gem = Polygon(p, new[] { V(-0.62f, 0.3f), V(-0.3f, 0.72f), V(0.3f, 0.72f), V(0.62f, 0.3f), V(0, -0.82f) });
            var facets = Union(Segment(p, V(-0.62f, 0.3f), V(0.62f, 0.3f), 0.035f),
                Union(Segment(p, V(-0.2f, 0.3f), V(0, -0.82f), 0.035f), Segment(p, V(0.2f, 0.3f), V(0, -0.82f), 0.035f)));

            return Cut(gem, facets);
        }

        /// <summary>A steamed bun from the side: a low dome on a flat foot, its pleats gathered to a knot on top.</summary>
        static float Bun(Vector2 p)
        {
            var dome = Max(Ellipse(p, V(0, -0.42f), V(0.86f, 0.98f)), -(p.y + 0.5f));
            var knot = Circle(p, V(0, 0.6f), 0.12f);
            var d = Union(dome, knot);

            foreach (var x in new[] { -0.5f, -0.18f, 0.18f, 0.5f })
            {
                d = Cut(d, Segment(p, V(x * 0.25f, 0.42f), V(x, -0.18f), 0.03f));
            }

            return d;
        }

        /// <summary>A moon orchid: five petals round a hollow heart.</summary>
        static float Orchid(Vector2 p)
        {
            var d = float.PositiveInfinity;

            for (var i = 0; i < 5; i++)
            {
                var angle = Mathf.PI / 2 + i * Mathf.PI * 2 / 5;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                d = Union(d, Segment(p, dir * 0.22f, dir * 0.6f, 0.24f));
            }

            return Cut(d, Circle(p, Vector2.zero, 0.13f));
        }

        static float Gear(Vector2 p)
        {
            var r = p.magnitude;
            var a = Mathf.Atan2(p.y, p.x);
            // Eight teeth: the rim's radius rises where a tooth is.
            var tooth = Mathf.Clamp01((Mathf.Cos(a * 8) - 0.25f) * 3);
            var outer = r - (0.62f + 0.2f * tooth);

            return Cut(outer, Circle(p, Vector2.zero, 0.26f));
        }

        static float Heart(Vector2 p)
        {
            p = new Vector2(Mathf.Abs(p.x), p.y + 0.1f);
            var lobe = Circle(p, V(0.33f, 0.28f), 0.4f);
            var point = Polygon(p, new[] { V(0, -0.8f), V(0.66f, 0.12f), V(0, 0.4f) });

            return Union(lobe, point);
        }

        /// <summary>A Gu: a small beetle seen from above — head, a body split by its wing line, six legs.</summary>
        static float Bug(Vector2 p)
        {
            var body = Ellipse(p, V(0, -0.12f), V(0.44f, 0.6f));
            var head = Circle(p, V(0, 0.6f), 0.2f);
            var d = Cut(Union(body, head), Segment(p, V(0, 0.42f), V(0, -0.72f), 0.03f));
            var q = new Vector2(Mathf.Abs(p.x), p.y);

            foreach (var (from, to) in new[] { (V(0.35f, 0.15f), V(0.78f, 0.42f)), (V(0.42f, -0.12f), V(0.85f, -0.1f)), (V(0.35f, -0.4f), V(0.72f, -0.72f)) })
            {
                d = Union(d, Segment(q, from, to, 0.05f));
            }

            // The feelers.
            return Union(d, Segment(q, V(0.08f, 0.76f), V(0.3f, 0.95f), 0.035f));
        }

        /// <summary>A wolf's head from the front: ears, a long muzzle.</summary>
        static float Wolf(Vector2 p)
        {
            var q = new Vector2(Mathf.Abs(p.x), p.y);
            var head = Polygon(q, new[] { V(0, -0.85f), V(0.22f, -0.6f), V(0.62f, 0.05f), V(0.6f, 0.85f), V(0.3f, 0.4f), V(0, 0.42f) });
            var eye = Ellipse(q, V(0.28f, 0.05f), V(0.09f, 0.05f));

            return Cut(head, eye);
        }

        // ── Distance functions (shapes' units, negative inside) ───────

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Ellipse(Vector2 p, Vector2 c, Vector2 radii)
        {
            // Close enough for icons: the circle's distance in the stretched space, scaled back.
            var q = new Vector2((p.x - c.x) / radii.x, (p.y - c.y) / radii.y);

            return (q.magnitude - 1) * Mathf.Min(radii.x, radii.y);
        }

        static float Box(Vector2 p, Vector2 c, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);

            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
        }

        /// <summary>A line from a to b, `r` thick on each side.</summary>
        static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));

            return (pa - ba * h).magnitude - r;
        }

        /// <summary>A polygon's signed distance (its points in order, either way round).</summary>
        static float Polygon(Vector2 p, Vector2[] v)
        {
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1f;

            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                var c1 = p.y >= v[i].y;
                var c2 = p.y < v[j].y;
                var c3 = e.x * w.y > e.y * w.x;

                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                {
                    s = -s;
                }
            }

            return s * Mathf.Sqrt(d);
        }

        static Vector2[] Regular(int sides, float radius, float turn)
        {
            var points = new Vector2[sides];

            for (var i = 0; i < sides; i++)
            {
                var a = turn + Mathf.PI / 2 + i * Mathf.PI * 2 / sides;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            return points;
        }

        static Vector2[] Star(int points, float outer, float inner)
        {
            var star = new Vector2[points * 2];

            for (var i = 0; i < star.Length; i++)
            {
                var a = Mathf.PI / 2 + i * Mathf.PI / points;
                star[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? outer : inner);
            }

            return star;
        }

        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Max(float a, float b) => Mathf.Max(a, b);
        /// <summary>`a` with `b` taken out of it.</summary>
        static float Cut(float a, float b) => Mathf.Max(a, -b);
        /// <summary>A shape's outline, `t` thick on each side of its edge.</summary>
        static float Ring(float d, float t) => Mathf.Abs(d) - t;
    }
}
