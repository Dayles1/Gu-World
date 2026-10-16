using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// A skill being aimed (a Gu's key held down), drawn on the ground the way a MOBA
    /// shows it: for a thrown blade, a path as wide as the blade out to where it fades —
    /// bright as far as it cuts at full strength, faint beyond — with an arrowhead, and a
    /// ring where the crosshair points; for a skill on oneself, a ring round the hero.
    /// The path follows the ground (steps, slopes) a little above it, and shows faintly
    /// through walls.
    /// </summary>
    public class SkillAim : MonoBehaviour
    {
        /// <summary>The path's colour when the skill can be used, and when there is not the essence for it.</summary>
        static readonly Color Ready = new Color(0.56f, 0.82f, 0.74f);
        static readonly Color Short = new Color(0.86f, 0.32f, 0.28f);
        const float Lift = 0.06f;
        const float StepLength = 0.5f;

        MeshFilter filter;
        Mesh mesh;
        Transform ignore;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();
        readonly List<int> triangles = new List<int>();
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
            renderer.sharedMaterial = new Material(Shader.Find("GuWorld/Lines")) { name = "Skill Aim" };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);

            return aim;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// A thrown skill's path from the hero's feet toward `target`: `width` wide, bright to `reach`,
        /// faint to `range` (m); red when it cannot be used now.
        /// </summary>
        public void ShowPath(Vector3 feet, Vector3 target, float width, float reach, float range, bool usable)
        {
            Begin();
            var ahead = target - feet;
            ahead.y = 0;
            ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, ahead) * (width * 0.5f);
            var colour = usable ? Ready : Short;
            var steps = Mathf.CeilToInt(range / StepLength);
            var last = feet.y;

            for (var i = 0; i <= steps; i++)
            {
                var along = Mathf.Min(range, i * StepLength);
                var at = feet + ahead * along;
                at.y = last = GroundUnder(at, last);
                var bright = along <= reach;
                var alpha = bright ? 0.3f : 0.13f;
                // The edges stronger than the middle.
                AddStrip(at - side, at + side, colour, alpha, i > 0);
            }

            // The arrowhead at the far end.
            var tip = feet + ahead * (range + width * 0.6f);
            tip.y = GroundUnder(tip, last) + Lift;
            var end = feet + ahead * range;
            end.y = last + Lift;
            var a = vertices.Count;
            vertices.Add(end - side * 1.35f);
            vertices.Add(end + side * 1.35f);
            vertices.Add(tip);
            colours.Add(new Color(colour.r, colour.g, colour.b, 0.35f));
            colours.Add(new Color(colour.r, colour.g, colour.b, 0.35f));
            colours.Add(new Color(colour.r, colour.g, colour.b, 0.55f));
            triangles.AddRange(new[] { a, a + 2, a + 1 });

            // Where the crosshair points (within the range).
            var flat = target - feet;
            flat.y = 0;

            if (flat.magnitude <= range + 0.5f)
            {
                AddRing(target + Vector3.up * Lift, 0.45f, 0.08f, colour, 0.85f);
            }

            Finish();
        }

        /// <summary>A skill on oneself: a ring round the hero.</summary>
        public void ShowRing(Vector3 feet, float radius, bool usable)
        {
            Begin();
            var colour = usable ? Ready : Short;
            AddRing(new Vector3(feet.x, GroundUnder(feet, feet.y) + Lift, feet.z), radius, 0.1f, colour, 0.75f);
            AddRing(new Vector3(feet.x, GroundUnder(feet, feet.y) + Lift, feet.z), radius * 0.92f, radius * 0.9f, colour, 0.12f);
            Finish();
        }

        void Begin()
        {
            vertices.Clear();
            colours.Clear();
            triangles.Clear();
            gameObject.SetActive(true);
        }

        void Finish()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>A cross-section of the path: its two edges and its middle, joined to the previous one.</summary>
        void AddStrip(Vector3 left, Vector3 right, Color colour, float alpha, bool join)
        {
            var middle = (left + right) * 0.5f;
            var start = vertices.Count;
            vertices.Add(left + Vector3.up * Lift);
            vertices.Add(middle + Vector3.up * Lift);
            vertices.Add(right + Vector3.up * Lift);
            colours.Add(new Color(colour.r, colour.g, colour.b, Mathf.Min(1, alpha * 2.4f)));
            colours.Add(new Color(colour.r, colour.g, colour.b, alpha * 0.6f));
            colours.Add(new Color(colour.r, colour.g, colour.b, Mathf.Min(1, alpha * 2.4f)));

            if (join)
            {
                var p = start - 3;
                triangles.AddRange(new[] { p, start, p + 1, p + 1, start, start + 1, p + 1, start + 1, p + 2, p + 2, start + 1, start + 2 });
            }
        }

        /// <summary>A flat ring (or, with a thickness near its radius, a disc).</summary>
        void AddRing(Vector3 centre, float radius, float thickness, Color colour, float alpha)
        {
            const int segments = 40;
            var start = vertices.Count;
            var c = new Color(colour.r, colour.g, colour.b, alpha);

            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add(centre + dir * radius);
                vertices.Add(centre + dir * Mathf.Max(0, radius - thickness));
                colours.Add(c);
                colours.Add(c);

                if (i > 0)
                {
                    var k = start + (i - 1) * 2;
                    triangles.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }
        }

        /// <summary>The ground's height under a point (the highest solid thing within a few metres, not the hero), or `fallback`.</summary>
        float GroundUnder(Vector3 at, float fallback)
        {
            var count = UnityEngine.Physics.RaycastNonAlloc(new Ray(at + Vector3.up * 1.2f, Vector3.down), hits, 4, ~0, QueryTriggerInteraction.Ignore);
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
