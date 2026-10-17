using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// A skill being aimed (a Gu's key held down), drawn the way a MOBA shows it — on the ground,
    /// as Smite does from behind the shoulder — fixed on where the crosshair points, and drawn as
    /// solid shapes of light (GuWorld/Aim shades their faces): for a thrown blade, a band along its
    /// course to where it will stop — rails along its edges, chevrons flowing along it, an
    /// arrowhead at its end — and the blade's crescent hanging where it stops (ShowPath); for a
    /// skill on oneself, a ring round the hero on the ground. Both show faintly through walls.
    /// </summary>
    public class SkillAim : MonoBehaviour
    {
        /// <summary>The path's colour when the skill can be used, and when there is not the essence for it.</summary>
        static readonly Color Ready = new Color(0.56f, 0.82f, 0.74f);
        static readonly Color Short = new Color(0.86f, 0.32f, 0.28f);

        /// <summary>
        /// The band begins this share of its length out from the hand: nearer, it would only lie over the
        /// hero. Only the picture starts there — the skill itself still leaves the hand.
        /// </summary>
        const float Gap = 0.35f;
        const float Lift = 0.06f;
        const float StepLength = 0.5f;
        /// <summary>The rails along the band's edges: how thick, m.</summary>
        const float Rail = 0.075f;
        /// <summary>The band itself between them: how thick, m.</summary>
        const float Sheet = 0.025f;

        /// <summary>A cross-section's corners (across, up), round it: its upper face first.</summary>
        static readonly Vector2[] Corners = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };

        /// <summary>One cross-section of a bar: its middle, which ways are across and up there, how far along it is, its colour.</summary>
        struct Station
        {
            public Vector3 at, side, up;
            public float along, across;
            public Color colour;
        }

        MeshFilter filter;
        Mesh mesh;
        Transform ignore;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();
        readonly List<Vector4> uvs = new List<Vector4>();
        readonly List<int> triangles = new List<int>();
        readonly List<Station> stations = new List<Station>();
        readonly RaycastHit[] hits = new RaycastHit[16];

        public static SkillAim Create(Transform hero)
        {
            var go = new GameObject("Skill Aim");
            var aim = go.AddComponent<SkillAim>();
            aim.ignore = hero;
            aim.filter = go.AddComponent<MeshFilter>();
            aim.mesh = new Mesh { name = "Skill Aim" };
            aim.mesh.MarkDynamic();
            aim.filter.sharedMesh = aim.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("GuWorld/Aim") ?? Shader.Find("GuWorld/Lines")) { name = "Skill Aim" };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);

            return aim;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// A thrown skill's indicator, the way a third-person MOBA (Smite) draws it: a band on the ground along the
        /// blade's course — toward the crosshair, out to the first thing in its way or the end of its range — as wide as
        /// the blade, beginning a third of the way out (Gap) and fading in there, lying over the ground as it rises and
        /// falls. Rails along its edges, chevrons flowing along it, bright as far as the blade cuts at full strength and
        /// faint beyond (a bar across where that ends), a solid arrowhead at its end. Where the blade will stop, in the
        /// air as the aim has it, hangs the blade itself, a crescent of light tipped as it flies, a line dropping from it
        /// to the band's tip. Red when the skill cannot be used now.
        /// </summary>
        public void ShowPath(Vector3 feet, Vector3 hand, Vector3 target, float width, float reach, float range, bool usable)
        {
            Begin();
            var flight = target - hand;
            var direction = flight.sqrMagnitude > 1e-4f ? flight.normalized : FlatForward(target - feet);
            var colour = usable ? Ready : Short;

            // Where it stops: the first solid thing along the flight (not the hero), or the end of its range.
            var length = range;
            var count = UnityEngine.Physics.SphereCastNonAlloc(hand, 0.1f, direction, hits, range, ~0, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                if (hits[i].distance > 0 && hits[i].distance < length && (ignore == null || !hits[i].collider.transform.IsChildOf(ignore)))
                {
                    length = hits[i].distance;
                }
            }

            length = Mathf.Max(length, width);
            var stop = hand + direction * length;

            // The course: the flight seen from above. A metre of flight is `level` metres of it (less, aimed up or down).
            var course = FlatForward(Mathf.Abs(direction.y) < 0.97f ? direction : target - feet);
            var across = Vector3.Cross(Vector3.up, course).normalized;
            var level = Mathf.Max(0.2f, Vector3.Dot(direction, course));
            var groundLength = Mathf.Max(Vector3.Dot(stop - hand, course), width * 0.8f);

            // From a third of the way out to the end; the arrowhead takes the last of it.
            var from = groundLength * Gap;
            var head = Mathf.Min(width * 0.45f, (groundLength - from) * 0.3f);
            var body = groundLength - head;
            var half = width * 0.5f;
            var fadeIn = Mathf.Max(0.05f, Mathf.Min(1.2f, (body - from) * 0.45f));
            var reachOnGround = reach * level;

            // How strong the band is a distance along: fading in at its start, faint past the full-strength cut.
            float Strength(float d) => Smooth((d - from) / fadeIn) * (d <= reachOnGround + 0.01f ? 1 : 0.45f);
            Color Tint(float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

            // The distances along it where its cross-sections are: even steps, and exactly where the full-strength cut ends.
            var marks = new List<float>();
            var steps = Mathf.Max(1, Mathf.CeilToInt((body - from) / StepLength));

            for (var i = 0; i <= steps; i++)
            {
                marks.Add(from + (body - from) * i / steps);
            }

            if (reachOnGround > from && reachOnGround < body)
            {
                marks.Add(reachOnGround);
                marks.Add(reachOnGround + 0.02f);
                marks.Sort();
            }

            // The ground under each cross-section, at both edges: the band lies across the slope as it lies along it.
            var origin = new Vector3(hand.x, feet.y, hand.z);
            var last = feet.y;
            ground.Clear();

            foreach (var d in marks)
            {
                ground.Add(Section(origin + course * d, hand.y + direction.y * (d / level), course, across, half, ref last));
            }

            // The sheet between the rails, with the chevrons on it.
            OnGround(marks, Vector2.zero, d => Tint(0.2f * Strength(d)));
            Beam(new Vector2(half - Rail, Sheet * 0.5f), 1, half);

            // The rails.
            foreach (var side in new[] { -1f, 1f })
            {
                OnGround(marks, new Vector2(side * (half - Rail * 0.5f), Rail * 0.3f), d => Tint(0.7f * Strength(d)));
                Beam(new Vector2(Rail * 0.5f, Rail * 0.5f), 0, half);
            }

            // A bar across where the blade stops cutting at full strength.
            var reachAt = marks.IndexOf(reachOnGround);

            if (reachAt >= 0 && reachOnGround > from + fadeIn * 0.5f)
            {
                var (centre, sideways, up) = ground[reachAt];
                Line(centre + up * (Rail * 0.3f), sideways, course, up, Vector2.zero, new List<float> { -half + Rail, half - Rail }, _ => Tint(0.5f));
                Beam(new Vector2(0.05f, Rail * 0.35f), 0, half);
            }

            // The arrowhead, its tip on the ground under where the blade stops.
            var (baseCentre, baseSide, baseUp) = ground[ground.Count - 1];
            var tip = Section(origin + course * groundLength, stop.y, course, across, half, ref last);
            var strong = Tint(length <= reach ? 0.7f : 0.45f);
            var lift = baseUp * (Rail * 0.45f);
            Prism(new[] { baseCentre - baseSide * (half * 1.2f) + lift, tip.centre + lift, baseCentre + baseSide * (half * 1.2f) + lift }, baseUp, Rail * 0.45f, strong, body);

            // The blade where it will stop, tipped toward the eye as it flies (its face turned up and back), and the line
            // down from it to the arrow's tip when it is in the air.
            var upright = Vector3.Cross(direction, Vector3.Cross(Vector3.up, direction).sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, direction).normalized : across).normalized;
            const float tipUp = 34 * Mathf.Deg2Rad;
            var forward = direction * Mathf.Cos(tipUp) + upright * Mathf.Sin(tipUp);
            var face = upright * Mathf.Cos(tipUp) - direction * Mathf.Sin(tipUp);
            var radius = width * 0.3f;
            var blade = stop - direction * (radius * 0.5f);
            Crescent(blade, forward, Vector3.Cross(face, forward).normalized, face, radius, colour);

            var drop = blade.y - tip.centre.y;

            if (drop > 0.5f)
            {
                Line(tip.centre, Vector3.up, across, course, Vector2.zero, new List<float> { Rail, drop - radius * 0.2f }, h => Tint(0.38f * Smooth(h / Mathf.Max(0.3f, drop * 0.5f))));
                Beam(new Vector2(0.025f, 0.025f), 0, 1);
            }

            Finish();
        }

        /// <summary>A cross-section of the band on the ground: its middle (just over the ground), which way is across it and which way up.</summary>
        readonly List<(Vector3 centre, Vector3 side, Vector3 up)> ground = new List<(Vector3, Vector3, Vector3)>();

        /// <summary>
        /// The band's cross-section at a point of the course: the ground under each of its edges (looked for from above
        /// the flight there; where there is none, as high as the last), its middle between them lifted off the ground,
        /// across from edge to edge, and up square to that and to the course.
        /// </summary>
        (Vector3 centre, Vector3 side, Vector3 up) Section(Vector3 at, float flightY, Vector3 course, Vector3 across, float half, ref float last)
        {
            var top = Mathf.Max(flightY, last) + 1.5f;
            var left = new Vector3(at.x, 0, at.z) - across * half;
            var right = new Vector3(at.x, 0, at.z) + across * half;
            left.y = GroundBelow(left, top, 14, last);
            right.y = GroundBelow(right, top, 14, last);
            last = (left.y + right.y) * 0.5f;
            var side = (right - left).normalized;
            var up = Vector3.Cross(course, side).normalized;

            return ((left + right) * 0.5f + up * Lift, side, up);
        }

        /// <summary>The cross-sections of a bar along the band on the ground (the `ground` sections), its middle `offset` (across, up) off the band's.</summary>
        void OnGround(List<float> along, Vector2 offset, System.Func<float, Color> colour)
        {
            stations.Clear();

            for (var i = 0; i < along.Count; i++)
            {
                var (centre, side, up) = ground[i];
                stations.Add(new Station
                {
                    at = centre + side * offset.x + up * offset.y,
                    side = side,
                    up = up,
                    along = along[i],
                    across = offset.x,
                    colour = colour(along[i]),
                });
            }
        }

        static float Smooth(float t)
        {
            var x = Mathf.Clamp01(t);

            return x * x * (3 - 2 * x);
        }

        static Vector3 FlatForward(Vector3 v)
        {
            v.y = 0;

            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
        }

        /// <summary>The cross-sections of a straight bar: `origin` + `axis` × each distance, its middle `offset` (across, up) off that line.</summary>
        void Line(Vector3 origin, Vector3 axis, Vector3 side, Vector3 up, Vector2 offset, List<float> along, System.Func<float, Color> colour)
        {
            stations.Clear();

            foreach (var d in along)
            {
                stations.Add(new Station
                {
                    at = origin + axis * d + side * offset.x + up * offset.y,
                    side = side,
                    up = up,
                    along = d,
                    across = offset.x,
                    colour = colour(d),
                });
            }
        }

        /// <summary>
        /// A bar of light through the stations (Line, or a ring's), its section a rectangle `half` (across, up) in
        /// half-size, each of its four long faces with its own normal; its ends closed unless it comes round to its
        /// start. `flow` puts the chevrons on its upper face; `band` is the half-width they are measured across.
        /// </summary>
        void Beam(Vector2 half, float flow, float band, bool ends = true)
        {
            for (var f = 0; f < 4; f++)
            {
                var p = Corners[f];
                var q = Corners[(f + 1) % 4];
                var start = vertices.Count;

                foreach (var s in stations)
                {
                    var normal = (s.side * (p.x + q.x) + s.up * (p.y + q.y)).normalized;
                    var uvFlow = f == 0 ? flow : 0;
                    Vertex(s.at + s.side * (p.x * half.x) + s.up * (p.y * half.y), normal, s.colour, new Vector4(s.along, (s.across + p.x * half.x) / band, uvFlow));
                    Vertex(s.at + s.side * (q.x * half.x) + s.up * (q.y * half.y), normal, s.colour, new Vector4(s.along, (s.across + q.x * half.x) / band, uvFlow));
                }

                for (var i = 0; i + 1 < stations.Count; i++)
                {
                    var k = start + i * 2;
                    Quad(k, k + 1, k + 3, k + 2, normals[k]);
                }
            }

            if (!ends || stations.Count < 2)
            {
                return;
            }

            void End(Station s, Vector3 outward)
            {
                var start = vertices.Count;

                foreach (var p in Corners)
                {
                    Vertex(s.at + s.side * (p.x * half.x) + s.up * (p.y * half.y), outward, s.colour, new Vector4(s.along, 0, 0));
                }

                Quad(start, start + 1, start + 2, start + 3, outward);
            }

            End(stations[0], (stations[0].at - stations[1].at).normalized);
            End(stations[stations.Count - 1], (stations[stations.Count - 1].at - stations[stations.Count - 2].at).normalized);
        }

        /// <summary>A flat solid: the outline `points` (round it, convex) pushed out ±`half` along `up`.</summary>
        void Prism(Vector3[] points, Vector3 up, float half, Color colour, float along)
        {
            var centre = Vector3.zero;

            foreach (var p in points)
            {
                centre += p;
            }

            centre /= points.Length;

            // The two faces (fans from the first corner).
            foreach (var sign in new[] { 1f, -1f })
            {
                var start = vertices.Count;

                foreach (var p in points)
                {
                    Vertex(p + up * (half * sign), up * sign, colour, new Vector4(along, 0, 0));
                }

                for (var i = 1; i + 1 < points.Length; i++)
                {
                    Triangle(start, start + i, start + i + 1, up * sign);
                }
            }

            // The sides.
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                var outward = Vector3.Cross(b - a, up).normalized;

                if (Vector3.Dot(outward, (a + b) * 0.5f - centre) < 0)
                {
                    outward = -outward;
                }

                var start = vertices.Count;
                var edge = new Color(colour.r, colour.g, colour.b, Mathf.Min(1, colour.a * 1.2f));
                Vertex(a + up * half, outward, edge, new Vector4(along, 0, 0));
                Vertex(b + up * half, outward, edge, new Vector4(along, 0, 0));
                Vertex(b - up * half, outward, edge, new Vector4(along, 0, 0));
                Vertex(a - up * half, outward, edge, new Vector4(along, 0, 0));
                Quad(start, start + 1, start + 2, start + 3, outward);
            }
        }

        /// <summary>
        /// A crescent of light lying in the band, `radius` round, its round edge toward `forward` and its horns back,
        /// with a thickness: its face filled faintly and brighter toward the edge, its rims solid.
        /// </summary>
        void Crescent(Vector3 centre, Vector3 forward, Vector3 side, Vector3 up, float radius, Color colour)
        {
            const int segments = 24;
            const float spread = 2.3f;
            const float thickness = 0.035f;
            var rim = new Color(Mathf.Min(1, colour.r * 1.25f), Mathf.Min(1, colour.g * 1.25f), Mathf.Min(1, colour.b * 1.25f), 0.8f);
            var fill = new Color(colour.r, colour.g, colour.b, 0.36f);
            var outer = new Vector3[segments + 1];
            var inner = new Vector3[segments + 1];
            var outward = new Vector3[segments + 1];

            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var angle = Mathf.Lerp(-spread, spread, t);
                outward[i] = forward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                outer[i] = centre + outward[i] * radius;
                // Thickest in the middle, nothing at the horns.
                inner[i] = outer[i] - outward[i] * (radius * 0.45f * Mathf.Sin(Mathf.PI * t));
            }

            // Its two faces.
            foreach (var sign in new[] { 1f, -1f })
            {
                var start = vertices.Count;
                var normal = up * sign;

                for (var i = 0; i <= segments; i++)
                {
                    Vertex(outer[i] + normal * thickness, normal, rim, Vector4.zero);
                    Vertex(inner[i] + normal * thickness, normal, fill, Vector4.zero);
                }

                for (var i = 0; i < segments; i++)
                {
                    var k = start + i * 2;
                    Quad(k, k + 2, k + 3, k + 1, normal);
                }
            }

            // Its rims: the round edge, and the hollow one.
            foreach (var (edge, sign) in new[] { (outer, 1f), (inner, -1f) })
            {
                var start = vertices.Count;

                for (var i = 0; i <= segments; i++)
                {
                    Vertex(edge[i] + up * thickness, outward[i] * sign, rim, Vector4.zero);
                    Vertex(edge[i] - up * thickness, outward[i] * sign, rim, Vector4.zero);
                }

                for (var i = 0; i < segments; i++)
                {
                    var k = start + i * 2;
                    Quad(k, k + 2, k + 3, k + 1, normals[k]);
                }
            }
        }

        /// <summary>A skill on oneself: a ring round the hero on the ground, a solid rim and a faint floor with waves flowing out.</summary>
        public void ShowRing(Vector3 feet, float radius, bool usable)
        {
            Begin();
            var colour = usable ? Ready : Short;
            var centre = new Vector3(feet.x, GroundUnder(feet, feet.y) + Lift, feet.z);
            const int segments = 48;

            // The rim.
            stations.Clear();

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                stations.Add(new Station { at = centre + radial * (radius - Rail * 0.5f) + Vector3.up * (Rail * 0.5f), side = radial, up = Vector3.up, along = angle * radius, colour = new Color(colour.r, colour.g, colour.b, 0.7f) });
            }

            Beam(new Vector2(Rail * 0.5f, Rail * 0.5f), 0, 1, false);

            // The floor: its "along" the distance from the middle, so the chevrons become waves going out.
            var middle = vertices.Count;
            Vertex(centre + Vector3.up * 0.01f, Vector3.up, new Color(colour.r, colour.g, colour.b, 0.06f), new Vector4(0, 0, 1));

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vertex(centre + radial * (radius - Rail) + Vector3.up * 0.01f, Vector3.up, new Color(colour.r, colour.g, colour.b, 0.16f), new Vector4(radius - Rail, 0, 1));

                if (i > 0)
                {
                    Triangle(middle, middle + i, middle + i + 1, Vector3.up);
                }
            }

            Finish();
        }

        void Begin()
        {
            vertices.Clear();
            normals.Clear();
            colours.Clear();
            uvs.Clear();
            triangles.Clear();
            gameObject.SetActive(true);
        }

        void Finish()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        void Vertex(Vector3 at, Vector3 normal, Color colour, Vector4 uv)
        {
            vertices.Add(at);
            normals.Add(normal);
            colours.Add(colour);
            uvs.Add(uv);
        }

        /// <summary>A triangle, its corners put in the order that faces it toward `normal` (only faces turned to the eye are drawn).</summary>
        void Triangle(int a, int b, int c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normal) < 0)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>A four-cornered face (corners in order round it), faced toward `normal`; judged by its diagonals, so a corner that has shrunk to nothing does not turn it.</summary>
        void Quad(int a, int b, int c, int d, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[b]), normal) < 0)
            {
                (b, d) = (d, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        /// <summary>The ground's height under a point (the highest solid thing within a few metres, not the hero), or `fallback`.</summary>
        float GroundUnder(Vector3 at, float fallback) => GroundBelow(at, at.y + 1.2f, 4, fallback);

        /// <summary>The height of the highest solid thing (not the hero) under a point, looked for down from `from` as far as `depth`; or `fallback`.</summary>
        float GroundBelow(Vector3 at, float from, float depth, float fallback)
        {
            var count = UnityEngine.Physics.RaycastNonAlloc(new Ray(new Vector3(at.x, from, at.z), Vector3.down), hits, depth, ~0, QueryTriggerInteraction.Ignore);
            var best = float.NegativeInfinity;

            for (var i = 0; i < count; i++)
            {
                if (hits[i].point.y > best && (ignore == null || !hits[i].collider.transform.IsChildOf(ignore)))
                {
                    best = hits[i].point.y;
                }
            }

            return float.IsNegativeInfinity(best) ? fallback : best;
        }
    }
}
