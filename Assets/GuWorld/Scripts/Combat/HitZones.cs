using System;
using System.Collections.Generic;
using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Combat
{
    /// <summary>
    /// Something whose body is found by its parts (HitZones): its own solid capsule
    /// does not stop a blade — a blade that misses every part passes by.
    /// </summary>
    public interface IZoned
    {
    }

    /// <summary>One part of a body as a blow finds it: a trigger on a joint of the figure.</summary>
    public class HitZone : MonoBehaviour
    {
        public BodyPart Part;
        public IStrikable Owner;
        public Collider Shape;
    }

    /// <summary>
    /// The parts of a person's body as shapes on the Mannequin's joints, so they move
    /// with every pose: the head a sphere, the neck and the limbs capsules along the
    /// bones, the trunk boxes — and inside the chest the heart (on the figure's left),
    /// the liver (on its right) and the spine (at the back). A blade that passes
    /// through several at once wounds the most vital (PartInfo.Priority).
    ///
    /// The numbers are in the figure's own units (a grown-up of 1.8 m): the figure's
    /// scale makes them fit any height. The figure's left is −X (Mannequin).
    /// </summary>
    public static class HitZones
    {
        public enum Shape
        {
            Sphere,
            Capsule,
            Box,
        }

        public struct Spec
        {
            public BodyPart Part;
            public Func<Mannequin, Transform> Joint;
            public Shape Shape;
            public Vector3 Centre;
            /// <summary>Box: its size. Sphere: x is the radius. Capsule (along the joint's Y): x the radius, y the length.</summary>
            public Vector3 Size;
        }

        public static readonly Spec[] Specs =
        {
            S(BodyPart.Head, m => m.Head, Shape.Sphere, new Vector3(0, 0.11f, 0.015f), new Vector3(0.115f, 0, 0)),
            S(BodyPart.Neck, m => m.Neck, Shape.Capsule, new Vector3(0, 0.02f, 0), new Vector3(0.058f, 0.16f, 0)),
            S(BodyPart.Heart, m => m.Spine, Shape.Box, new Vector3(-0.055f, 0.30f, 0.075f), new Vector3(0.1f, 0.11f, 0.08f)),
            S(BodyPart.Liver, m => m.Spine, Shape.Box, new Vector3(0.075f, 0.16f, 0.06f), new Vector3(0.11f, 0.08f, 0.09f)),
            S(BodyPart.Spine, m => m.Spine, Shape.Box, new Vector3(0, 0.22f, -0.095f), new Vector3(0.07f, 0.48f, 0.05f)),
            S(BodyPart.Chest, m => m.Spine, Shape.Box, new Vector3(0, 0.32f, 0), new Vector3(0.34f, 0.24f, 0.22f)),
            S(BodyPart.Belly, m => m.Spine, Shape.Box, new Vector3(0, 0.09f, 0.01f), new Vector3(0.30f, 0.2f, 0.2f)),
            S(BodyPart.Groin, m => m.HipsJoint, Shape.Box, new Vector3(0, -0.06f, 0.02f), new Vector3(0.28f, 0.12f, 0.19f)),
            S(BodyPart.UpperArmL, m => m.ShoulderL, Shape.Capsule, new Vector3(0, -0.14f, 0), new Vector3(0.05f, 0.32f, 0)),
            S(BodyPart.UpperArmR, m => m.ShoulderR, Shape.Capsule, new Vector3(0, -0.14f, 0), new Vector3(0.05f, 0.32f, 0)),
            S(BodyPart.ForearmL, m => m.ElbowL, Shape.Capsule, new Vector3(0, -0.12f, 0), new Vector3(0.042f, 0.26f, 0)),
            S(BodyPart.ForearmR, m => m.ElbowR, Shape.Capsule, new Vector3(0, -0.12f, 0), new Vector3(0.042f, 0.26f, 0)),
            S(BodyPart.HandL, m => m.ElbowL, Shape.Box, new Vector3(0, -0.32f, 0), new Vector3(0.06f, 0.15f, 0.08f)),
            S(BodyPart.HandR, m => m.ElbowR, Shape.Box, new Vector3(0, -0.32f, 0), new Vector3(0.06f, 0.15f, 0.08f)),
            S(BodyPart.ThighL, m => m.HipL, Shape.Capsule, new Vector3(0, -0.22f, 0), new Vector3(0.078f, 0.47f, 0)),
            S(BodyPart.ThighR, m => m.HipR, Shape.Capsule, new Vector3(0, -0.22f, 0), new Vector3(0.078f, 0.47f, 0)),
            S(BodyPart.ShinL, m => m.KneeL, Shape.Capsule, new Vector3(0, -0.22f, 0), new Vector3(0.056f, 0.45f, 0)),
            S(BodyPart.ShinR, m => m.KneeR, Shape.Capsule, new Vector3(0, -0.22f, 0), new Vector3(0.056f, 0.45f, 0)),
            S(BodyPart.FootL, m => m.AnkleL, Shape.Box, new Vector3(0, -0.035f, 0.055f), new Vector3(0.09f, 0.07f, 0.25f)),
            S(BodyPart.FootR, m => m.AnkleR, Shape.Box, new Vector3(0, -0.035f, 0.055f), new Vector3(0.09f, 0.07f, 0.25f)),
        };

        static Spec S(BodyPart part, Func<Mannequin, Transform> joint, Shape shape, Vector3 centre, Vector3 size) =>
            new Spec { Part = part, Joint = joint, Shape = shape, Centre = centre, Size = size };

        static int layer = -1;

        /// <summary>
        /// The parts' own layer ("Body"), which touches no other: blows find the parts by casts
        /// (Moonblade), never by the physics' trigger events, and two hundred people's parts
        /// overlapping their own capsules, the ground and the houses cost the physics most of
        /// its time each step.
        /// </summary>
        public static int Layer
        {
            get
            {
                if (layer < 0)
                {
                    layer = Mathf.Max(0, LayerMask.NameToLayer("Body"));

                    for (var other = 0; other < 32 && layer > 0; other++)
                    {
                        UnityEngine.Physics.IgnoreLayerCollision(layer, other, true);
                    }
                }

                return layer;
            }
        }

        /// <summary>
        /// Puts the body's parts on a figure's joints, for `owner` to be wounded through.
        /// The figure gets a kinematic body (unless one above it has one already), so the
        /// parts move with it as one.
        /// </summary>
        public static List<HitZone> Build(Mannequin rig, IStrikable owner)
        {
            var zones = new List<HitZone>();

            if (rig == null)
            {
                return zones;
            }

            var body = rig.Root.GetComponentInParent<Rigidbody>();

            if (body == null)
            {
                body = rig.Root.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }

            foreach (var spec in Specs)
            {
                var go = new GameObject($"Zone {spec.Part}");
                go.layer = Layer;
                go.transform.SetParent(spec.Joint(rig), false);
                var zone = go.AddComponent<HitZone>();
                zone.Part = spec.Part;
                zone.Owner = owner;
                zone.Shape = AddCollider(go, spec);
                zone.Shape.isTrigger = true;
                zones.Add(zone);
            }

            return zones;
        }

        static Collider AddCollider(GameObject go, Spec spec)
        {
            switch (spec.Shape)
            {
                case Shape.Sphere:
                {
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.center = spec.Centre;
                    sphere.radius = spec.Size.x;

                    return sphere;
                }
                case Shape.Capsule:
                {
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.center = spec.Centre;
                    capsule.radius = spec.Size.x;
                    capsule.height = spec.Size.y;
                    capsule.direction = 1;

                    return capsule;
                }
                default:
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = spec.Centre;
                    box.size = spec.Size;

                    return box;
                }
            }
        }

        /// <summary>The parts that lie inside others (the body page draws them over what hides them).</summary>
        public static bool Inner(BodyPart part) => part == BodyPart.Heart || part == BodyPart.Liver || part == BodyPart.Spine;

        /// <summary>
        /// Shows the parts on a figure as coloured, see-through shapes (the body page),
        /// each in its part's colour — the outer ones a little swollen so they wrap the
        /// body, the inner ones (`inner`'s depth test) seen through it; returns them by part.
        /// </summary>
        public static Dictionary<BodyPart, Renderer> Paint(Mannequin rig, Material outer, Material inner)
        {
            var shown = new Dictionary<BodyPart, Renderer>();

            foreach (var spec in Specs)
            {
                var type = spec.Shape switch
                {
                    Shape.Sphere => PrimitiveType.Sphere,
                    Shape.Capsule => PrimitiveType.Capsule,
                    _ => PrimitiveType.Cube,
                };
                var go = GameObject.CreatePrimitive(type);
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                go.name = $"Shown {spec.Part}";
                go.transform.SetParent(spec.Joint(rig), false);
                go.transform.localPosition = spec.Centre;
                var swell = Inner(spec.Part) ? 1 : 1.12f;
                go.transform.localScale = swell * spec.Shape switch
                {
                    Shape.Sphere => Vector3.one * spec.Size.x * 2,
                    // Unity's capsule is 2 tall and 1 across.
                    Shape.Capsule => new Vector3(spec.Size.x * 2, spec.Size.y / 2, spec.Size.x * 2),
                    _ => spec.Size,
                };
                var renderer = go.GetComponent<Renderer>();
                var material = new Material(Inner(spec.Part) ? inner : outer);
                var colour = Wounds.Of(spec.Part).Colour;
                material.color = new Color(colour.r, colour.g, colour.b, 0.6f);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                shown[spec.Part] = renderer;
            }

            return shown;
        }

        /// <summary>
        /// The part a blade wounds, of the parts it passes through: the first it meets,
        /// unless a more vital one lies just behind it (within `depth`) — a cut into the
        /// left of the chest reaches the heart.
        /// </summary>
        public static HitZone Choose(List<(HitZone zone, float distance)> met, float depth = 0.18f)
        {
            if (met.Count == 0)
            {
                return null;
            }

            var first = float.PositiveInfinity;

            foreach (var (_, distance) in met)
            {
                first = Mathf.Min(first, distance);
            }

            HitZone best = null;

            foreach (var (zone, distance) in met)
            {
                if (distance <= first + depth && (best == null || Wounds.Of(zone.Part).Priority > Wounds.Of(best.Part).Priority))
                {
                    best = zone;
                }
            }

            return best;
        }
    }
}
