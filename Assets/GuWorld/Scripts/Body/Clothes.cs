using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// What a village youth wears in GU World — plain clothes of an old
    /// mountain clan: wide trousers bound at the shins, cloth shoes, a
    /// cross-collared tunic (the left side over the right) with sleeves that
    /// widen to the wrist, its skirt to mid-thigh (to the knee for a girl),
    /// dark trim at the collar, cuffs and hem, and a sash tied at the waist.
    ///
    /// Dressed after the body, hair and beard, layer by layer from the skin
    /// out. Most of it is cut from the body's own skin (Shell) and lifted off
    /// it, so it bends exactly as the body does; the skirt and the sash's ends
    /// hang free of the legs, skinned to the hips and thighs.
    /// </summary>
    public static class Clothes
    {
        /// <summary>The cloth's colours: plain, undyed and earth-dyed.</summary>
        public struct Palette
        {
            public Color tunic, trim, sash, trousers, wraps, shoes;
        }

        /// <summary>
        /// The dyes a village has (for the villagers): 0 is the hero's own for each gender;
        /// then hemp brown, indigo grey, undyed, the guard's dark with red, the elder's plum
        /// with gold, sage, rust.
        /// </summary>
        public static Palette For(Gender gender, int index = 0)
        {
            Palette P(int tunic, int trim, int sash, int trousers) => new Palette
            {
                tunic = Hex(tunic), trim = Hex(trim), sash = Hex(sash),
                trousers = Hex(trousers), wraps = Hex(0xb8ae98), shoes = Hex(0x1f1c1a),
            };

            switch (index)
            {
                case 1: return P(0x6b5a45, 0x2e2620, 0x3f4a5a, 0x3a3631);
                case 2: return P(0x4a5568, 0x1f2430, 0x8a6a3a, 0x33322f);
                case 3: return P(0x857660, 0x3a3026, 0x5a3020, 0x45403a);
                case 4: return P(0x3a3f3a, 0x7a2a22, 0x2a2a2a, 0x2c2b29);
                case 5: return P(0x5a4a66, 0xb89a5a, 0x3a2e40, 0x3a3436);
                case 6: return P(0x6a7d6a, 0x2c3a2c, 0x7a5a3a, 0x3c3a34);
                case 7: return P(0x8a5a45, 0x2e1e18, 0x4a4a3a, 0x3a3430);
            }

            return gender == Gender.Female
                ? P(0x7d5f5a, 0x2d2422, 0x4a5a4e, 0x3f3a36)
                : P(0x56686a, 0x262a2c, 0x7a4e2d, 0x3d3a35);
        }

        public const int Palettes = 8;

        /// <summary>Where the body's joints are in the rest pose (the mesh's bind pose).</summary>
        class Landmarks
        {
            readonly SkinData body;

            public Landmarks(SkinData body)
            {
                this.body = body;
            }

            public Vector3 this[string bone]
            {
                get
                {
                    var index = body.BoneIndex(bone);

                    return index < 0 ? Vector3.zero : body.Mesh.bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
                }
            }
        }

        static float SmoothStep(float x, float min, float max)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>Dresses the body; the pieces are made beside it and given back.</summary>
        public static List<SkinnedMeshRenderer> Dress(SkinData body, HeadFrame frame, Gender gender, Material clothTemplate, int dye = 0)
        {
            var palette = For(gender, dye);
            var at = new Landmarks(body);
            var pieces = new List<SkinnedMeshRenderer>();

            void Add(SkinnedMeshRenderer piece)
            {
                if (piece != null) pieces.Add(piece);
            }

            Material Cloth(Color colour, string name)
            {
                var material = new Material(clothTemplate) { name = name };
                SetColour(material, colour);

                return material;
            }

            var tunicCloth = Cloth(palette.tunic, "Tunic");
            var trimCloth = Cloth(palette.trim, "Trim");
            var sashCloth = Cloth(palette.sash, "Sash");

            // Landmarks of the rest pose: the arms are out to the sides along X.
            var waist = at["spine_01"].y + 0.02f;
            var hips = (at["thigh_l"].y + at["thigh_r"].y) / 2;
            var knee = (at["calf_l"].y + at["calf_r"].y) / 2;
            var ankle = (at["foot_l"].y + at["foot_r"].y) / 2;
            var neck = at["neck_01"].y;
            var elbow = Mathf.Abs(at["lowerarm_l"].x);
            var wrist = Mathf.Abs(at["hand_l"].x);
            var feet = body.Shares(n => n.StartsWith("foot") || n.StartsWith("ball"));
            var legs = body.Shares(n => n.StartsWith("thigh") || n.StartsWith("calf"));
            var hands = body.Shares(n => n.StartsWith("hand") || n.StartsWith("index") || n.StartsWith("middle") ||
                                         n.StartsWith("ring") || n.StartsWith("pinky") || n.StartsWith("thumb"));
            var arms = body.Shares(n => n.StartsWith("upperarm") || n.StartsWith("lowerarm") || n.StartsWith("clavicle"));
            var onHead = frame.OnHead;

            // 1. Trousers: waist to just above the ankle, loose below the knee.
            var trousers = new float[body.Count];
            var trouserLift = new float[body.Count];

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                trousers[i] = Mathf.Min(
                    Mathf.Min(Shell.Inside(waist + 0.01f - p.y), Shell.Inside(p.y - (ankle + 0.05f))),
                    Mathf.Min(1 - Shell.Inside(arms[i] + hands[i] - 0.5f, 0.2f), 1 - Shell.Inside(feet[i] - 0.5f, 0.2f)));
                // Wide trousers: loose from the thigh, fullest below the knee, gathered into the wraps.
                var below = Mathf.Clamp01((knee + 0.1f - p.y) / (knee + 0.1f - ankle));
                var thigh = SmoothStep(p.y, hips - 0.05f, hips + 0.05f);
                trouserLift[i] = 0.016f + 0.03f * Mathf.Sin(Mathf.PI * Mathf.Min(1, 0.35f + below * 0.75f)) * (1 - SmoothStep(below, 0.8f, 1)) * (1 - thigh * 0.6f);
            }

            Add(Shell.Cut(body, trousers, 0, Cloth(palette.trousers, "Trousers"), "Cloth_trousers", trouserLift));

            // 2. Leg wraps: linen bound round the shins over the trousers' ends.
            var wraps = new float[body.Count];

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                wraps[i] = Mathf.Min(Mathf.Min(Shell.Inside(p.y - (ankle + 0.02f)), Shell.Inside(ankle + 0.17f - p.y)),
                    Mathf.Min(Shell.Inside(legs[i] + feet[i] - 0.5f, 0.2f), 1 - Shell.Inside(hands[i] - 0.5f, 0.2f)));
            }

            Add(Shell.Cut(body, wraps, 0.013f, Cloth(palette.wraps, "Wraps"), "Cloth_wraps"));

            // 3. Shoes: cloth, over the feet to the ankle.
            var shoes = new float[body.Count];

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                shoes[i] = Mathf.Min(Shell.Inside(ankle + 0.035f - p.y), Shell.Inside(feet[i] + legs[i] - 0.5f, 0.2f));
            }

            Add(Shell.Cut(body, shoes, 0.01f, Cloth(palette.shoes, "Shoes"), "Cloth_shoes"));

            // 4. The tunic, up to where the neck meets the shoulders, with a small V at the
            //    front whose point lies on the wearer's right (+X here), where the left side
            //    crosses over the right. Its trim — round the neck, down the crossing edge and
            //    at the cuffs — is painted into its own texture (Bake), so the trim's edges are
            //    crisp whatever the size of the body's triangles. The sleeves widen to the wrist.
            var tunic = new float[body.Count];
            var tunicLift = new float[body.Count];
            var necks = body.Shares(n => n == "neck_01" || n == "Head");

            // Where the neck meets the body, behind and in front (its middle line).
            float seamBack = 0, seamFront = 0;
            int behind = 0, ahead = 0;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (necks[i] > 0.35f && necks[i] < 0.65f && Mathf.Abs(p.x) < 0.04f)
                {
                    if (body.Normals[i].z < 0) { seamBack += p.y; behind++; }
                    else { seamFront += p.y; ahead++; }
                }
            }

            seamBack = behind > 0 ? seamBack / behind : neck + 0.04f;
            seamFront = ahead > 0 ? seamFront / ahead : neck + 0.02f;

            const float vX = 0.025f;
            const float vDepth = 0.075f;
            var vY = seamFront - 0.005f - vDepth;
            // The crossing edge: from the V's point down to under the right arm.
            var edgeEnd = new Vector2(0.16f, vY - 0.17f);

            float Front(Vector3 n) => SmoothStep(n.z, 0.2f, 0.6f);

            float Neckline(Vector3 p, Vector3 n)
            {
                var front = Front(n);

                return Mathf.Lerp(seamBack, seamFront, front) - 0.005f -
                       front * vDepth * (1 - Mathf.Clamp01(Mathf.Abs(p.x - vX) / 0.075f));
            }

            float ToEdge(Vector3 p)
            {
                var a = new Vector2(vX, vY);
                var ab = edgeEnd - a;
                var t = Mathf.Clamp01(Vector2.Dot(new Vector2(p.x, p.y) - a, ab) / ab.sqrMagnitude);

                return Vector2.Distance(new Vector2(p.x, p.y), a + ab * t);
            }

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                var line = Neckline(p, body.Normals[i]);
                var torso = Mathf.Min(Shell.Inside(line - p.y), Shell.Inside(p.y - (hips - 0.04f)));
                var notHead = 1 - Shell.Inside(Mathf.Max(onHead[i], necks[i]) - 0.6f, 0.2f);
                var notHand = 1 - Shell.Inside(hands[i] - 0.35f, 0.1f);
                var notLeg = 1 - Shell.Inside(legs[i] - 0.6f, 0.2f);
                // The sleeve ends a little short of the wrist.
                var sleeve = Shell.Inside(wrist - 0.015f - Mathf.Abs(p.x), 0.01f);
                tunic[i] = Mathf.Min(Mathf.Min(torso, notHead), Mathf.Min(Mathf.Min(notHand, notLeg), sleeve));

                var along = Mathf.Clamp01((Mathf.Abs(p.x) - elbow) / Mathf.Max(0.05f, wrist - elbow));
                var onArm = Mathf.Clamp01(arms[i] + hands[i]);
                tunicLift[i] = 0.012f + 0.03f * along * along * onArm;
            }

            bool Trim(Vector3 p, Vector3 n)
            {
                var line = Neckline(p, n);
                var neckBand = line - p.y < 0.022f;
                var crossing = ToEdge(p) < 0.009f && Front(n) > 0.5f;
                var cuff = Mathf.Abs(p.x) > wrist - 0.08f;

                return neckBand || crossing || cuff;
            }

            // The cloth falls from the chest instead of clinging under it: below the
            // chest's front, each point of the tunic is brought forward to a line that
            // drops gently from there toward the sash.
            var placed = new Vector3[body.Count];
            var chestY = at["spine_03"].y;
            var front = new float[41];
            // How high the chest stands out furthest: the cloth hangs from there.
            var apexY = chestY;
            var apexZ = float.NegativeInfinity;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                placed[i] = p + body.Normals[i] * tunicLift[i];

                if (p.y > chestY - 0.12f && p.y < chestY + 0.06f && Mathf.Abs(p.x) < 0.2f && arms[i] < 0.3f)
                {
                    var column = Mathf.Clamp(Mathf.RoundToInt((p.x + 0.2f) / 0.01f), 0, 40);
                    front[column] = Mathf.Max(front[column], placed[i].z);

                    if (placed[i].z > apexZ)
                    {
                        apexZ = placed[i].z;
                        apexY = p.y;
                    }
                }
            }

            // The cloth spans the hollows: each column's front is the most forward within 6 cm.
            var spanned = new float[front.Length];

            for (var c = 0; c < front.Length; c++)
            {
                for (var k = Mathf.Max(0, c - 6); k <= Mathf.Min(front.Length - 1, c + 6); k++)
                {
                    spanned[c] = Mathf.Max(spanned[c], front[k]);
                }
            }

            front = spanned;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (tunic[i] <= 0 || body.Normals[i].z < 0.1f || Mathf.Abs(p.x) >= 0.2f || p.y >= apexY + 0.07f || arms[i] > 0.3f)
                {
                    continue;
                }

                var column = Mathf.Clamp(Mathf.RoundToInt((p.x + 0.2f) / 0.01f), 0, 40);
                // Below the chest's front it falls gently; above it, it climbs back to the collarbones.
                var fall = p.y < apexY
                    ? front[column] - (apexY - p.y) * 0.22f
                    : front[column] - (p.y - apexY) * 0.6f;
                // Less at the sides, where the cloth wraps round.
                var middle = 1 - SmoothStep(Mathf.Abs(p.x), 0.12f, 0.2f);
                placed[i].z = Mathf.Max(placed[i].z, Mathf.Lerp(placed[i].z, fall, middle));
            }

            var tunicPainted = new Material(clothTemplate) { name = "Tunic (painted)" };
            var painted = Bake(body, tunic, Trim, palette.tunic, palette.trim);

            foreach (var name in new[] { "_BaseMap", "_MainTex" })
            {
                if (tunicPainted.HasProperty(name))
                {
                    tunicPainted.SetTexture(name, painted);
                    tunicPainted.SetTextureScale(name, Vector2.one);
                }
            }

            SetColour(tunicPainted, Color.white);
            Add(Shell.Cut(body, tunic, 0, tunicPainted, "Cloth_tunic", tunicLift, placed));

            // 5. The tunic's skirt, hanging free round both legs from the waist.
            var hem = gender == Gender.Female || dye == 5 ? knee - 0.05f : Mathf.Lerp(hips, knee, 0.5f);
            Add(Skirt(body, waist + 0.03f, hem, tunicCloth, trimCloth));

            // 6. The sash over it all, and its two ends hanging at the wearer's right front.
            var sash = new float[body.Count];

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];
                sash[i] = Mathf.Min(Mathf.Min(Shell.Inside(p.y - (waist - 0.04f)), Shell.Inside(waist + 0.045f - p.y)),
                    1 - Shell.Inside(arms[i] + hands[i] - 0.5f, 0.2f));
            }

            Add(Shell.Cut(body, sash, 0.034f, sashCloth, "Cloth_sash"));
            Add(SashEnds(body, waist, sashCloth));

            return pieces;
        }

        /// <summary>
        /// A skirt: rings from the waist down to the hem, round the outline of the hips
        /// and legs; the top follows the pelvis, the lower part each thigh on its side
        /// (the middle front and back split between them). Its last band is the trim.
        /// </summary>
        static SkinnedMeshRenderer Skirt(SkinData body, float top, float hem, Material cloth, Material trim)
        {
            var centre = Vector3.zero;
            var count = 0;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (Mathf.Abs(p.y - top) < 0.05f)
                {
                    centre += p;
                    count++;
                }
            }

            centre = count > 0 ? centre / count : new Vector3(0, top, 0);
            centre.y = top;

            var outline = new Hair.Outline(body, centre, top - hem + 0.1f, top - hem + 0.25f);
            const int columns = 40;
            const int rows = 12;
            var pelvis = body.BoneIndex("pelvis");
            var thighL = body.BoneIndex("thigh_l");
            var thighR = body.BoneIndex("thigh_r");
            var positions = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var radii = new float[columns + 1, rows + 1];

            for (var c = 0; c <= columns; c++)
            {
                var angle = 2 * Mathf.PI * c / columns;
                var radius = 0f;

                for (var r = 0; r <= rows; r++)
                {
                    var t = (float)r / rows;
                    var y = Mathf.Lerp(top, hem, t);
                    // Never narrower than above: the cloth falls, it does not cling.
                    radius = Mathf.Max(radius, outline.At(angle, y) + 0.022f + 0.035f * t);
                    radii[c, r] = radius;
                }
            }

            for (var c = 0; c <= columns; c++)
            {
                var angle = 2 * Mathf.PI * c / columns;

                for (var r = 0; r <= rows; r++)
                {
                    var t = (float)r / rows;
                    // Smoothed round the ring, never pulled in.
                    var around = (radii[(c + columns - 1) % columns, r] + radii[c % columns, r] + radii[(c + 1) % columns, r]) / 3;
                    var radius = Mathf.Max(radii[c % columns, r], around);
                    var p = new Vector3(centre.x + Mathf.Sin(angle) * radius, Mathf.Lerp(top, hem, t), centre.z - Mathf.Cos(angle) * radius);
                    positions.Add(p);

                    // Down the skirt it follows the thighs more; a side follows its own thigh.
                    var legs = 0.85f * SmoothStep(t, 0.1f, 1);
                    var side = Mathf.Clamp(p.x - centre.x, -0.08f, 0.08f) / 0.08f;
                    var sums = new Dictionary<int, float>
                    {
                        [pelvis] = 1 - legs,
                    };

                    if (thighL >= 0) sums[thighL] = legs * Mathf.Clamp01(0.5f - side / 2);
                    if (thighR >= 0) sums[thighR] = legs * Mathf.Clamp01(0.5f + side / 2);

                    weights.Add(Shell.Strongest(sums));
                }
            }

            var clothTriangles = new List<int>();
            var trimTriangles = new List<int>();

            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    var a = c * (rows + 1) + r;
                    var b = a + rows + 1;
                    (r >= rows - 1 ? trimTriangles : clothTriangles).AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            }

            var clothFaces = Hair.FaceOutward(positions, clothTriangles, centre);
            var trimFaces = Hair.FaceOutward(positions, trimTriangles, centre);

            return Shell.Build(body, "Cloth_skirt", positions.ToArray(), null, null, weights.ToArray(),
                new[] { clothFaces, trimFaces }, new[] { cloth, trim });
        }

        /// <summary>The sash's two ends, hanging from the knot at the wearer's right front.</summary>
        static SkinnedMeshRenderer SashEnds(SkinData body, float waist, Material cloth)
        {
            // The front of the belly at the knot.
            var knotX = 0.07f;
            var front = 0f;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (Mathf.Abs(p.x - knotX) < 0.03f && Mathf.Abs(p.y - waist) < 0.03f)
                {
                    front = Mathf.Max(front, p.z);
                }
            }

            var pelvis = body.BoneIndex("pelvis");
            var thigh = body.BoneIndex("thigh_r");
            var positions = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var indices = new List<int>();
            const int rows = 8;

            // Two strips, side by side and a little apart, the second a bit longer.
            for (var strip = 0; strip < 2; strip++)
            {
                var x = knotX - 0.02f + strip * 0.045f;
                var length = strip == 0 ? 0.26f : 0.32f;
                var start = positions.Count;

                for (var r = 0; r <= rows; r++)
                {
                    var t = (float)r / rows;
                    var y = waist - 0.01f - length * t;
                    // Hanging off the skirt, which flares below the belly.
                    var z = front + 0.05f + 0.06f * t;

                    positions.Add(new Vector3(x - 0.018f, y, z));
                    positions.Add(new Vector3(x + 0.018f, y, z));
                    var w = Shell.Between(pelvis, thigh, 1 - 0.6f * t);
                    weights.Add(w);
                    weights.Add(w);
                }

                for (var r = 0; r < rows; r++)
                {
                    var a = start + r * 2;
                    indices.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }

            // Face forward (+Z): the front is what is seen.
            var faces = indices.ToArray();
            var n = Vector3.Cross(positions[faces[1]] - positions[faces[0]], positions[faces[2]] - positions[faces[0]]);

            if (n.z < 0)
            {
                for (var i = 0; i < faces.Length; i += 3)
                {
                    (faces[i + 1], faces[i + 2]) = (faces[i + 2], faces[i + 1]);
                }
            }

            return Shell.Build(body, "Cloth_sash_ends", positions.ToArray(), null, null, weights.ToArray(),
                new[] { faces }, new[] { cloth });
        }

        /// <summary>
        /// A texture for a piece cut from the skin (in the body's UVs): each texel of the
        /// covered triangles is the cloth's colour or the trim's, by where on the body it
        /// lies, over a fine weave. Texels outside the piece are the cloth's colour.
        /// </summary>
        static Texture2D Bake(SkinData body, float[] cover, System.Func<Vector3, Vector3, bool> trim, Color cloth, Color trimColour, int size = 1024)
        {
            var pixels = new Color32[size * size];

            Color32 Woven(Color colour, int x, int y)
            {
                var thread = (x / 2 + y / 2) % 2 == 0 ? 1f : 0.93f;

                return colour * thread;
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = Woven(cloth, x, y);
                }
            }

            var uv = body.Uv;
            var source = body.Triangles;

            for (var i = 0; i < source.Length; i += 3)
            {
                int a = source[i], b = source[i + 1], c = source[i + 2];

                if (Mathf.Max(cover[a], Mathf.Max(cover[b], cover[c])) < 0.5f)
                {
                    continue;
                }

                var ua = uv[a] * size;
                var ub = uv[b] * size;
                var uc = uv[c] * size;
                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x))) - 1);
                var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))) + 1);
                var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y))) - 1);
                var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))) + 1);
                var area = (ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y);

                if (Mathf.Abs(area) < 1e-6f)
                {
                    continue;
                }

                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var px = x + 0.5f;
                        var py = y + 0.5f;
                        var wb = ((px - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (py - ua.y)) / area;
                        var wc = ((ub.x - ua.x) * (py - ua.y) - (px - ua.x) * (ub.y - ua.y)) / area;
                        var wa = 1 - wb - wc;
                        // A texel's worth of slack past the edges, so seams in the UVs do not show.
                        const float slack = -0.08f;

                        if (wa < slack || wb < slack || wc < slack)
                        {
                            continue;
                        }

                        var p = body.Vertices[a] * wa + body.Vertices[b] * wb + body.Vertices[c] * wc;
                        var n = (body.Normals[a] * wa + body.Normals[b] * wb + body.Normals[c] * wc).normalized;

                        if (trim(p, n))
                        {
                            pixels[y * size + x] = Woven(trimColour, x, y);
                        }
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Tunic (painted)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 16,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            return texture;
        }

        public static void SetColour(Material material, Color colour)
        {
            foreach (var name in new[] { "_BaseColor", "baseColorFactor", "_Color" })
            {
                if (material.HasProperty(name))
                {
                    material.SetColor(name, colour);
                }
            }
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
