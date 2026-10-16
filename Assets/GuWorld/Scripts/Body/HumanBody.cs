using System.Collections.Generic;
using GuWorld.Engine;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// The realistic body: a skinned, textured human — Quaternius' Universal
    /// Base Characters (CC0) — worn over the Mannequin's joints (ported from
    /// client/body/human.ts). The hero's look tints the hair.
    ///
    /// The Mannequin keeps doing all the moving on its own (invisible) joints;
    /// every frame each bone copies the turn of the joint that drives it.
    /// Rotations are compared in the figure's own space, so the bones' local
    /// axes don't matter: at load the model is brought into the Mannequin's
    /// rest pose (arms and legs hanging straight down), and from then on a
    /// bone's orientation is its joint's orientation times that rest one.
    ///
    /// Some bones take only a share of their joint's turn, which the real body
    /// spreads out: the bend of the back over three vertebrae, a turn of the
    /// head partly in the neck, a raised arm partly in the collarbone. The
    /// fingers rest loosely curled.
    /// </summary>
    public class HumanBody
    {
        /// <summary>Bone → the joint that turns it, and the share of the joint's own turn it takes.</summary>
        static readonly Dictionary<string, (string joint, float share)> Drivers = new Dictionary<string, (string, float)>
        {
            ["pelvis"] = ("hips", 1),
            ["spine_01"] = ("spine", 0.35f),
            ["spine_02"] = ("spine", 0.7f),
            ["spine_03"] = ("spine", 1),
            ["neck_01"] = ("head", 0.45f),
            ["Head"] = ("head", 1),
            ["clavicle_l"] = ("shoulderL", 0.2f),
            ["upperarm_l"] = ("shoulderL", 1),
            ["lowerarm_l"] = ("elbowL", 1),
            ["hand_l"] = ("elbowL", 1),
            ["clavicle_r"] = ("shoulderR", 0.2f),
            ["upperarm_r"] = ("shoulderR", 1),
            ["lowerarm_r"] = ("elbowR", 1),
            ["hand_r"] = ("elbowR", 1),
            ["thigh_l"] = ("hipL", 1),
            ["calf_l"] = ("kneeL", 1),
            ["foot_l"] = ("ankleL", 1),
            ["thigh_r"] = ("hipR", 1),
            ["calf_r"] = ("kneeR", 1),
            ["foot_r"] = ("ankleR", 1),
        };

        /// <summary>Arms hang this far out from straight down (radians) at rest.</summary>
        const float ArmOut = 0.1f;
        /// <summary>Finger curl per joint (radians), relaxed.</summary>
        const float CurlRest = 0.3f;
        static readonly string[] Fingers = { "index", "middle", "ring", "pinky" };
        /// <summary>How much of the curl each knuckle takes, from the palm out.</summary>
        static readonly float[] Knuckles = { 0.8f, 1, 0.75f };

        class Driven
        {
            public Transform bone;
            public Transform joint;
            public float share;
            /// <summary>Orientation at rest, in figure space.</summary>
            public Quaternion rest;
            public Driven parent;
            public Quaternion target;
        }

        class Finger
        {
            public Transform bone;
            public Quaternion rest;
            /// <summary>The curl axis in the bone's own space, and its share of the curl.</summary>
            public Vector3 axis;
            public float amount;
        }

        /// <summary>Holds the model: scaled to the Mannequin and turned to face +Z.</summary>
        public readonly Transform Holder;

        readonly Transform root;
        readonly Mannequin rig;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly List<Driven> driven = new List<Driven>();
        readonly List<Finger> fingers = new List<Finger>();
        readonly Vector3 hipsDriverRest;
        /// <summary>The pelvis' parent in figure space (it never moves).</summary>
        Quaternion pelvisParent;
        Vector3 pelvisRest;
        Transform pelvis;

        /// <summary>What a body is made from: the two models (as glTFast imports them) and the materials to copy.</summary>
        public struct Parts
        {
            public GameObject male;
            public GameObject female;
            /// <summary>Opaque, two-sided URP Lit: the generated hair and the clothes are copies of it.</summary>
            public Material cloth;
            /// <summary>See-through URP Lit, for stubble.</summary>
            public Material faint;

            public GameObject Model(Gender gender) => gender == Gender.Female ? female : male;
        }

        /// <summary>How a body is grown beyond its look: its age, its hair's colour, its clothes' dye.</summary>
        public struct Build
        {
            /// <summary>1 fifteen (a younger face, a slighter build, a bigger head), 0 a grown-up as the model comes.</summary>
            public float youth;
            public Color hair;
            /// <summary>Which dye the clothes are (Clothes.For).</summary>
            public int dye;
            /// <summary>The clan's mark on the clothes.</summary>
            public Emblem emblem;

            /// <summary>The hero: fifteen, black-haired, a Gu Yue in a dark hanfu.</summary>
            public static Build Hero => ForHero(Look.Default(Gender.Male));

            /// <summary>The hero in their look's hanfu, with the clan's crescent.</summary>
            public static Build ForHero(Look look) => new Build { youth = 1, hair = Look.HairBlack, dye = look.Dye, emblem = Emblem.Crescent };

            public string Key => $"{youth:0.00}|{ColorUtility.ToHtmlStringRGB(hair)}|{dye}|{emblem}";
        }

        /// <summary>A teenager's head is a little bigger for the body than a grown-up's.</summary>
        const float TeenHead = 0.06f;

        /// <summary>Bodies built once per look and kept (hidden), copied for each who wears them.</summary>
        static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
        static Transform shelf;

        /// <summary>
        /// The model's meshes for a shape — a gender at an age — made younger and smoothed once
        /// (the costliest part of a body after the tunic's paint) and shared by every look of that
        /// shape, in the order of the model's skinned renderers (null: the model's own mesh).
        /// </summary>
        static readonly Dictionary<(Gender, float), Mesh[]> shapes = new Dictionary<(Gender, float), Mesh[]>();

        /// <summary>The cloth's weave, one texture for every body.</summary>
        static Texture2D weave;

        /// <summary>
        /// Ages are built in steps of a fifth (0, 0.2 … 1): a few shapes shared by a whole village
        /// instead of a shape for every villager, and the difference of less than a step is not seen.
        /// </summary>
        public static float YouthStep(float youth) => Mathf.Round(Mathf.Clamp01(youth) * 5) / 5;

        public HumanBody(Parts parts, Look look, Mannequin rig) : this(parts, look, Build.Hero, rig)
        {
        }

        /// <summary>
        /// Dresses the Mannequin's joints in the body for a look: a copy of its template
        /// (made the first time it is asked for, see Template), fitted to the joints.
        /// </summary>
        public HumanBody(Parts parts, Look look, Build build, Mannequin rig)
        {
            this.rig = rig;
            root = rig.Root;
            build.youth = YouthStep(build.youth);

            // Measured in the rest pose, with the figure upright.
            rig.ApplyRest();

            var template = Template(parts, look, build);
            using var cost = Costs.Time("body copied");
            Holder = new GameObject("Body").transform;
            Holder.SetParent(root, false);
            var instance = Object.Instantiate(template, Holder, false);
            instance.name = template.name;
            instance.SetActive(true);

            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }

            Fit(Mannequin.Hips - Mannequin.HipJoint);
            Calibrate();

            // A teenager: the head a little bigger for the body.
            if (bones.TryGetValue("Head", out var headBone))
            {
                headBone.localScale = Vector3.one * (1 + TeenHead * build.youth);
            }

            var joints = new Dictionary<string, Transform>
            {
                ["hips"] = rig.HipsJoint, ["spine"] = rig.Spine, ["head"] = rig.Head,
                ["shoulderL"] = rig.ShoulderL, ["shoulderR"] = rig.ShoulderR,
                ["elbowL"] = rig.ElbowL, ["elbowR"] = rig.ElbowR,
                ["hipL"] = rig.HipL, ["hipR"] = rig.HipR,
                ["kneeL"] = rig.KneeL, ["kneeR"] = rig.KneeR,
                ["ankleL"] = rig.AnkleL, ["ankleR"] = rig.AnkleR,
            };
            var byBone = new Dictionary<Transform, Driven>();

            // Parents before children, so each bone's parent target is ready first.
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (!Drivers.TryGetValue(t.name, out var drive))
                {
                    continue;
                }

                var entry = new Driven
                {
                    bone = t,
                    joint = joints[drive.joint],
                    share = drive.share,
                    rest = InFigure(t),
                    parent = t.parent != null && byBone.TryGetValue(t.parent, out var parent) ? parent : null,
                };
                driven.Add(entry);
                byBone[t] = entry;
            }

            pelvis = Bone("pelvis");
            pelvisParent = InFigure(pelvis.parent);
            pelvisRest = PositionInFigure(pelvis);
            hipsDriverRest = PositionInFigure(rig.HipsJoint);
            FindFingers();
        }

        /// <summary>The other model's own hair, fitted to this head.</summary>
        static void BorrowHair(GameObject donor, string name, SkinData skin, HeadFrame frame, Material material, Color colour)
        {
            SkinData theirSkin = null;
            Mesh theirEyes = null, theirBrows = null, hair = null;

            foreach (var renderer in donor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name.StartsWith("Superhero") || renderer.name.StartsWith("SuperHero")) theirSkin = new SkinData(renderer);
                else if (renderer.name == "Eyes") theirEyes = renderer.sharedMesh;
                else if (renderer.name == "Eyebrows") theirBrows = renderer.sharedMesh;
                else if (renderer.name == name)
                {
                    hair = renderer.sharedMesh;

                    // Its own textured material (its UVs are for that texture), tinted black.
                    if (renderer.sharedMaterial != null)
                    {
                        material = new Material(renderer.sharedMaterial);
                        Tint(material, colour);
                        Paint.Sharpen(material);
                    }
                }
            }

            if (theirSkin == null || hair == null)
            {
                return;
            }

            Hair.Borrow(skin, frame, HeadFrame.Measure(theirSkin, theirEyes, theirBrows), hair, material, name);
        }

        /// <summary>The mesh of the renderer of that name (the model's eyes, its brows), or null.</summary>
        static Mesh Find(SkinnedMeshRenderer[] renderers, string name)
        {
            foreach (var renderer in renderers)
            {
                if (renderer.name == name)
                {
                    return renderer.sharedMesh;
                }
            }

            return null;
        }

        static void SetSmoothness(Material material, float smoothness)
        {
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
        }

        static void Tint(Material material, Color color)
        {
            // glTFast's shader takes the colour as baseColorFactor; others as _BaseColor.
            foreach (var name in new[] { "baseColorFactor", "_BaseColor" })
            {
                if (material.HasProperty(name))
                {
                    material.SetColor(name, color);
                }
            }
        }

        /// <summary>
        /// The body for a look, built once and kept on a hidden shelf, in order: the bare
        /// body (younger by `youth`, the skin's colours richer, every texture sharpened),
        /// then the hair (the model's own, the other model's, or grown) and the beard, then
        /// the clothes, layer by layer. Its meshes and materials are shared by every copy.
        /// </summary>
        static GameObject Template(Parts parts, Look look, Build build)
        {
            var key = look.Key + "|" + build.Key;

            if (templates.TryGetValue(key, out var done) && done != null)
            {
                return done;
            }

            using var cost = Costs.Time("look built");
            var step = Costs.Time("model");

            if (shelf == null)
            {
                var go = new GameObject("Body Templates");
                go.SetActive(false);
                Object.DontDestroyOnLoad(go);
                shelf = go.transform;
            }

            var model = parts.Model(look.Gender);
            var keepHair = Hair.ModelHair(look.Hair);
            var instance = Object.Instantiate(model, shelf, false);
            instance.name = model.name;
            var hairColour = build.hair;

            SkinData skin = null;
            Mesh eyes = null;
            Mesh brows = null;
            Material hairTexture = null;
            var ownHairThere = false;

            // 1. The bare body.
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                // Skinned meshes move away from their bounds.
                renderer.updateWhenOffscreen = true;

                if (renderer.name.StartsWith("Hair"))
                {
                    if (renderer.name == keepHair)
                    {
                        ownHairThere = true;
                    }
                    else
                    {
                        renderer.gameObject.SetActive(false);

                        continue;
                    }
                }

                var materials = renderer.materials;

                foreach (var material in materials)
                {
                    Paint.Sharpen(material);

                    if (material.name.StartsWith("MI_Hair"))
                    {
                        // The hair and brows are grey, to be tinted.
                        Tint(material, hairColour);

                        if (hairTexture == null)
                        {
                            hairTexture = material;
                        }
                    }

                    if (material.name.StartsWith("MI_Superhero"))
                    {
                        // The muscles painted into the skin's relief: gone.
                        foreach (var name in new[] { "normalTexture_scale", "_BumpScale", "normalScale" })
                        {
                            if (material.HasProperty(name))
                            {
                                material.SetFloat(name, 0);
                            }
                        }

                        // …and into its colour, as light and shade: evened, fair.
                        foreach (var name in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                        {
                            if (material.HasProperty(name) && material.GetTexture(name) != null)
                            {
                                material.SetTexture(name, Paint.Saturate(material.GetTexture(name), Paint.SkinSaturation, Paint.SkinEvenness, Paint.SkinTone));
                            }
                        }
                    }
                }

                renderer.materials = materials;
            }

            step.Dispose();
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skinRenderer = System.Array.Find(renderers, r => r.name.StartsWith("Superhero") || r.name.StartsWith("SuperHero"));

            if (skinRenderer != null)
            {
                if (shapes.TryGetValue((look.Gender, build.youth), out var shaped))
                {
                    // The shape made before: its younger, smoothed meshes as they are.
                    for (var i = 0; i < renderers.Length && i < shaped.Length; i++)
                    {
                        if (shaped[i] != null)
                        {
                            renderers[i].sharedMesh = shaped[i];
                        }
                    }

                    skin = new SkinData(skinRenderer);
                }
                else
                {
                    step = Costs.Time("shape");
                    skin = new SkinData(skinRenderer);

                    // Younger: the face and the build (every mesh of the model together).
                    if (build.youth > 0.01f)
                    {
                        Youth.Reshape(skin, HeadFrame.Measure(skin, Find(renderers, "Eyes"), Find(renderers, "Eyebrows")), look.Gender, renderers, build.youth);
                        skin = new SkinData(skinRenderer);
                    }

                    // No bodybuilder's muscles, young or old: the skin smoothed (the clothes are cut from it next).
                    Muscles.Smooth(skinRenderer);
                    skin = new SkinData(skinRenderer);

                    // Kept for every look of this shape (the meshes reshaped here are this body's own copies).
                    shaped = new Mesh[renderers.Length];

                    for (var i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i].GetComponent<GuWorld.World.OwnedMesh>() != null)
                        {
                            shaped[i] = renderers[i].sharedMesh;
                        }
                    }

                    shapes[(look.Gender, build.youth)] = shaped;
                    step.Dispose();
                }

                eyes = Find(renderers, "Eyes");
                brows = Find(renderers, "Eyebrows");
            }

            if (skin != null)
            {
                step = Costs.Time("hair");
                var frame = HeadFrame.Measure(skin, eyes, brows);
                var hair = new Material(parts.cloth) { name = "Hair" };
                Clothes.SetColour(hair, hairColour);
                SetSmoothness(hair, 0.12f);
                var stubble = new Material(parts.faint) { name = "Stubble" };
                Clothes.SetColour(stubble, new Color(hairColour.r * 0.7f, hairColour.g * 0.7f, hairColour.b * 0.7f, 0.5f));

                // 2. The hair: the other model's own cut, when it is that one…
                if (keepHair != null && !ownHairThere)
                {
                    var other = parts.Model(look.Gender == Gender.Male ? Gender.Female : Gender.Male);
                    BorrowHair(other, keepHair, skin, frame, hairTexture != null ? hairTexture : hair, hairColour);
                }

                // …or grown, with the beard. Hanging hair takes the model's strand texture.
                Material strands = null;

                foreach (var renderer in renderers)
                {
                    if (renderer.name.StartsWith("Hair") && renderer.sharedMaterial != null)
                    {
                        strands = new Material(renderer.sharedMaterial) { name = "Hair strands" };
                        Tint(strands, hairColour);
                        Paint.Sharpen(strands);

                        break;
                    }
                }

                Hair.Grow(skin, frame, look, hair, stubble, strands);
                step.Dispose();

                // 3. The clothes, from the skin out.
                step = Costs.Time("clothes");
                var cloth = new Material(parts.cloth) { name = "Cloth" };

                if (weave == null)
                {
                    weave = Paint.Weave();
                }

                foreach (var name in new[] { "_BaseMap", "_MainTex" })
                {
                    if (cloth.HasProperty(name))
                    {
                        cloth.SetTexture(name, weave);
                        cloth.SetTextureScale(name, new Vector2(24, 24));
                    }
                }

                SetSmoothness(cloth, 0.12f);
                Clothes.Dress(skin, frame, look.Gender, cloth, build.dye, build.emblem);
                step.Dispose();
            }

            // 4. Drawn as the art direction asks: cel-shaded, outlined.
            step = Costs.Time("materials");
            Toon.Apply(instance);

            // Its meshes are shared by every copy and outlive any one of them.
            foreach (var owned in instance.GetComponentsInChildren<GuWorld.World.OwnedMesh>(true))
            {
                Object.DestroyImmediate(owned);
            }

            // 4. The look: cel shading and an ink outline (Anime), once everything is on.
            Anime.Apply(instance);
            step.Dispose();

            templates[key] = instance;

            return instance;
        }

        /// <summary>Copies the joints' turns onto the bones. Call after the joints move.</summary>
        public void Update()
        {
            var rootInverse = Quaternion.Inverse(root.rotation);

            foreach (var entry in driven)
            {
                Quaternion target;

                if (entry.share < 1)
                {
                    // The joint's parent turned, plus a share of the joint's own turn.
                    target = rootInverse * entry.joint.parent.rotation *
                             Quaternion.Slerp(Quaternion.identity, entry.joint.localRotation, entry.share);
                }
                else
                {
                    target = rootInverse * entry.joint.rotation;
                }

                target *= entry.rest;
                entry.target = target;

                var parentTarget = entry.parent != null ? entry.parent.target : pelvisParent;
                entry.bone.localRotation = Quaternion.Inverse(parentTarget) * target;
            }

            foreach (var finger in fingers)
            {
                finger.bone.localRotation = finger.rest * Quaternion.AngleAxis(CurlRest * finger.amount * Mathf.Rad2Deg, finger.axis);
            }

            // The hips also carry the body's shift (crouching, lying).
            var shift = PositionInFigure(rig.HipsJoint) - hipsDriverRest + pelvisRest;
            pelvis.position = root.TransformPoint(shift);
        }

        Transform Bone(string name)
        {
            if (!bones.TryGetValue(name, out var bone))
            {
                throw new KeyNotFoundException($"The body has no {name} bone");
            }

            return bone;
        }

        Quaternion InFigure(Transform t) => Quaternion.Inverse(root.rotation) * t.rotation;

        Vector3 PositionInFigure(Transform t) => root.InverseTransformPoint(t.position);

        /// <summary>Scales the model so its hip joints are the Mannequin's, feet on the ground, facing +Z.</summary>
        void Fit(float hipJoint)
        {
            Vector3 At(string name) => PositionInFigure(Bone(name));

            // The figure's left is -X here (the web's +X, mirrored).
            if (At("upperarm_l").x > At("upperarm_r").x)
            {
                Holder.localRotation = Quaternion.Euler(0, 180, 0);
            }

            float Sole() => Mathf.Min(At("ball_leaf_l").y, At("ball_leaf_r").y);
            var hips = (At("thigh_l").y + At("thigh_r").y) / 2;
            var scale = hipJoint / Mathf.Max(0.1f, hips - Sole());

            Holder.localScale = Vector3.one * scale;

            var centre = At("pelvis");
            Holder.localPosition = new Vector3(-centre.x, -Sole(), -centre.z);
        }

        /// <summary>Hangs the arms and legs straight down: the Mannequin's rest pose.</summary>
        void Calibrate()
        {
            // Left is -X here.
            Vector3 Down(float side) => new Vector3(-side * Mathf.Sin(ArmOut), -Mathf.Cos(ArmOut), 0);
            var straight = Vector3.down;
            var aims = new (string bone, string child, Vector3 direction)[]
            {
                ("upperarm_l", "lowerarm_l", Down(1)),
                ("lowerarm_l", "hand_l", Down(1)),
                ("hand_l", "middle_01_l", Down(1)),
                ("upperarm_r", "lowerarm_r", Down(-1)),
                ("lowerarm_r", "hand_r", Down(-1)),
                ("hand_r", "middle_01_r", Down(-1)),
                ("thigh_l", "calf_l", straight),
                ("calf_l", "foot_l", straight),
                ("thigh_r", "calf_r", straight),
                ("calf_r", "foot_r", straight),
            };

            foreach (var (name, childName, direction) in aims)
            {
                if (!bones.TryGetValue(name, out var bone) || !bones.TryGetValue(childName, out var child))
                {
                    continue;
                }

                var from = root.TransformDirection(PositionInFigure(child) - PositionInFigure(bone));
                var turn = Quaternion.FromToRotation(from.normalized, root.TransformDirection(direction));
                bone.rotation = turn * bone.rotation;
            }
        }

        /// <summary>Works out, per hand, which way the fingers curl: toward the palm, the side the thumb is on.</summary>
        void FindFingers()
        {
            foreach (var s in new[] { "l", "r" })
            {
                Vector3 At(string name) => PositionInFigure(Bone(name));
                var wrist = At($"hand_{s}");
                var along = (At($"middle_01_{s}") - wrist).normalized;
                var across = At($"pinky_01_{s}") - At($"index_01_{s}");
                var palm = Vector3.Cross(along, across).normalized;

                if (Vector3.Dot(palm, At($"thumb_03_{s}") - wrist) < 0)
                {
                    palm = -palm;
                }

                var axis = Vector3.Cross(along, palm).normalized;

                void Add(string name, float amount)
                {
                    if (!bones.TryGetValue(name, out var bone))
                    {
                        return;
                    }

                    fingers.Add(new Finger
                    {
                        bone = bone,
                        rest = bone.localRotation,
                        axis = Quaternion.Inverse(InFigure(bone)) * axis,
                        amount = amount,
                    });
                }

                foreach (var finger in Fingers)
                {
                    for (var k = 0; k < Knuckles.Length; k++)
                    {
                        Add($"{finger}_0{k + 1}_{s}", Knuckles[k]);
                    }
                }

                Add($"thumb_02_{s}", 0.35f);
                Add($"thumb_03_{s}", 0.45f);
            }
        }

        public void Destroy()
        {
            if (Holder != null)
            {
                Object.Destroy(Holder.gameObject);
            }
        }
    }
}
