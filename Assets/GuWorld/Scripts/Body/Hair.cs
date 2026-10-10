using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Hair and beards for the realistic body, made from the body itself
    /// (ported from client/body/hair.ts):
    ///
    /// - everything is placed by the head's measurements (HeadFrame), in the
    ///   model's rest pose;
    /// - a cap of hair is cut from the scalp's skin (Shell), down to a hairline
    ///   that runs over the ears to the nape — or, with a fringe, down to the
    ///   brows in strands;
    /// - long hair and a bob hang as a curtain from under the cap, kept off the
    ///   body by its outline round the head's axis, the top turning with the
    ///   head and the ends with the chest;
    /// - a ponytail is a tapering tube from the crown;
    /// - buns and the parted cut are the models' own hair, carried over to the
    ///   other body when it is the other gender's (Borrow);
    /// - a beard is a thin shell cut from the jaw, chin and upper lip.
    /// </summary>
    public static class Hair
    {
        /// <summary>The models' own hair meshes, by style.</summary>
        public static string ModelHair(HairStyle hair) => hair switch
        {
            HairStyle.Parted => "Hair_SimpleParted",
            HairStyle.Buns => "Hair_Buns",
            _ => null,
        };

        static bool Fringe(HairStyle hair) => hair == HairStyle.Bob || hair == HairStyle.Long;

        static bool Capped(HairStyle hair) =>
            hair == HairStyle.Short || hair == HairStyle.Bob || hair == HairStyle.Ponytail || hair == HairStyle.Long;

        /// <summary>
        /// Grows the hairstyle and beard on a body in its rest pose. The models' own
        /// hair is handled by the caller (it is already there, or Borrow brings it).
        /// </summary>
        /// <param name="strands">A hair material with a strand texture, for the curtain and the tail (null: the plain one).</param>
        public static List<SkinnedMeshRenderer> Grow(SkinData body, HeadFrame frame, Look look, Material hair, Material stubble, Material strands = null)
        {
            var grown = new List<SkinnedMeshRenderer>();

            void Add(SkinnedMeshRenderer piece)
            {
                if (piece != null) grown.Add(piece);
            }

            if (Capped(look.Hair))
            {
                Add(Shell.Cut(body, Scalp(body, frame, Fringe(look.Hair)), 0.005f * frame.Scale, hair, "Hair_cap"));
            }

            if (look.Hair == HairStyle.Bob || look.Hair == HairStyle.Long)
            {
                Add(Curtain(body, frame, look.Hair, strands != null ? strands : hair));
            }

            if (look.Hair == HairStyle.Ponytail)
            {
                Add(Ponytail(body, frame, strands != null ? strands : hair));
            }

            if (look.BeardStyle != Beard.None)
            {
                Add(BeardOf(body, frame, look.BeardStyle, look.BeardStyle == Beard.Stubble ? stubble : hair));
            }

            return grown;
        }

        /// <summary>A triangle wave, 0…1, `period` long: strand tips along an edge.</summary>
        static float Strands(float at, float period)
        {
            var t = at / period % 1;

            if (t < 0) t += 1;

            return 1 - Mathf.Abs(t * 2 - 1);
        }

        static float SmoothStep(float x, float min, float max)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>
        /// The scalp under a hairline: at the nape behind, above the ears at the sides,
        /// a little up the forehead in front — or, with a fringe, down to the brows in
        /// pointed strands.
        /// </summary>
        static float[] Scalp(SkinData body, HeadFrame frame, bool fringe)
        {
            var s = frame.Scale;
            var eyeY = frame.EyeY;
            var cover = new float[body.Count];
            // Hairline height from the back (0) of the head to the front (1).
            float[] at = { 0, 0.3f, 0.5f, 0.72f, 0.86f, 1 };
            var front = fringe ? frame.Brow - 0.002f * s : frame.Brow + 0.03f * s;
            float[] line = { frame.Mouth, frame.Mouth, eyeY + 0.014f * s, eyeY + 0.026f * s, front, front };

            for (var i = 0; i < body.Count; i++)
            {
                if (frame.OnHead[i] < 0.3f)
                {
                    continue;
                }

                var p = body.Vertices[i];
                var t = Mathf.Clamp01((p.z - frame.Back) / (frame.Front - frame.Back));
                var k = 1;

                while (k < at.Length - 1 && t > at[k])
                {
                    k++;
                }

                var hairline = Mathf.Lerp(line[k - 1], line[k], (t - at[k - 1]) / (at[k] - at[k - 1]));

                if (fringe && t > 0.8f)
                {
                    // Strands hang down over the forehead.
                    hairline -= Strands(p.x + 0.5f, 0.016f * s) * 0.008f * s;
                }

                cover[i] = Mathf.Min(frame.OnHead[i], Shell.Inside(p.y - hairline));
            }

            return cover;
        }

        /// <summary>
        /// How far out the body reaches round the head's upright axis: by angle (from
        /// the back) and height. The arms (out in the rest pose) are left out.
        /// </summary>
        public class Outline
        {
            const int Angles = 36;
            const float Step = 0.01f;
            readonly float[] reach;
            readonly float bottom;
            readonly int rows;
            readonly Vector3 centre;

            public Outline(SkinData body, Vector3 centre, float below = 0.8f, float span = 1.1f, System.Func<string, bool> leaveOut = null)
            {
                this.centre = centre;
                bottom = centre.y - below;
                rows = Mathf.CeilToInt(span / Step);
                reach = new float[Angles * rows];
                var arms = body.Shares(leaveOut ?? (name =>
                    name.StartsWith("upperarm") || name.StartsWith("lowerarm") || name.StartsWith("hand") ||
                    name.StartsWith("index") || name.StartsWith("middle") || name.StartsWith("ring") ||
                    name.StartsWith("pinky") || name.StartsWith("thumb")));

                for (var i = 0; i < body.Count; i++)
                {
                    if (arms[i] > 0.3f)
                    {
                        continue;
                    }

                    var p = body.Vertices[i];
                    var cell = Cell(Angle(p), p.y);

                    if (cell >= 0)
                    {
                        reach[cell] = Mathf.Max(reach[cell], Radius(p));
                    }
                }
            }

            public float Angle(Vector3 p) => Mathf.Atan2(p.x - centre.x, centre.z - p.z);

            public float Radius(Vector3 p) => new Vector2(p.x - centre.x, p.z - centre.z).magnitude;

            /// <summary>The furthest the body reaches near an angle and height.</summary>
            public float At(float angle, float y)
            {
                var most = 0f;

                for (var a = -1; a <= 1; a++)
                {
                    for (var dy = -0.02f; dy <= 0.0201f; dy += Step)
                    {
                        var cell = Cell(angle + a * 2 * Mathf.PI / Angles, y + dy);

                        if (cell >= 0)
                        {
                            most = Mathf.Max(most, reach[cell]);
                        }
                    }
                }

                return most;
            }

            int Cell(float angle, float y)
            {
                var row = Mathf.FloorToInt((y - bottom) / Step);
                var turn = (angle / (2 * Mathf.PI) + 1.5f) % 1;

                if (row < 0 || row >= rows)
                {
                    return -1;
                }

                return row * Angles + Mathf.Min(Angles - 1, Mathf.FloorToInt(turn * Angles));
            }
        }

        /// <summary>Skin weights shared between the head and the chest: 1 all on the head.</summary>
        static BoneWeight[] HeadAndChest(SkinData body, IList<float> onHead)
        {
            var head = body.BoneIndex("Head");
            var chest = body.BoneIndex("spine_03");
            var weights = new BoneWeight[onHead.Count];

            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = Shell.Between(head, chest, onHead[i]);
            }

            return weights;
        }

        /// <summary>
        /// Hair hanging round the back and sides from under the cap: to the jaw (bob)
        /// or down the back (long), its ends cut in strands.
        /// </summary>
        static SkinnedMeshRenderer Curtain(SkinData body, HeadFrame frame, HairStyle hair, Material material)
        {
            var s = frame.Scale;
            var outline = new Outline(body, frame.Centre);
            const int columns = 28;
            const int rows = 18;
            var widest = hair == HairStyle.Bob ? 2.06f : 1.96f;
            var top = frame.Centre.y + 0.03f * s;
            var backEnd = hair == HairStyle.Bob ? frame.Chin - 0.02f * s : frame.Centre.y - 0.42f;
            var sideEnd = hair == HairStyle.Bob ? frame.Chin + 0.005f * s : frame.Chin - 0.1f * s;
            var clearance = 0.012f * s;
            var radii = new float[columns + 1, rows + 1];

            for (var c = 0; c <= columns; c++)
            {
                var angle = Mathf.Lerp(-widest, widest, (float)c / columns);
                var end = Mathf.Lerp(backEnd, sideEnd, Mathf.Pow(Mathf.Abs(angle / widest), 1.5f));
                var radius = 0f;

                for (var r = 0; r <= rows; r++)
                {
                    var y = Mathf.Lerp(top, end, (float)r / rows);
                    radius = Mathf.Max(radius, outline.At(angle, y) + clearance + 0.012f * s * r / rows);
                    radii[c, r] = radius;
                }
            }

            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var onHead = new List<float>();

            for (var c = 0; c <= columns; c++)
            {
                var angle = Mathf.Lerp(-widest, widest, (float)c / columns);
                var end = Mathf.Lerp(backEnd, sideEnd, Mathf.Pow(Mathf.Abs(angle / widest), 1.5f));
                var tip = c % 2 == 1 ? 0.012f * s : 0;

                // Strands run down: the texture repeats round the head, once top to bottom.
                for (var r = 0; r <= rows; r++)
                {
                    uvs.Add(new Vector2(3f * c / columns, 1 - (float)r / rows));
                }

                for (var r = 0; r <= rows; r++)
                {
                    // Smoothed across the columns, never pulled in.
                    var radius = Mathf.Max(radii[c, r],
                        (radii[Mathf.Max(0, c - 1), r] + radii[c, r] + radii[Mathf.Min(columns, c + 1), r]) / 3);
                    var y = Mathf.Lerp(top, end, (float)r / rows) + (r == rows ? tip : 0);

                    positions.Add(new Vector3(
                        frame.Centre.x + Mathf.Sin(angle) * radius,
                        y,
                        frame.Centre.z - Mathf.Cos(angle) * radius));
                    onHead.Add(1 - SmoothStep(frame.Centre.y - y, 0.05f, 0.2f));
                }
            }

            var indices = new List<int>();

            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    var a = c * (rows + 1) + r;
                    var b = a + rows + 1;
                    indices.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            }

            var triangles = FaceOutward(positions, indices, frame.Centre);

            return Shell.Build(body, "Hair_curtain", positions.ToArray(), null, uvs.ToArray(), HeadAndChest(body, onHead),
                new[] { triangles }, new[] { material });
        }

        /// <summary>A tail tied at the crown, falling down the back and narrowing to a point.</summary>
        static SkinnedMeshRenderer Ponytail(SkinData body, HeadFrame frame, Material material)
        {
            var s = frame.Scale;
            var outline = new Outline(body, frame.Centre);
            float Behind(float y, float gap) => frame.Centre.z - outline.At(0, y) - gap;
            var tie = frame.Centre.y + 0.035f * s;
            var length = 0.28f * s;
            Vector3[] knots =
            {
                new Vector3(0, tie, Behind(tie, -0.012f * s)),
                new Vector3(0, tie - 0.01f * s, Behind(tie, 0.04f * s)),
                new Vector3(0, tie - 0.12f * s, Behind(tie - 0.12f * s, 0.035f * s)),
                new Vector3(0, tie - length, Behind(tie - length, 0.025f * s)),
            };
            const int segments = 24;
            const int sides = 10;
            var radius = 0.026f * s;
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var onHead = new List<float>();
            var normal = Vector3.right;

            for (var i = 0; i <= segments; i++)
            {
                var u = (float)i / segments;
                var centre = CatmullRom(knots, u);
                var tangent = (CatmullRom(knots, Mathf.Min(1, u + 0.01f)) - CatmullRom(knots, Mathf.Max(0, u - 0.01f))).normalized;
                // A frame carried along the tube (it barely twists: the tail hangs in one plane).
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                // Thick just below the tie, thin at the tip.
                var thickness = u < 0.2f ? Mathf.Lerp(0.7f, 1.15f, u / 0.2f) : Mathf.Lerp(1.15f, 0.12f, (u - 0.2f) / 0.8f);

                for (var j = 0; j <= sides; j++)
                {
                    var angle = 2 * Mathf.PI * j / sides;
                    positions.Add(centre + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius * thickness);
                    uvs.Add(new Vector2((float)j / sides, 1 - u));
                    onHead.Add(1 - SmoothStep(u, 0.15f, 0.55f));
                }
            }

            var indices = new List<int>();

            for (var i = 0; i < segments; i++)
            {
                for (var j = 0; j < sides; j++)
                {
                    var a = i * (sides + 1) + j;
                    var b = a + sides + 1;
                    indices.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            }

            var triangles = FaceOutward(positions, indices, null, sides + 1);

            return Shell.Build(body, "Hair_tail", positions.ToArray(), null, uvs.ToArray(), HeadAndChest(body, onHead),
                new[] { triangles }, new[] { material });
        }

        static Vector3 CatmullRom(Vector3[] knots, float u)
        {
            var segments = knots.Length - 1;
            var f = Mathf.Clamp01(u) * segments;
            var i = Mathf.Min(segments - 1, Mathf.FloorToInt(f));
            var t = f - i;
            var p0 = knots[Mathf.Max(0, i - 1)];
            var p1 = knots[i];
            var p2 = knots[i + 1];
            var p3 = knots[Mathf.Min(segments, i + 2)];

            return 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
        }

        /// <summary>
        /// The triangles, wound so their faces look outward: away from `centre`'s upright
        /// axis, or (for a tube, `ring` vertices a ring) away from the middle of each ring.
        /// </summary>
        public static int[] FaceOutward(List<Vector3> positions, List<int> indices, Vector3? centre, int ring = 0)
        {
            var score = 0f;

            for (var i = 0; i < indices.Count; i += 3)
            {
                Vector3 a = positions[indices[i]], b = positions[indices[i + 1]], c = positions[indices[i + 2]];
                // Unity's front face is clockwise seen from the front: its normal is (b - a) × (c - a).
                var normal = Vector3.Cross(b - a, c - a);
                var middle = (a + b + c) / 3;
                Vector3 outward;

                if (centre.HasValue)
                {
                    outward = middle - centre.Value;
                    outward.y = 0;
                }
                else
                {
                    var start = indices[i] / ring * ring;
                    var axis = Vector3.zero;

                    for (var k = 0; k < ring; k++)
                    {
                        axis += positions[start + k];
                    }

                    outward = middle - axis / ring;
                }

                score += Vector3.Dot(normal, outward);
            }

            var result = indices.ToArray();

            if (score < 0)
            {
                for (var i = 0; i < result.Length; i += 3)
                {
                    (result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
                }
            }

            return result;
        }

        /// <summary>
        /// A beard cut from the face: stubble (faint, everywhere a full beard grows), a
        /// moustache, a goatee (moustache and chin) or a full beard (along the jaw up to
        /// the sideburns). The lips stay clear.
        /// </summary>
        static SkinnedMeshRenderer BeardOf(SkinData body, HeadFrame frame, Beard kind, Material material)
        {
            var s = frame.Scale;
            var eyeY = frame.EyeY;
            var cover = new float[body.Count];
            var edge = 0.004f * s;

            for (var i = 0; i < body.Count; i++)
            {
                if (frame.OnHead[i] < 0.2f)
                {
                    continue;
                }

                var p = body.Vertices[i];
                var x = Mathf.Abs(p.x);
                var ahead = Shell.Inside(p.z - (frame.Centre.z - 0.01f * s), 0.01f * s);
                var lips = Mathf.Min(Shell.Inside(0.026f * s - x, edge), Shell.Inside(0.0075f * s - Mathf.Abs(p.y - frame.Mouth), edge));
                var moustache = Mathf.Min(
                    Mathf.Min(Shell.Inside(0.03f * s - x, edge), Shell.Inside(p.y - (frame.Mouth + 0.006f * s), edge)),
                    Mathf.Min(Shell.Inside(frame.Nose.y - 0.014f * s - p.y, edge), Shell.Inside(p.z - (frame.Nose.z - 0.045f * s), edge)));
                var chin = Mathf.Min(Shell.Inside(0.028f * s - x, edge), Shell.Inside(frame.Mouth - 0.007f * s - p.y, edge));
                // The beard line rises from the corners of the mouth to the ears.
                var line = Mathf.Lerp(frame.Mouth - 0.004f * s, eyeY - 0.03f * s, SmoothStep(x, 0.025f * s, 0.065f * s));
                var jaw = Shell.Inside(line - p.y, edge);
                var under = Shell.Inside(p.y - (frame.Chin - 0.035f * s), edge);
                var ears = Shell.Inside(x - (frame.HalfWidth - 0.016f * s), edge);
                var grows = kind switch
                {
                    Beard.Mustache => moustache,
                    Beard.Goatee => Mathf.Max(moustache, chin),
                    _ => Mathf.Max(moustache, jaw),
                };

                cover[i] = Mathf.Min(Mathf.Min(grows, 1 - lips), Mathf.Min(Mathf.Min(ahead, under), Mathf.Min(1 - ears, frame.OnHead[i] + 0.3f)));
            }

            return Shell.Cut(body, cover, (kind == Beard.Stubble ? 0.0012f : 0.003f) * s, material, "Beard");
        }

        /// <summary>
        /// The models' own hairstyle taken from the other model: its hair mesh fitted to
        /// this head (rest pose to rest pose, by the heads' sizes) and tied to the head bone.
        /// </summary>
        public static SkinnedMeshRenderer Borrow(SkinData body, HeadFrame frame, HeadFrame theirs, Mesh hair, Material material, string name)
        {
            var points = hair.vertices;
            var size = new Vector3(
                frame.HalfWidth / theirs.HalfWidth,
                (frame.Top - frame.Chin) / (theirs.Top - theirs.Chin),
                (frame.Front - frame.Back) / (theirs.Front - theirs.Back));

            for (var i = 0; i < points.Length; i++)
            {
                points[i] = Vector3.Scale(points[i] - theirs.Centre, size) + frame.Centre;
            }

            var head = body.BoneIndex("Head");
            var weights = new BoneWeight[points.Length];

            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = new BoneWeight { boneIndex0 = Mathf.Max(0, head), weight0 = 1 };
            }

            var submeshes = new int[hair.subMeshCount][];

            for (var m = 0; m < hair.subMeshCount; m++)
            {
                submeshes[m] = hair.GetTriangles(m);
            }

            var materials = new Material[submeshes.Length];

            for (var m = 0; m < materials.Length; m++)
            {
                materials[m] = material;
            }

            var normals = hair.normals;

            for (var i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.Scale(normals[i], new Vector3(1 / size.x, 1 / size.y, 1 / size.z)).normalized;
            }

            return Shell.Build(body, name, points, normals, hair.uv, weights, submeshes, materials);
        }
    }
}
