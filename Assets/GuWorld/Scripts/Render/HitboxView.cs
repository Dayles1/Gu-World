using System.Collections.Generic;
using GuWorld.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Render
{
    /// <summary>What a collider is, for its colour in the hitbox view.</summary>
    public enum HitboxKind
    {
        Other,
        Building,
        Prop,
        Tree,
        Branch,
        Person,
        Hero,
        Trigger,
        Moving,
    }

    /// <summary>Marks a thing's colliders with what it is (the builders put it on houses, props, trees).</summary>
    public class HitboxTag : MonoBehaviour
    {
        public HitboxKind Kind;
    }

    /// <summary>
    /// The hitbox view (F4, or the setting): every collider near the camera drawn as
    /// wire lines in the colour of what it is — buildings orange, props yellow, trees
    /// green, branches light green, people cyan, the hero white, each part of a body
    /// in its own colour (as on the body page). Lines behind walls show faintly.
    /// The terrain is left out: it is everywhere.
    /// </summary>
    public class HitboxView : MonoBehaviour
    {
        /// <summary>Whether the view is on (the settings keep it).</summary>
        public static bool On;

        public static readonly (HitboxKind kind, string name, Color colour)[] Legend =
        {
            (HitboxKind.Building, "Здания и стены", new Color(1, 0.55f, 0.15f)),
            (HitboxKind.Prop, "Вещи", new Color(1, 0.88f, 0.3f)),
            (HitboxKind.Tree, "Деревья", new Color(0.3f, 0.85f, 0.35f)),
            (HitboxKind.Branch, "Ветви", new Color(0.7f, 1, 0.45f)),
            (HitboxKind.Person, "Люди", new Color(0.3f, 0.9f, 1)),
            (HitboxKind.Hero, "Герой", Color.white),
            (HitboxKind.Trigger, "Области", new Color(0.75f, 0.45f, 1)),
            (HitboxKind.Moving, "Падающее", new Color(1, 0.3f, 0.3f)),
            (HitboxKind.Other, "Прочее", new Color(0.75f, 0.75f, 0.75f)),
        };

        public Transform Around;
        public float Radius = 60;
        const int MaxVertices = 400000;

        readonly Collider[] found = new Collider[8192];
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();
        readonly List<int> indices = new List<int>();
        readonly Dictionary<Collider, Color> tints = new Dictionary<Collider, Color>();
        readonly Dictionary<Mesh, (Vector3[] points, int[] triangles)> wires = new Dictionary<Mesh, (Vector3[], int[])>();
        Mesh mesh;
        Material material;
        MeshRenderer drawn;

        public static HitboxView Create(Transform around)
        {
            var view = new GameObject("Hitbox View").AddComponent<HitboxView>();
            view.Around = around;

            return view;
        }

        void Awake()
        {
            mesh = new Mesh { name = "Hitboxes", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            var shader = Shader.Find("GuWorld/Lines");

            if (shader != null)
            {
                material = new Material(shader) { name = "Hitboxes" };
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                drawn = gameObject.AddComponent<MeshRenderer>();
                drawn.sharedMaterial = material;
                drawn.shadowCastingMode = ShadowCastingMode.Off;
                drawn.receiveShadows = false;
                drawn.enabled = false;
            }
            else
            {
                Debug.LogWarning("GU World: the GuWorld/Lines shader is missing: no hitbox view");
            }
        }

        void OnDestroy()
        {
            Destroy(mesh);

            if (material != null)
            {
                Destroy(material);
            }
        }

        public static Color ColourOf(HitboxKind kind)
        {
            foreach (var entry in Legend)
            {
                if (entry.kind == kind)
                {
                    return entry.colour;
                }
            }

            return Color.grey;
        }

        Color Tint(Collider collider)
        {
            var id = collider;

            if (tints.TryGetValue(id, out var known))
            {
                return known;
            }

            Color colour;

            if (collider.TryGetComponent<HitZone>(out var zone))
            {
                colour = Wounds.Of(zone.Part).Colour;
            }
            else if (collider is CharacterController)
            {
                colour = ColourOf(HitboxKind.Hero);
            }
            else if (collider.GetComponentInParent<HitboxTag>() is HitboxTag tag)
            {
                colour = ColourOf(tag.Kind);
            }
            else if (collider.GetComponentInParent<IZoned>() != null)
            {
                colour = ColourOf(HitboxKind.Person);
            }
            else if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
            {
                colour = ColourOf(HitboxKind.Moving);
            }
            else if (collider.isTrigger)
            {
                colour = ColourOf(HitboxKind.Trigger);
            }
            else if (collider is MeshCollider)
            {
                colour = ColourOf(HitboxKind.Building);
            }
            else
            {
                colour = ColourOf(HitboxKind.Other);
            }

            // Moving things change; the rest keep their colour.
            if (collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic)
            {
                tints[id] = colour;
            }

            return colour;
        }

        void LateUpdate()
        {
            if (drawn != null)
            {
                drawn.enabled = On && Around != null;
            }

            if (!On || material == null || Around == null)
            {
                return;
            }

            vertices.Clear();
            colours.Clear();
            indices.Clear();
            var centre = Around.position;
            var count = UnityEngine.Physics.OverlapSphereNonAlloc(centre, Radius, found, ~0, QueryTriggerInteraction.Collide);

            for (var i = 0; i < count && vertices.Count < MaxVertices; i++)
            {
                var collider = found[i];

                if (collider == null || !collider.enabled || collider is TerrainCollider)
                {
                    continue;
                }

                var colour = Tint(collider);

                switch (collider)
                {
                    case BoxCollider box:
                        Box(box.transform, box.center, box.size, colour);
                        break;
                    case SphereCollider sphere:
                        Sphere(sphere, colour);
                        break;
                    case CapsuleCollider capsule:
                        Capsule(capsule.transform, capsule.center, capsule.radius, capsule.height, capsule.direction, colour);
                        break;
                    case CharacterController controller:
                        Capsule(controller.transform, controller.center, controller.radius, controller.height, 1, colour);
                        break;
                    case MeshCollider meshCollider:
                        Wire(meshCollider, colour);
                        break;
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
            mesh.bounds = new Bounds(centre, Vector3.one * (Radius * 2 + 50));
        }

        void Line(Vector3 a, Vector3 b, Color colour)
        {
            indices.Add(vertices.Count);
            vertices.Add(a);
            colours.Add(colour);
            indices.Add(vertices.Count);
            vertices.Add(b);
            colours.Add(colour);
        }

        void Box(Transform t, Vector3 centre, Vector3 size, Color colour)
        {
            var h = size / 2;
            var p = new Vector3[8];

            for (var i = 0; i < 8; i++)
            {
                p[i] = t.TransformPoint(centre + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
            }

            foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
            {
                Line(p[a], p[b], colour);
            }
        }

        void Circle(Vector3 centre, Vector3 u, Vector3 v, float radius, Color colour, float from = 0, float to = 2 * Mathf.PI, int steps = 20)
        {
            var previous = centre + (u * Mathf.Cos(from) + v * Mathf.Sin(from)) * radius;

            for (var i = 1; i <= steps; i++)
            {
                var a = Mathf.Lerp(from, to, (float)i / steps);
                var next = centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                Line(previous, next, colour);
                previous = next;
            }
        }

        void Sphere(SphereCollider sphere, Color colour)
        {
            var t = sphere.transform;
            var scale = t.lossyScale;
            var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var centre = t.TransformPoint(sphere.center);
            Circle(centre, t.right, t.up, radius, colour);
            Circle(centre, t.right, t.forward, radius, colour);
            Circle(centre, t.up, t.forward, radius, colour);
        }

        void Capsule(Transform t, Vector3 centre, float radius, float height, int direction, Color colour)
        {
            var scale = t.lossyScale;
            var axisLocal = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
            var along = Mathf.Abs(direction == 0 ? scale.x : direction == 1 ? scale.y : scale.z);
            var across = direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            var r = radius * across;
            var half = Mathf.Max(0, height * along / 2 - r);
            var axis = t.TransformDirection(axisLocal).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            var middle = t.TransformPoint(centre);
            var top = middle + axis * half;
            var bottom = middle - axis * half;
            Circle(top, u, v, r, colour, 0, 2 * Mathf.PI, 16);
            Circle(bottom, u, v, r, colour, 0, 2 * Mathf.PI, 16);

            foreach (var side in new[] { u, -u, v, -v })
            {
                Line(top + side * r, bottom + side * r, colour);
            }

            Circle(top, u, axis, r, colour, 0, Mathf.PI, 8);
            Circle(top, v, axis, r, colour, 0, Mathf.PI, 8);
            Circle(bottom, u, -axis, r, colour, 0, Mathf.PI, 8);
            Circle(bottom, v, -axis, r, colour, 0, Mathf.PI, 8);
        }

        static long Indices(Mesh shared)
        {
            long total = 0;

            for (var i = 0; i < shared.subMeshCount; i++)
            {
                total += shared.GetIndexCount(i);
            }

            return total;
        }

        void Wire(MeshCollider collider, Color colour)
        {
            var shared = collider.sharedMesh;

            if (shared == null)
            {
                return;
            }

            if (!wires.TryGetValue(shared, out var wire))
            {
                // A big mesh is drawn as its bounds; a small one edge by edge.
                wire = shared.isReadable && Indices(shared) <= 6000 ? (shared.vertices, shared.triangles) : (null, null);
                wires[shared] = wire;
            }

            if (wire.points == null)
            {
                var b = shared.bounds;
                Box(collider.transform, b.center, b.size, colour);

                return;
            }

            var points = wire.points;
            var triangles = wire.triangles;
            var matrix = collider.transform.localToWorldMatrix;

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = matrix.MultiplyPoint3x4(points[triangles[i]]);
                var b = matrix.MultiplyPoint3x4(points[triangles[i + 1]]);
                var c = matrix.MultiplyPoint3x4(points[triangles[i + 2]]);
                Line(a, b, colour);
                Line(b, c, colour);
                Line(c, a, colour);
            }
        }
    }
}
