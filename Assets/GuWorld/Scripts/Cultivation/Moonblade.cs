using System;
using System.Collections.Generic;
using GuWorld.Combat;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// A moonblade in flight (lore: sys_moonblade): a pale-blue crescent of
    /// condensed essence, thrown straight, about two metres from horn to horn and
    /// trailing light from its horns, so it can be seen and followed. It cuts
    /// clean within its reach (about ten metres), weaker beyond it, and fades
    /// some metres further; the first thing it meets it cuts and is spent.
    ///
    /// A person is cut where the blade meets their body (HitZones) — the head, the
    /// heart, a hand — and the part decides the wound (Wounds); a blade that
    /// misses every part passes by. It never strikes its thrower.
    /// </summary>
    public class Moonblade : MonoBehaviour
    {
        const float Speed = 30;
        /// <summary>How wide the cut is for walls and things (the blade's middle).</summary>
        const float SolidRadius = 0.22f;
        /// <summary>The crescent: its outer radius, its inner, and how far round the horns reach (radians from the front).</summary>
        public const float Outer = 1.05f;
        /// <summary>How wide the blade is from horn to horn, m (the aimed path's width).</summary>
        public static float Width => 2 * Mathf.Sin(Horns) * Outer;
        const float Inner = 0.74f;
        const float Horns = 1.32f;

        /// <summary>How far the crescent's face is tipped up towards the one who threw it, degrees: flat, it would be a mere line seen from behind.</summary>
        const float Tip = 34;

        static Mesh crescent;
        static Mesh quad;
        static Material glow;
        static Material trailGlow;
        static Material haloGlow;

        Vector3 direction;
        float left;
        float power;
        float reach;
        float flown;
        Collider thrower;
        float fading = -1;
        Material material;
        Light shine;
        TrailRenderer[] trails;
        Transform halo;
        Material haloMaterial;

        static readonly Color Bright = new Color(1.25f, 1.75f, 2.6f, 1);
        static readonly Color HaloColour = new Color(0.45f, 0.7f, 1.2f, 0.55f);

        /// <summary>The last blade thrown (the checks follow it).</summary>
        public static Moonblade Last { get; private set; }

        /// <summary>
        /// Throws a moonblade from a point along a direction: `power` its strength (100 is a
        /// rank-one Gu Master's), `roll` the crescent's tilt about its flight, degrees.
        /// </summary>
        public static Moonblade Throw(Vector3 from, Vector3 direction, GuKind kind, Collider thrower, float power = -1, float roll = 0)
        {
            var go = new GameObject("Moonblade");
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 0, roll));
            var blade = go.AddComponent<Moonblade>();
            blade.direction = direction.normalized;
            blade.left = Mathf.Max(kind.Range, kind.Reach);
            blade.reach = kind.Reach > 0 ? kind.Reach : blade.left;
            blade.power = power > 0 ? power : kind.Power;
            blade.thrower = thrower;
            // The crescent itself, its face tipped up so it shows from behind and above.
            var shape = new GameObject("Crescent").transform;
            shape.SetParent(go.transform, false);
            shape.localRotation = Quaternion.Euler(-Tip, 0, 0);
            shape.gameObject.AddComponent<MeshFilter>().sharedMesh = Crescent();
            blade.material = new Material(Glow());
            var renderer = shape.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = blade.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // A soft glow round it, always facing the camera.
            blade.halo = new GameObject("Halo").transform;
            blade.halo.SetParent(go.transform, false);
            blade.halo.localPosition = new Vector3(0, 0, Outer * 0.7f);
            blade.halo.localScale = new Vector3(Outer * 2.6f, Outer * 1.3f, 1);
            blade.halo.gameObject.AddComponent<MeshFilter>().sharedMesh = Quad();
            blade.haloMaterial = new Material(HaloGlow());
            var haloRenderer = blade.halo.gameObject.AddComponent<MeshRenderer>();
            haloRenderer.sharedMaterial = blade.haloMaterial;
            haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blade.shine = go.AddComponent<Light>();
            blade.shine.type = LightType.Point;
            blade.shine.color = new Color(0.55f, 0.78f, 1);
            blade.shine.range = 7;
            blade.shine.intensity = 2.4f;
            blade.shine.shadows = LightShadows.None;

            // Light streaming back from the horns and the middle of the edge.
            blade.trails = new[]
            {
                Trail(shape, new Vector3(Mathf.Sin(Horns) * Outer * 0.95f, 0, Mathf.Cos(Horns) * Outer * 0.95f), 0.2f),
                Trail(shape, new Vector3(-Mathf.Sin(Horns) * Outer * 0.95f, 0, Mathf.Cos(Horns) * Outer * 0.95f), 0.2f),
                Trail(shape, new Vector3(0, 0, Outer * 0.9f), 0.4f),
            };
            Last = blade;

            return blade;
        }

        static TrailRenderer Trail(Transform blade, Vector3 at, float width)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(blade, false);
            go.transform.localPosition = at;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.15f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(1, 0));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.75f, 0.9f, 1), 0), new GradientColorKey(new Color(0.4f, 0.65f, 1), 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0, 1) },
            };
            trail.sharedMaterial = TrailGlow();
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            return trail;
        }

        void Update() => Step(Time.deltaTime);

        void LateUpdate()
        {
            var camera = Camera.main;

            if (halo != null && camera != null)
            {
                halo.rotation = camera.transform.rotation;
            }
        }

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
                fading += dt / 0.2f;
                var a = Mathf.Clamp01(1 - fading);
                material.color = Bright * a;
                haloMaterial.color = HaloColour * a;
                shine.intensity = 2.4f * a;
                transform.localScale = Vector3.one * (1 + fading * 0.5f);

                if (fading >= 1)
                {
                    Destroy(material);
                    Destroy(haloMaterial);
                    Destroy(gameObject);
                }

                return;
            }

            var step = Mathf.Min(Speed * dt, left);

            if (Hit(step, out var point, out var distance, out var zone, out var solid))
            {
                transform.position = point;
                var strike = new Strike
                {
                    Power = power,
                    // Past its reach the blade has lost its edge: half the cut.
                    Sharpness = flown + distance <= reach ? 1 : 0.5f,
                    Point = point,
                    Direction = direction,
                    Source = thrower != null ? thrower.gameObject : null,
                };

                if (zone != null)
                {
                    zone.Owner?.TakeStrike(strike, zone.Part);
                }
                else if (solid != null && solid.GetComponentInParent<IStrikable>() is IStrikable target)
                {
                    target.TakeStrike(strike, null);
                }

                Spend();

                return;
            }

            transform.position += direction * step;
            left -= step;
            flown += step;

            if (left <= 0.001f)
            {
                Spend();
            }
        }

        void Spend()
        {
            fading = 0;

            foreach (var trail in trails)
            {
                if (trail != null)
                {
                    trail.emitting = false;
                }
            }
        }

        bool Mine(Collider collider) =>
            thrower != null && (collider == thrower || collider.transform.IsChildOf(thrower.transform));

        /// <summary>
        /// The first thing along the next step: a part of a body the crescent's edge meets
        /// (across its whole width), or a solid thing its middle meets — whichever is nearer.
        /// A person's own capsule does not count: only their parts do.
        /// </summary>
        bool Hit(float step, out Vector3 point, out float distance, out HitZone zone, out Collider solid)
        {
            point = default;
            distance = float.PositiveInfinity;
            zone = null;
            solid = null;

            foreach (var hit in UnityEngine.Physics.SphereCastAll(transform.position, SolidRadius, direction, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mine(hit.collider) || hit.collider.GetComponentInParent<IZoned>() != null)
                {
                    continue;
                }

                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    point = hit.distance <= 0 && hit.point == Vector3.zero ? transform.position : hit.point;
                    solid = hit.collider;
                }
            }

            // The parts of bodies: the crescent's whole edge, a thin box across the flight.
            var met = new List<(HitZone, float)>();
            var half = new Vector3(Mathf.Sin(Horns) * Outer, 0.06f, 0.05f);

            foreach (var hit in UnityEngine.Physics.BoxCastAll(transform.position + direction * (Outer * 0.6f), half, direction, transform.rotation, step, ~0, QueryTriggerInteraction.Collide))
            {
                if (!hit.collider.isTrigger || Mine(hit.collider) || !hit.collider.TryGetComponent<HitZone>(out var part))
                {
                    continue;
                }

                met.Add((part, hit.distance));
            }

            var chosen = HitZones.Choose(met);

            if (chosen != null)
            {
                var at = float.PositiveInfinity;

                foreach (var (part, d) in met)
                {
                    if (part == chosen)
                    {
                        at = Mathf.Min(at, d);
                    }
                }

                if (at <= distance)
                {
                    distance = at;
                    var centre = chosen.Shape.bounds.center;
                    point = chosen.Shape.ClosestPoint(transform.position + direction * at);
                    point = float.IsFinite(point.x) ? point : centre;
                    zone = chosen;
                    solid = null;
                }
            }

            return zone != null || solid != null;
        }

        /// <summary>A flat crescent, lying across the flight (+Z forward), its round edge leading, bright at the edge.</summary>
        static Mesh Crescent()
        {
            if (crescent != null)
            {
                return crescent;
            }

            const int segments = 32;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                // From one horn round the front to the other; thickest in the middle.
                var t = (float)i / segments;
                var a = Mathf.Lerp(-Horns, Horns, t);
                var thickness = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * t)), 0.7f);
                var front = new Vector3(Mathf.Sin(a) * Outer, 0, Mathf.Cos(a) * Outer);
                var back = new Vector3(Mathf.Sin(a) * Inner, 0, Mathf.Cos(a) * Inner - 0.12f);
                var edge = Vector3.Lerp(front, back, thickness);
                var middle = Vector3.Lerp(front, edge, 0.35f) + Vector3.up * 0.015f * thickness;
                vertices.Add(front);
                vertices.Add(middle);
                vertices.Add(edge);
                colors.Add(new Color(1, 1, 1, Mathf.Max(0.25f, thickness)));
                colors.Add(new Color(1, 1, 1, 0.85f * thickness));
                colors.Add(new Color(1, 1, 1, 0.12f * thickness));
                uvs.Add(new Vector2(t, 1));
                uvs.Add(new Vector2(t, 0.6f));
                uvs.Add(new Vector2(t, 0));

                if (i < segments)
                {
                    var k = i * 3;
                    triangles.AddRange(new[] { k, k + 3, k + 1, k + 1, k + 3, k + 4, k + 1, k + 4, k + 2, k + 2, k + 4, k + 5 });
                }
            }

            // The rim: a band standing up and down along the leading edge.
            var rim = vertices.Count;

            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var a = Mathf.Lerp(-Horns, Horns, t);
                var thickness = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * t)), 0.7f);
                var front = new Vector3(Mathf.Sin(a) * Outer * 0.985f, 0, Mathf.Cos(a) * Outer * 0.985f);
                var height = 0.11f * thickness + 0.01f;
                vertices.Add(front + Vector3.up * height);
                vertices.Add(front - Vector3.up * height);
                colors.Add(new Color(1, 1, 1, 0.55f * thickness));
                colors.Add(new Color(1, 1, 1, 0.55f * thickness));
                uvs.Add(new Vector2(t, 1));
                uvs.Add(new Vector2(t, 0));

                if (i < segments)
                {
                    var k = rim + i * 2;
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

        /// <summary>The blade's glow: GuWorld/Sky, added and brighter than white (the bloom catches it).</summary>
        static Material Glow()
        {
            if (glow != null)
            {
                return glow;
            }

            glow = Additive("Moonblade");
            glow.color = Bright;
            glow.renderQueue = 3050;

            return glow;
        }

        static Mesh Quad()
        {
            if (quad != null)
            {
                return quad;
            }

            quad = new Mesh
            {
                name = "Halo",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            quad.RecalculateBounds();

            return quad;
        }

        /// <summary>The glow round the blade: a soft oval, brightest in its middle.</summary>
        static Material HaloGlow()
        {
            if (haloGlow != null)
            {
                return haloGlow;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Moonblade Halo", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f) / size, new Vector2(0.5f, 0.5f)) * 2;
                    var a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f);
                    pixels[y * size + x] = new Color(1, 1, 1, a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            haloGlow = Additive("Moonblade Halo");
            haloGlow.mainTexture = texture;
            haloGlow.color = HaloColour;
            haloGlow.renderQueue = 3048;

            return haloGlow;
        }

        static Material TrailGlow()
        {
            if (trailGlow != null)
            {
                return trailGlow;
            }

            trailGlow = Additive("Moonblade Trail");
            trailGlow.color = new Color(1.1f, 1.4f, 2, 1);
            trailGlow.renderQueue = 3049;

            return trailGlow;
        }

        static Material Additive(string name)
        {
            var shader = Shader.Find("GuWorld/Sky") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = name };

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            return material;
        }
    }

    /// <summary>
    /// A straw training dummy on a post, for trying skills on (the academy trains on
    /// such targets). Its straw body has a head, a chest with a marked heart, a belly
    /// and a crossbar for arms — a blow wounds it there as it would a person
    /// (Wounds), wobbling it; broken, it falls over and is set up again a few
    /// seconds later.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IStrikable, IZoned
    {
        public const float Toughness = 100;
        /// <summary>Its "health": a dummy holds about as much as a person.</summary>
        public const float Sturdiness = 100;

        /// <summary>A dummy was struck: where, how hard, and whether it broke.</summary>
        public static event Action<TrainingDummy, BodyPart?, float, bool> Struck;

        public float Health = Sturdiness;
        Transform body;
        float wobble;
        Vector3 wobbleAxis;
        float fallen = -1;
        readonly System.Random random = new System.Random(17);

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

            // Its parts, moving with its wobble.
            var rigid = dummy.body.gameObject.AddComponent<Rigidbody>();
            rigid.isKinematic = true;
            rigid.useGravity = false;
            Zone(dummy, BodyPart.Head, new Vector3(0, 1.9f, 0), new Vector3(0.32f, 0.34f, 0.32f), true);
            Zone(dummy, BodyPart.Heart, new Vector3(-0.07f, 1.42f, 0.1f), new Vector3(0.12f, 0.12f, 0.12f));
            Zone(dummy, BodyPart.Chest, new Vector3(0, 1.4f, 0), new Vector3(0.46f, 0.3f, 0.36f));
            Zone(dummy, BodyPart.Belly, new Vector3(0, 1.08f, 0), new Vector3(0.44f, 0.34f, 0.34f));
            Zone(dummy, BodyPart.UpperArmL, new Vector3(-0.42f, 1.45f, 0), new Vector3(0.36f, 0.1f, 0.1f));
            Zone(dummy, BodyPart.UpperArmR, new Vector3(0.42f, 1.45f, 0), new Vector3(0.36f, 0.1f, 0.1f));

            return dummy;
        }

        static void Zone(TrainingDummy dummy, BodyPart part, Vector3 at, Vector3 size, bool round = false)
        {
            var go = new GameObject($"Zone {part}");
            go.transform.SetParent(dummy.body, false);
            go.transform.localPosition = at;
            Collider shape;

            if (round)
            {
                var sphere = go.AddComponent<SphereCollider>();
                sphere.radius = size.x / 2;
                shape = sphere;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
                shape = box;
            }

            shape.isTrigger = true;
            go.layer = HitZones.Layer;
            var zone = go.AddComponent<HitZone>();
            zone.Part = part;
            zone.Owner = dummy;
            zone.Shape = shape;
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

        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (fallen >= 0)
            {
                return;
            }

            var harm = Wounds.Harm(part ?? BodyPart.Chest, strike, Sturdiness, Toughness, random);
            Health -= harm;
            wobble = Mathf.Max(wobble, Mathf.Clamp01(0.3f + harm / 40));
            wobbleAxis = Vector3.Cross(Vector3.up, strike.Direction).normalized;
            var broke = Health <= 0;

            if (broke)
            {
                fallen = 0;
            }

            Struck?.Invoke(this, part, harm, broke);
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
                    Health = Sturdiness;
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
