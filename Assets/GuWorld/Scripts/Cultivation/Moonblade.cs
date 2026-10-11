using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>Something a strike can hurt.</summary>
    public interface IDamageable
    {
        void TakeHit(float damage, Vector3 point, Vector3 direction);
    }

    /// <summary>
    /// A moonblade in flight (lore: sys_moonblade): a pale-blue crescent of
    /// condensed essence, thrown flat and straight. It cuts clean within its
    /// reach (about ten metres), weaker beyond it, and fades some metres further;
    /// the first thing it meets it cuts — hurting what can be hurt — and is
    /// spent. It never strikes its thrower.
    /// </summary>
    public class Moonblade : MonoBehaviour
    {
        const float Speed = 28;
        const float Radius = 0.22f;

        static Mesh crescent;
        static Material glow;

        Vector3 direction;
        float left;
        float damage;
        float reach;
        float flown;
        Collider thrower;
        float fading = -1;
        Material material;
        Light shine;

        /// <summary>Throws a moonblade from a point along a direction.</summary>
        public static Moonblade Throw(Vector3 from, Vector3 direction, GuKind kind, Collider thrower)
        {
            var go = new GameObject("Moonblade");
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction, Vector3.up));
            var blade = go.AddComponent<Moonblade>();
            blade.direction = direction.normalized;
            blade.left = Mathf.Max(kind.Range, kind.Reach);
            blade.reach = kind.Reach > 0 ? kind.Reach : blade.left;
            blade.damage = kind.Damage;
            blade.thrower = thrower;
            go.AddComponent<MeshFilter>().sharedMesh = Crescent();
            blade.material = new Material(Glow());
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = blade.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blade.shine = go.AddComponent<Light>();
            blade.shine.type = LightType.Point;
            blade.shine.color = new Color(0.55f, 0.78f, 1);
            blade.shine.range = 5;
            blade.shine.intensity = 1.6f;
            blade.shine.shadows = LightShadows.None;

            return blade;
        }

        void Update() => Step(Time.deltaTime);

        /// <summary>Whether it is still flying (not yet spent on a hit or at the end of its reach).</summary>
        public bool Flying => fading < 0;

        /// <summary>Flies for `dt` seconds (the frame's; a check can drive it by hand).</summary>
        public void Step(float dt)
        {
            if (dt <= 0 || this == null)
            {
                return;
            }

            if (fading >= 0)
            {
                fading += dt / 0.18f;
                var a = Mathf.Clamp01(1 - fading);
                material.color = new Color(0.62f, 0.85f, 1, a);
                shine.intensity = 1.6f * a;
                transform.localScale = Vector3.one * (1 + fading * 0.6f);

                if (fading >= 1)
                {
                    Destroy(material);
                    Destroy(gameObject);
                }

                return;
            }

            var step = Mathf.Min(Speed * dt, left);

            if (Hit(step, out var hit))
            {
                transform.position = hit.point;

                if (hit.collider.GetComponentInParent<IDamageable>() is IDamageable target)
                {
                    // Past its reach the blade has lost its edge: half the cut.
                    var strength = flown + hit.distance <= reach ? 1 : 0.5f;
                    target.TakeHit(damage * strength, hit.point, direction);
                }

                fading = 0;

                return;
            }

            transform.position += direction * step;
            left -= step;
            flown += step;

            if (left <= 0.001f)
            {
                fading = 0;
            }
        }

        /// <summary>The nearest solid thing along the next step (not the thrower, not triggers).</summary>
        bool Hit(float step, out RaycastHit nearest)
        {
            nearest = default;
            var found = false;

            foreach (var hit in UnityEngine.Physics.SphereCastAll(transform.position, Radius, direction, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == thrower || hit.collider.transform.IsChildOf(thrower.transform))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            if (found && nearest.distance <= 0 && nearest.point == Vector3.zero)
            {
                // Started inside it: the cut lands where the blade is.
                nearest.point = transform.position;
            }

            return found;
        }

        /// <summary>A flat crescent, lying across the flight (+Z forward), its round edge leading.</summary>
        static Mesh Crescent()
        {
            if (crescent != null)
            {
                return crescent;
            }

            const int segments = 20;
            const float outer = 0.42f;
            const float inner = 0.3f;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                // From one horn round the front to the other.
                var a = Mathf.Lerp(-1.25f, 1.25f, (float)i / segments);
                var thickness = Mathf.Sin(Mathf.PI * i / segments);
                var front = new Vector3(Mathf.Sin(a) * outer, 0, Mathf.Cos(a) * outer);
                var back = new Vector3(Mathf.Sin(a) * inner, 0, Mathf.Cos(a) * inner - 0.06f) ;
                var edge = Vector3.Lerp(front, back, 1 - thickness * 0.85f);
                vertices.Add(front);
                vertices.Add(edge);
                colors.Add(new Color(1, 1, 1, thickness));
                colors.Add(new Color(1, 1, 1, thickness * 0.35f));
                uvs.Add(new Vector2((float)i / segments, 1));
                uvs.Add(new Vector2((float)i / segments, 0));

                if (i < segments)
                {
                    var k = i * 2;
                    triangles.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }

            crescent = new Mesh { name = "Moonblade" };
            crescent.SetVertices(vertices);
            crescent.SetColors(colors);
            crescent.SetUVs(0, uvs);
            crescent.SetTriangles(triangles, 0);
            crescent.RecalculateBounds();

            return crescent;
        }

        /// <summary>The blade's glow: GuWorld/Sky, added (it lights what is behind it).</summary>
        static Material Glow()
        {
            if (glow != null)
            {
                return glow;
            }

            var shader = Shader.Find("GuWorld/Sky") ?? Shader.Find("Universal Render Pipeline/Unlit");
            glow = new Material(shader) { name = "Moonblade" };
            glow.color = new Color(0.62f, 0.85f, 1, 1);

            if (glow.HasProperty("_SrcBlend"))
            {
                glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            glow.renderQueue = 3050;

            return glow;
        }
    }

    /// <summary>
    /// A straw training dummy on a post, for trying skills on (the academy trains on
    /// such targets). It takes blows — wobbling at each — and, broken, falls over
    /// and is set up again a few seconds later.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        public const float Toughness = 100;

        /// <summary>A dummy was struck: how hard, and whether it broke.</summary>
        public static event Action<TrainingDummy, float, bool> Struck;

        public float Health = Toughness;
        Transform body;
        float wobble;
        Vector3 wobbleAxis;
        float fallen = -1;

        public static TrainingDummy Make(Vector3 at, float yaw, Material straw, Material wood)
        {
            var root = new GameObject("Training Dummy");
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var dummy = root.AddComponent<TrainingDummy>();
            dummy.body = new GameObject("Body").transform;
            dummy.body.SetParent(root.transform, false);

            Part(PrimitiveType.Cylinder, dummy.body, new Vector3(0, 0.8f, 0), new Vector3(0.1f, 0.8f, 0.1f), wood);
            Part(PrimitiveType.Capsule, dummy.body, new Vector3(0, 1.25f, 0), new Vector3(0.45f, 0.5f, 0.35f), straw);
            Part(PrimitiveType.Sphere, dummy.body, new Vector3(0, 1.9f, 0), new Vector3(0.3f, 0.32f, 0.3f), straw);
            Part(PrimitiveType.Cube, dummy.body, new Vector3(0, 1.45f, 0), new Vector3(1.2f, 0.09f, 0.09f), wood);

            var solid = root.AddComponent<CapsuleCollider>();
            solid.radius = 0.32f;
            solid.height = 2.1f;
            solid.center = new Vector3(0, 1.05f, 0);

            return dummy;
        }

        static void Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;

            if (material != null)
            {
                part.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        public void TakeHit(float damage, Vector3 point, Vector3 direction)
        {
            if (fallen >= 0)
            {
                return;
            }

            Health -= damage;
            wobble = 1;
            wobbleAxis = Vector3.Cross(Vector3.up, direction).normalized;
            var broke = Health <= 0;

            if (broke)
            {
                fallen = 0;
            }

            Struck?.Invoke(this, damage, broke);
        }

        void Update()
        {
            var dt = Time.deltaTime;

            if (fallen >= 0)
            {
                fallen += dt;
                // Tips over, lies there, and is set up again.
                var tip = fallen < 0.5f ? fallen / 0.5f : fallen < 6 ? 1 : Mathf.Max(0, 1 - (fallen - 6) / 0.6f);
                body.localRotation = Quaternion.AngleAxis(85 * tip * tip, transform.InverseTransformDirection(wobbleAxis));

                if (fallen > 6.6f)
                {
                    fallen = -1;
                    Health = Toughness;
                    body.localRotation = Quaternion.identity;
                }

                return;
            }

            if (wobble > 0.001f)
            {
                wobble *= Mathf.Exp(-4 * dt);
                var angle = Mathf.Sin(Time.time * 22) * 14 * wobble;
                body.localRotation = Quaternion.AngleAxis(angle, transform.InverseTransformDirection(wobbleAxis));
            }
        }
    }
}
