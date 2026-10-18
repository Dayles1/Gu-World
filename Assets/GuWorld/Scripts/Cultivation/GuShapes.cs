using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// The shape a Gu's strike takes — the missile in flight (GuBolt), the hologram its aim shows (Render.SkillAim) —
    /// one and the same, by the Gu's mark: a moonblade's crescent, a spear of bone or starlight, a star, a tongue of
    /// fire, a shard of ice, a drop of water, a bolt of lightning; a plain glowing seed for the rest. Each made once,
    /// along +Z (the way it flies), a unit long and wide; `Size` says how big a Gu's is.
    /// </summary>
    public static class GuShapes
    {
        static readonly Dictionary<string, Mesh> made = new Dictionary<string, Mesh>();

        /// <summary>The mesh for a Gu's strike.</summary>
        public static Mesh For(GuKind kind)
        {
            if (kind.Skill == SkillKind.Moonblade)
            {
                return Moonblade.CrescentMesh;
            }

            var key = Key(kind);

            if (made.TryGetValue(key, out var mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = key switch
            {
                "spear" => Lathe(key, 8, 16, t => 0.5f * (t < 0.72f ? 0.35f : t < 0.8f ? 0.35f + (t - 0.72f) / 0.08f * 0.65f : 1 - (t - 0.8f) / 0.2f)),
                "star" => Star(),
                "flame" => Lathe(key, 12, 14, t => 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(1 - t, 0.55f)), 0.9f)),
                "flake" => Lathe(key, 6, 4, t => 0.5f * (1 - Mathf.Abs(2 * t - 1))),
                "drop" => Lathe(key, 12, 12, t => 0.5f * Mathf.Sqrt(Mathf.Max(0, Mathf.Sin(Mathf.PI * t))) * (0.55f + 0.45f * t)),
                "bolt" => Lathe(key, 4, 6, t => 0.5f * (t < 0.5f ? 0.6f + 0.4f * t * 2 : 1 - (t - 0.5f) * 2)),
                _ => Lathe(key, 12, 10, t => 0.5f * Mathf.Sin(Mathf.PI * t)),
            };
            made[key] = mesh;

            return mesh;
        }

        /// <summary>Which shape a Gu's strike takes, by its mark.</summary>
        static string Key(GuKind kind) => kind.Icon switch
        {
            "spear" => "spear",
            "star" => "star",
            "flame" => "flame",
            "flake" => "flake",
            "drop" => "drop",
            "bolt" => "bolt",
            _ => "seed",
        };

        /// <summary>
        /// How big a Gu's strike is (across, up, along), m: a moonblade by its crescent's scale; a missile by its rank and
        /// its shape — a spear long and thin, a star flat and wide, a shard long, a blast's missile fatter.
        /// </summary>
        public static Vector3 Size(GuKind kind, float moonScale = 1)
        {
            if (kind.Skill == SkillKind.Moonblade)
            {
                return Vector3.one * moonScale * (kind.Amount > 0 ? kind.Amount : 1);
            }

            var s = 0.22f + 0.05f * kind.Rank + (kind.Radius > 0 ? 0.12f : 0);

            return Key(kind) switch
            {
                "spear" => new Vector3(s * 0.55f, s * 0.55f, s * 6.5f),
                "star" => new Vector3(s * 1.8f, s * 1.8f, s * 0.45f),
                "flame" => new Vector3(s * 1.1f, s * 1.1f, s * 2.6f),
                "flake" => new Vector3(s * 0.8f, s * 0.8f, s * 3.4f),
                "drop" => new Vector3(s * 1.2f, s * 1.2f, s * 1.8f),
                "bolt" => new Vector3(s * 0.6f, s * 0.6f, s * 4.5f),
                _ => new Vector3(s, s, s * 2.2f),
            };
        }

        /// <summary>Whether its shape spins about its flight as it goes (a star does).</summary>
        public static bool Spins(GuKind kind) => Key(kind) == "star";

        /// <summary>
        /// A shape turned round its long axis (+Z, from -0.5 at its tail to +0.5 at its head): `radius(t)` how thick it is
        /// a share `t` of the way along, `sides` round it, `rings` along it.
        /// </summary>
        static Mesh Lathe(string name, int sides, int rings, System.Func<float, float> radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (var r = 0; r <= rings; r++)
            {
                var t = (float)r / rings;
                var rad = Mathf.Max(0, radius(t));

                for (var s = 0; s <= sides; s++)
                {
                    var a = s * Mathf.PI * 2 / sides;
                    var round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    vertices.Add(round * rad + Vector3.forward * (t - 0.5f));
                    normals.Add(round);
                }
            }

            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var k = r * (sides + 1) + s;
                    var n = k + sides + 1;
                    triangles.AddRange(new[] { k, n, k + 1, k + 1, n, n + 1 });
                }
            }

            var mesh = new Mesh { name = "Gu Shape " + name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>A five-pointed star, flat across its flight (in X and Y), with a little thickness.</summary>
        static Mesh Star()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int points = 5;

            foreach (var z in new[] { 0.5f, -0.5f })
            {
                var centre = vertices.Count;
                vertices.Add(new Vector3(0, 0, z));

                for (var i = 0; i < points * 2; i++)
                {
                    var a = Mathf.PI / 2 + i * Mathf.PI / points;
                    var r = i % 2 == 0 ? 0.5f : 0.2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z * 0.4f));
                }

                for (var i = 0; i < points * 2; i++)
                {
                    var a = centre + 1 + i;
                    var b = centre + 1 + (i + 1) % (points * 2);
                    triangles.AddRange(z > 0 ? new[] { centre, b, a } : new[] { centre, a, b });
                }
            }

            var mesh = new Mesh { name = "Gu Shape star" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
