using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Makes the grown-up models fifteen: the face younger — a little bigger
    /// eyes, a smaller nose, a shorter, narrower jaw — and the build slighter —
    /// leaner arms, a narrower waist and, for a boy, a flatter chest. The
    /// sculpting is the web's anime reshaping (client/body/anime.ts) at about
    /// half its strength, without its bigger head and longer legs (the head
    /// is grown by its bone, the height by the figure's scale).
    ///
    /// Only the vertices move — the bones stay put — so the body still bends
    /// at its joints. Every mesh of the model is reshaped together (the eyes,
    /// brows and hair with the face), each in a copy of its own.
    /// </summary>
    public static class Youth
    {
        struct Shape
        {
            /// <summary>How much bigger the eyes; shares taken off the waist's width, the nose, the jaw, the arms' and chest's thickness.</summary>
            public float eyes, waist, nose, jaw, arms, chest;
            /// <summary>Shares taken off the legs' and neck's thickness and the trunk's breadth (shoulders, back).</summary>
            public float legs, neck, trunk;
        }

        static Shape For(Gender gender) => gender == Gender.Female
            ? new Shape { eyes = 1.15f, waist = 0.05f, nose = 0.25f, jaw = 0.08f, arms = 0.2f, chest = 0.32f, legs = 0.1f, neck = 0.12f, trunk = 0.05f }
            : new Shape { eyes = 1.14f, waist = 0.05f, nose = 0.25f, jaw = 0.1f, arms = 0.32f, chest = 0.16f, legs = 0.15f, neck = 0.2f, trunk = 0.09f };

        /// <summary>How tall (m) the waist narrowing reaches above and below the waist.</summary>
        const float WaistSpread = 0.09f;
        /// <summary>Beyond this far out from the middle (m) are the arms, left as they are.</summary>
        const float TrunkHalf = 0.2f;

        static float SmoothStep(float x, float min, float max)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>Reshapes every skinned mesh of a model (in place, on copies of the meshes).</summary>
        /// <param name="frame">The head as measured before the reshaping.</param>
        /// <param name="amount">How young: 1 fifteen (the hero), 0 as the model comes (a grown-up).</param>
        public static void Reshape(SkinData body, HeadFrame frame, Gender gender, IEnumerable<SkinnedMeshRenderer> meshes, float amount = 1)
        {
            var full = For(gender);
            var shape = new Shape
            {
                eyes = 1 + (full.eyes - 1) * amount,
                waist = full.waist * amount,
                nose = full.nose * amount,
                jaw = full.jaw * amount,
                arms = full.arms * Mathf.Max(amount, 0.5f),
                chest = full.chest * Mathf.Max(amount, 0.4f),
                legs = full.legs * Mathf.Max(amount, 0.5f),
                neck = full.neck * Mathf.Max(amount, 0.5f),
                trunk = full.trunk * Mathf.Max(amount, 0.5f),
            };
            var bindposes = body.Mesh.bindposes;
            Vector3 At(string name)
            {
                var index = body.BoneIndex(name);

                return index < 0 ? Vector3.zero : bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
            }

            var waist = Vector3.Lerp(At("spine_01"), At("spine_02"), 0.5f);
            var chest = At("spine_03");
            var arms = new (Vector3 from, Vector3 along, float length)[2];

            for (var a = 0; a < 2; a++)
            {
                var side = a == 0 ? "l" : "r";
                var from = At($"upperarm_{side}");
                var along = At($"hand_{side}") - from;
                arms[a] = (from, along.normalized, along.magnitude);
            }

            // The legs (thigh, then shin) and the neck, as axes to thin round.
            (Vector3 from, Vector3 along, float length) Axis(Vector3 from, Vector3 to) =>
                (from, (to - from).normalized, (to - from).magnitude);

            var thighs = new[] { Axis(At("thigh_l"), At("calf_l")), Axis(At("thigh_r"), At("calf_r")) };
            var shins = new[] { Axis(At("calf_l"), At("foot_l")), Axis(At("calf_r"), At("foot_r")) };
            var neckAxis = Axis(At("neck_01"), At("Head"));
            var leftX = At("thigh_l").x;

            Vector3 Thin(Vector3 point, (Vector3 from, Vector3 along, float length) axis, float amount)
            {
                var t = Mathf.Clamp(Vector3.Dot(point - axis.from, axis.along), 0, axis.length);
                var on = axis.from + axis.along * t;

                return on + (point - on) * (1 - amount);
            }

            var s = frame.Scale;
            var nose = frame.Nose;
            var eyeY = frame.EyeY;
            var eyes = new[] { frame.EyeLeft, frame.EyeRight };

            Vector3 Sculpt(Vector3 point, float[] on)
            {
                float onHead = on[1], onArm = on[2], onChest = on[3];

                // Slighter limbs and neck, narrower shoulders and back: a youth, not an athlete.
                var left = point.x * leftX > 0 ? 0 : 1;

                if (on[4] > 0) point = Thin(point, thighs[left], shape.legs * on[4]);
                if (on[5] > 0) point = Thin(point, shins[left], shape.legs * on[5]);
                if (on[6] > 0) point = Thin(point, neckAxis, shape.neck * on[6]);

                if (on[7] > 0 && Mathf.Abs(point.x) < TrunkHalf + 0.1f)
                {
                    point.x *= 1 - shape.trunk * on[7];
                    point.z = chest.z + (point.z - chest.z) * (1 - shape.trunk * on[7]);
                }

                if (onHead > 0)
                {
                    foreach (var eye in eyes)
                    {
                        var reach = Mathf.Min(frame.EyeRadius * 2.3f, Mathf.Abs(eye.x) * 0.95f);
                        var grow = (shape.eyes - 1) * onHead * (1 - SmoothStep(Vector3.Distance(point, eye), frame.EyeRadius * 1.1f, reach));
                        point.x = eye.x + (point.x - eye.x) * (1 + grow);
                        point.y = eye.y + (point.y - eye.y) * (1 + grow);
                    }

                    var across = 1 - SmoothStep(Mathf.Abs(point.x), 0.008f * s, 0.022f * s);
                    var along = SmoothStep(point.y, nose.y - 0.02f * s, nose.y - 0.008f * s) *
                                (1 - SmoothStep(point.y, eyeY - 0.02f * s, eyeY - 0.005f * s));
                    var noseBase = nose.z - 0.025f * s;

                    if (point.z > noseBase)
                    {
                        point.z -= (point.z - noseBase) * shape.nose * across * along * onHead;
                    }

                    if (point.y < nose.y)
                    {
                        var jaw = shape.jaw * onHead * SmoothStep(nose.y - point.y, 0, 0.03f * s) *
                                  SmoothStep(point.z, frame.Centre.z - 0.03f * s, frame.Centre.z + 0.02f * s);
                        point.y = nose.y - (nose.y - point.y) * (1 - jaw);
                        point.x *= 1 - jaw;
                    }
                }

                if (onArm > 0)
                {
                    var arm = arms[0].from.x * point.x > 0 ? arms[0] : arms[1];
                    var t = Mathf.Clamp(Vector3.Dot(point - arm.from, arm.along), 0, arm.length);
                    var axis = arm.from + arm.along * t;
                    point = axis + (point - axis) * (1 - shape.arms * onArm);
                }

                if (onChest > 0 && point.z > chest.z)
                {
                    point.z = chest.z + (point.z - chest.z) * (1 - shape.chest * onChest);
                }

                // The waist, narrower.
                var narrow = shape.waist * Mathf.Exp(-Mathf.Pow((point.y - waist.y) / WaistSpread, 2)) *
                             (1 - SmoothStep(Mathf.Abs(point.x), TrunkHalf, TrunkHalf + 0.08f));
                point.x *= 1 - narrow;
                point.z = waist.z + (point.z - waist.z) * (1 - narrow);

                return point;
            }

            foreach (var renderer in meshes)
            {
                var source = renderer.sharedMesh;

                if (source == null || !source.isReadable)
                {
                    continue;
                }

                var mesh = Object.Instantiate(source);
                mesh.name = source.name + " (fifteen)";
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var bones = renderer.bones;
                var category = new int[bones.Length];

                for (var b = 0; b < bones.Length; b++)
                {
                    var name = bones[b] != null ? bones[b].name : "";
                    var underHead = false;

                    for (var up = bones[b]; up != null; up = up.parent)
                    {
                        if (up.name == "Head")
                        {
                            underHead = true;

                            break;
                        }
                    }

                    category[b] = underHead ? 1
                        : name.StartsWith("upperarm") || name.StartsWith("lowerarm") ? 2
                        : name == "spine_03" ? 3
                        : name.StartsWith("thigh") ? 4
                        : name.StartsWith("calf") ? 5
                        : name == "neck_01" ? 6
                        : name.StartsWith("clavicle") || name == "spine_02" ? 7
                        : 0;
                }

                for (var i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    var shares = new float[8];
                    shares[category[w.boneIndex0]] += w.weight0;
                    shares[category[w.boneIndex1]] += w.weight1;
                    shares[category[w.boneIndex2]] += w.weight2;
                    shares[category[w.boneIndex3]] += w.weight3;
                    // The chest's flattening also takes the trunk's share (spine_03 is both).
                    shares[7] += shares[3];
                    vertices[i] = Sculpt(vertices[i], shares);
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                renderer.sharedMesh = mesh;
                renderer.gameObject.AddComponent<GuWorld.World.OwnedMesh>().Mesh = mesh;
            }
        }
    }
}
