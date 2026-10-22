using System.Collections.Generic;
using GuWorld.Cultivation;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// A skill being aimed, drawn as the Gu's own strike will go — each Gu its own: a faint trail along the very course
    /// the strike will take, as wide as the strike, all but invisible where it leaves the hand and a little stronger
    /// toward its end; and half way along it a hologram of the strike itself — the moonblade's crescent at its size
    /// and tilt, a bone spear, a star, a tongue of fire, a shard of ice (GuShapes: the same shapes the strikes fly as).
    /// A blast: its missile hanging at the top of its arc, the blast's sphere faint over the spot and its ground
    /// ringed. A sweep: the arc of the swing hanging before the hero over the ground it covers. A rush, a blink, a
    /// leap: the hero's figure where they will be. A skill on oneself: its shell or its ring round the hero. All in the
    /// Gu's colour (red when it cannot be used now), faintly through walls; the numbers from the same reckoning as the
    /// strike (Cultivator.PlanShot, BlastPoint, DashEnd, BlinkPoint), so what is shown is what happens.
    /// </summary>
    public class SkillAim : MonoBehaviour
    {
        /// <summary>The colour when the skill cannot be used now (no essence, still resting).</summary>
        static readonly Color Short = new Color(0.9f, 0.32f, 0.28f);

        const float Lift = 0.05f;
        const float Rail = 0.05f;

        /// <summary>A cross-section's corners (across, up), round it: its upper face first.</summary>
        static readonly Vector2[] Corners = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };

        /// <summary>One cross-section of a bar: its middle, which ways are across and up there, how far along it is, its colour.</summary>
        struct Station
        {
            public Vector3 at, side, up;
            public float along;
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

        // The holograms: the strike's own shape, the hero's figure, a blast's sphere.
        Transform shape, figure, sphere;
        MeshFilter shapeMesh;
        Material shapeGlow, figureGlow, sphereGlow;

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
            aim.shape = aim.Hologram("Hologram", null, out aim.shapeGlow);
            aim.shapeMesh = aim.shape.GetComponent<MeshFilter>();
            aim.figure = aim.Hologram("Hologram Figure", Primitive(PrimitiveType.Capsule), out aim.figureGlow);
            aim.sphere = aim.Hologram("Hologram Sphere", Primitive(PrimitiveType.Sphere), out aim.sphereGlow);
            go.SetActive(false);

            return aim;
        }

        Transform Hologram(string name, Mesh shapeOf, out Material glow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = shapeOf;
            glow = GuSkills.Glow(Color.white);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);

            return go.transform;
        }

        static readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();

        static Mesh Primitive(PrimitiveType type)
        {
            if (!primitives.TryGetValue(type, out var made) || made == null)
            {
                var probe = GameObject.CreatePrimitive(type);
                made = probe.GetComponent<MeshFilter>().sharedMesh;
                Destroy(probe);
                primitives[type] = made;
            }

            return made;
        }

        public void Hide() => gameObject.SetActive(false);

        static Color Tint(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        /// <summary>A hologram's breathing: a slow, slight pulse so it reads as light, not as a thing.</summary>
        static float Breath => 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3);

        // ── A missile ─────────────────────────────────────────────────

        /// <summary>
        /// A missile (a moonblade, a bolt): its trail from the hand to where it stops — as wide as the strike and lying as
        /// the strike lies (a moonblade's across its horns, tilted with them), faint at the hand and a little stronger at
        /// the end — and half way along it the strike's hologram, at its size and tilt. A ring on what it strikes, if it
        /// strikes something; the ground a blast would cover, if it bursts.
        /// </summary>
        public void ShowShot(Vector3 from, Vector3 direction, float length, float reach, GuKind kind, Vector3 size, float roll, bool usable, bool hits, Vector3 normal)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var moon = kind.Skill == SkillKind.Moonblade;
            var turn = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 0, moon ? roll : 0);
            var across = turn * Vector3.right;
            var width = moon ? Moonblade.Width * size.x : Mathf.Max(0.25f, size.x);

            // The trail: from a step out of the hand, nothing at first and a little more toward the end (weaker past where
            // the strike loses its edge).
            var lead = Mathf.Min(1, length * 0.2f);
            Ribbon(from + direction * lead, direction, across, length - lead, width, d =>
            {
                var t = (d + lead) / Mathf.Max(0.01f, length);
                var alpha = 0.15f * Mathf.Pow(t, 2.2f);

                return Tint(c, d + lead > reach ? alpha * 0.6f : alpha);
            });

            if (hits)
            {
                Disc(from + direction * length + normal * 0.03f, normal, Mathf.Max(0.25f, width * 0.3f), c, 0.4f);
            }

            if (kind.Radius > 0)
            {
                var end = from + direction * length;
                Ring(new Vector3(end.x, GroundUnder(end, end.y), end.z), kind.Radius, c, 0.45f, 0.06f);
            }

            Finish();

            // The strike's hologram half way along, as it will fly there.
            Show(shape);
            shapeMesh.sharedMesh = GuShapes.For(kind);
            shape.position = from + direction * (length * 0.5f);
            shape.rotation = moon ? turn * Quaternion.Euler(-Moonblade.TipAngle, 0, 0) : turn;
            shape.localScale = size;
            shapeGlow.color = Tint(c * 1.6f, 1) * (0.62f * Breath);
        }

        // ── A blast ───────────────────────────────────────────────────

        /// <summary>A blast: its missile's faint arc from the hand, the missile's hologram at the top of it, and over the spot the blast's sphere, faint, its ground ringed.</summary>
        public void ShowBlast(Vector3 hand, Vector3 spot, float radius, GuKind kind, Vector3 size, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var distance = Vector3.Distance(hand, spot);
            var peak = 0.6f + distance * 0.12f;
            Vector3 Arc(float t) => Vector3.Lerp(hand, spot, t) + Vector3.up * (peak * 4 * t * (1 - t));
            var points = new List<Vector3>();
            var marks = new List<float>();
            const int segments = 28;

            for (var i = 1; i <= segments; i++)
            {
                var t = (float)i / segments;
                points.Add(Arc(t));
                marks.Add(t * distance);
            }

            Curve(points, marks, d => Tint(c, 0.32f * Mathf.Pow(d / Mathf.Max(0.01f, distance), 2f)));
            Beam(new Vector2(0.03f, 0.03f), 0, 0.06f);
            Ring(spot, radius, c, 0.55f, 0.08f);
            Finish();

            Show(shape, sphere);
            shapeMesh.sharedMesh = GuShapes.For(kind);
            shape.position = Arc(0.5f);
            shape.rotation = Quaternion.LookRotation((Arc(0.55f) - Arc(0.45f)).normalized, Vector3.up);
            shape.localScale = size;
            shapeGlow.color = Tint(c * 1.6f, 1) * (0.62f * Breath);
            sphere.position = spot;
            sphere.localScale = Vector3.one * radius * 2;
            sphereGlow.color = Tint(c, 1) * (0.1f * Breath);
        }

        // ── A sweep ───────────────────────────────────────────────────

        /// <summary>A sweep before the hero: the ground it covers (`arc` degrees either side), faint toward the hero and a little stronger at its reach, and the swing's arc hanging over it.</summary>
        public void ShowSweep(Vector3 feet, Vector3 forward, float reach, float arc, GuKind kind, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var centre = new Vector3(feet.x, GroundUnder(feet, feet.y) + Lift, feet.z);
            forward = FlatForward(forward);
            var limit = arc * Mathf.Deg2Rad;
            const int segments = 28;
            const int rings = 6;
            Vector3 Way(float a) => Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up) * forward;

            // The ground it covers: rings of it, fainter toward the hero.
            for (var r = 0; r < rings; r++)
            {
                var inner = reach * r / rings;
                var outer = reach * (r + 1) / rings;
                var start = vertices.Count;

                for (var i = 0; i <= segments; i++)
                {
                    var a = Mathf.Lerp(-limit, limit, (float)i / segments);
                    Vertex(centre + Way(a) * inner, Vector3.up, Tint(c, 0.015f + 0.14f * Mathf.Pow((float)r / rings, 1.5f)), Vector4.zero);
                    Vertex(centre + Way(a) * outer, Vector3.up, Tint(c, 0.015f + 0.14f * Mathf.Pow((float)(r + 1) / rings, 1.5f)), Vector4.zero);

                    if (i > 0)
                    {
                        var k = start + (i - 1) * 2;
                        Quad(k, k + 1, k + 3, k + 2, Vector3.up);
                    }
                }
            }

            Finish();

            // The swing's arc: the crescent lying flat, its round edge at two thirds of the reach.
            Show(shape);
            shapeMesh.sharedMesh = Moonblade.CrescentMesh;
            shape.position = centre + Vector3.up * 1.0f + forward * (reach * 0.15f);
            shape.rotation = Quaternion.LookRotation(forward, Vector3.up);
            shape.localScale = Vector3.one * (reach * 0.62f / Moonblade.Outer);
            shapeGlow.color = Tint(c * 1.5f, 1) * (0.42f * Breath);
        }

        // ── Going somewhere ───────────────────────────────────────────

        /// <summary>A rush: a faint band on the ground to where it ends (stronger at the end), the hero's figure half way.</summary>
        public void ShowDash(Vector3 feet, Vector3 end, GuKind kind, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var way = end - feet;
            way.y = 0;
            var length = way.magnitude;
            var course = length > 0.01f ? way / length : Vector3.forward;
            var start = new Vector3(feet.x, GroundUnder(feet, feet.y) + Lift, feet.z);
            Ribbon(start, course, Vector3.Cross(Vector3.up, course), length, 0.7f, d => Tint(c, 0.18f * Mathf.Pow(d / Mathf.Max(0.01f, length), 2.2f)));
            Ring(new Vector3(end.x, GroundUnder(end, end.y), end.z), 0.5f, c, 0.55f, 0.05f);
            Finish();
            Figure(Vector3.Lerp(feet, end, 0.5f), c);
        }

        /// <summary>A blink: a faint arc to where the hero will stand, and their figure there.</summary>
        public void ShowBlink(Vector3 feet, Vector3 at, GuKind kind, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var from = feet + Vector3.up;
            var to = at + Vector3.up;
            var distance = Vector3.Distance(from, to);
            var points = new List<Vector3>();
            var marks = new List<float>();
            const int segments = 24;

            for (var i = 2; i <= segments; i++)
            {
                var t = (float)i / segments;
                points.Add(Vector3.Lerp(from, to, t) + Vector3.up * (distance * 0.15f * 4 * t * (1 - t)));
                marks.Add(t * distance);
            }

            Curve(points, marks, d => Tint(c, 0.32f * Mathf.Pow(d / Mathf.Max(0.01f, distance), 2f)));
            Beam(new Vector2(0.02f, 0.02f), 0, 0.04f);
            Ring(at, 0.55f, c, 0.6f, 0.06f);
            Finish();
            Figure(at, c);
        }

        /// <summary>A leap (wings of thunder): the faint arc the body will fly, a ring where it comes down, the figure at the top of it.</summary>
        public void ShowLeap(Vector3 feet, Vector3 velocity, float gravity, GuKind kind, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            var points = new List<Vector3>();
            var marks = new List<float>();
            var at = feet;
            var land = feet;
            var top = feet;
            var along = 0f;
            const float step = 0.05f;

            for (var t = step; t < 4; t += step)
            {
                var next = feet + velocity * t + Vector3.down * (0.5f * gravity * t * t);
                along += Vector3.Distance(at, next);
                at = next;

                if (next.y > top.y)
                {
                    top = next;
                }

                if (t > 0.2f && velocity.y - gravity * t < 0 && next.y <= GroundUnder(next, next.y + 2) + 0.05f)
                {
                    land = new Vector3(next.x, GroundUnder(next, next.y + 2), next.z);

                    break;
                }

                points.Add(next + Vector3.up);
                marks.Add(along);
                land = next;
            }

            Curve(points, marks, d => Tint(c, 0.32f * Mathf.Pow(d / Mathf.Max(0.01f, along), 2f)));
            Beam(new Vector2(0.025f, 0.025f), 0, 0.05f);
            Ring(land, 0.6f, c, 0.6f, 0.06f);
            Finish();
            Figure(top, c);
        }

        /// <summary>A figure (a scarecrow, or the hero) where a skill puts it.</summary>
        public void ShowFigureAt(Vector3 at, GuKind kind, bool usable)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            Ring(at, 0.5f, c, 0.55f, 0.05f);
            Finish();
            Figure(at, c);
        }

        // ── On oneself ────────────────────────────────────────────────

        /// <summary>What a skill on oneself does, as its aim shows it.</summary>
        public enum Self
        {
            /// <summary>A skin, a ward: a shell round the body.</summary>
            Shell,
            /// <summary>A heal, a buff: a ring of light at the feet.</summary>
            Body,
            /// <summary>A howl, a flash, a sense: the ground it reaches, ringed.</summary>
            Reach,
        }

        /// <summary>A skill on oneself: its shell round the body, its light at the feet, or the ground it reaches.</summary>
        public void ShowSelf(Vector3 feet, float radius, GuKind kind, bool usable, Self how)
        {
            Begin();
            var c = usable ? kind.Colour : Short;
            Ring(feet, how == Self.Reach ? radius : 0.9f, c, 0.5f, how == Self.Reach ? 0.05f : 0.12f);
            Finish();

            if (how == Self.Shell)
            {
                Show(sphere);
                sphere.position = feet + Vector3.up * 0.95f;
                sphere.localScale = new Vector3(1.3f, 2f, 1.3f);
                sphereGlow.color = Tint(c, 1) * (0.16f * Breath);
            }
            else
            {
                Show();
            }
        }

        /// <summary>The hero's figure, a hologram where the body will be.</summary>
        void Figure(Vector3 feet, Color colour)
        {
            Show(figure);
            figure.position = feet + Vector3.up * 0.85f;
            figure.rotation = Quaternion.identity;
            figure.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            figureGlow.color = Tint(colour, 1) * (0.35f * Breath);
        }

        /// <summary>Only these holograms shown.</summary>
        void Show(params Transform[] which)
        {
            shape.gameObject.SetActive(System.Array.IndexOf(which, shape) >= 0);
            figure.gameObject.SetActive(System.Array.IndexOf(which, figure) >= 0);
            sphere.gameObject.SetActive(System.Array.IndexOf(which, sphere) >= 0);
        }

        // ── Shapes ────────────────────────────────────────────────────

        /// <summary>A flat band from `from` along `axis`, `width` across `across`, its colour a distance along it by `colour` — seen from either side.</summary>
        void Ribbon(Vector3 from, Vector3 axis, Vector3 across, float length, float width, System.Func<float, Color> colour)
        {
            const float step = 0.35f;
            var normal = Vector3.Cross(axis, across).normalized;
            var half = across.normalized * (width * 0.5f);

            foreach (var side in new[] { normal, -normal })
            {
                var start = vertices.Count;
                var count = 0;

                for (var d = 0f; ; d = Mathf.Min(length, d + step))
                {
                    var at = from + axis * d;
                    var tint = colour(d);
                    Vertex(at - half, side, tint, Vector4.zero);
                    Vertex(at + half, side, tint, Vector4.zero);
                    count++;

                    if (d >= length)
                    {
                        break;
                    }
                }

                for (var i = 0; i + 1 < count; i++)
                {
                    var k = start + i * 2;
                    Quad(k, k + 1, k + 3, k + 2, side);
                }
            }
        }

        /// <summary>A ring on the ground round a point: a solid rim and a faint floor.</summary>
        void Ring(Vector3 at, float radius, Color colour, float rim, float floor)
        {
            var centre = new Vector3(at.x, GroundUnder(at, at.y) + Lift, at.z);
            const int segments = 48;
            stations.Clear();

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                stations.Add(new Station { at = centre + radial * (radius - Rail * 0.5f) + Vector3.up * (Rail * 0.5f), side = radial, up = Vector3.up, along = angle * radius, colour = Tint(colour, rim) });
            }

            Beam(new Vector2(Rail * 0.5f, Rail * 0.5f), 0, 1, false);
            var middle = vertices.Count;
            Vertex(centre + Vector3.up * 0.01f, Vector3.up, Tint(colour, floor * 0.3f), Vector4.zero);

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vertex(centre + radial * (radius - Rail) + Vector3.up * 0.01f, Vector3.up, Tint(colour, floor), Vector4.zero);

                if (i > 0)
                {
                    Triangle(middle, middle + i, middle + i + 1, Vector3.up);
                }
            }
        }

        /// <summary>A thin ring square to `normal` (where a missile strikes).</summary>
        void Disc(Vector3 at, Vector3 normal, float radius, Color colour, float alpha)
        {
            var side = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            var up = Vector3.Cross(side, normal).normalized;
            const int segments = 32;
            stations.Clear();

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                stations.Add(new Station { at = at + radial * radius, side = radial, up = normal, along = angle * radius, colour = Tint(colour, alpha) });
            }

            Beam(new Vector2(0.03f, 0.015f), 0, 1, false);
        }

        /// <summary>The cross-sections of a bar along a bent line through `points` (`along` how far along each is).</summary>
        void Curve(List<Vector3> points, List<float> along, System.Func<float, Color> colour)
        {
            stations.Clear();

            for (var i = 0; i < points.Count; i++)
            {
                var ahead = i + 1 < points.Count ? points[i + 1] - points[i] : i > 0 ? points[i] - points[i - 1] : Vector3.forward;
                stations.Add(new Station { at = points[i], side = Side(ahead), up = Up(ahead), along = along[i], colour = colour(along[i]) });
            }
        }

        static Vector3 Side(Vector3 direction)
        {
            var side = Vector3.Cross(Vector3.up, direction);

            return side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
        }

        static Vector3 Up(Vector3 direction) => Vector3.Cross(direction.normalized, Side(direction)).normalized;

        static Vector3 FlatForward(Vector3 v)
        {
            v.y = 0;

            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
        }

        /// <summary>
        /// A bar of light through the stations, its section a rectangle `half` (across, up) in half-size, each of its four
        /// long faces with its own normal; its ends closed unless it comes round to its start.
        /// </summary>
        void Beam(Vector2 half, float flow, float band, bool ends = true)
        {
            if (stations.Count < 2)
            {
                return;
            }

            for (var f = 0; f < 4; f++)
            {
                var p = Corners[f];
                var q = Corners[(f + 1) % 4];
                var start = vertices.Count;

                foreach (var s in stations)
                {
                    var normal = (s.side * (p.x + q.x) + s.up * (p.y + q.y)).normalized;
                    var uvFlow = f == 0 ? flow : 0;
                    Vertex(s.at + s.side * (p.x * half.x) + s.up * (p.y * half.y), normal, s.colour, new Vector4(s.along, p.x * half.x / band, uvFlow));
                    Vertex(s.at + s.side * (q.x * half.x) + s.up * (q.y * half.y), normal, s.colour, new Vector4(s.along, q.x * half.x / band, uvFlow));
                }

                for (var i = 0; i + 1 < stations.Count; i++)
                {
                    var k = start + i * 2;
                    Quad(k, k + 1, k + 3, k + 2, normals[k]);
                }
            }

            if (!ends)
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
