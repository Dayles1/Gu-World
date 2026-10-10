using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Measures a body's head in its rest pose (ported from client/body/head.ts):
    /// how far it reaches, where the eyes, brows, nose tip, mouth and chin are,
    /// and how much each vertex follows the head. The hair and beards are
    /// placed by it.
    ///
    /// The model's space here is the web's mirrored: the figure's left eye is
    /// on -X. Everything that uses the frame is symmetric in X or says so.
    /// </summary>
    public class HeadFrame
    {
        /// <summary>The plain model's head, top to chin (m).</summary>
        const float HeadHeight = 0.235f;

        /// <summary>The middle of the head and how far it reaches.</summary>
        public Vector3 Centre;
        public float Top, Back, Front, HalfWidth;
        /// <summary>The eye centres and an eye's radius.</summary>
        public Vector3 EyeLeft, EyeRight;
        public float EyeRadius;
        /// <summary>Top of the eyebrows.</summary>
        public float Brow;
        public Vector3 Nose;
        public float Mouth, Chin;
        /// <summary>Size relative to the plain model's head: distances scale with it.</summary>
        public float Scale;
        /// <summary>Per body vertex: how much it follows the head.</summary>
        public float[] OnHead;

        public float EyeY => (EyeLeft.y + EyeRight.y) / 2;

        /// <summary>Measures the head of a body; `eyes` and `brows` are the model's eye and eyebrow meshes, when it has them.</summary>
        public static HeadFrame Measure(SkinData body, Mesh eyes, Mesh brows)
        {
            var head = new bool[body.BoneNames.Length];
            var bones = body.Renderer.bones;

            for (var b = 0; b < bones.Length; b++)
            {
                for (var up = bones[b]; up != null; up = up.parent)
                {
                    if (up.name == "Head")
                    {
                        head[b] = true;

                        break;
                    }
                }
            }

            var frame = new HeadFrame { OnHead = new float[body.Count] };
            var box = new Bounds();
            var boxed = false;

            for (var i = 0; i < body.Count; i++)
            {
                var w = body.Weights[i];
                frame.OnHead[i] = (head[w.boneIndex0] ? w.weight0 : 0) + (head[w.boneIndex1] ? w.weight1 : 0) +
                                  (head[w.boneIndex2] ? w.weight2 : 0) + (head[w.boneIndex3] ? w.weight3 : 0);

                if (frame.OnHead[i] > 0.9f)
                {
                    if (boxed)
                    {
                        box.Encapsulate(body.Vertices[i]);
                    }
                    else
                    {
                        box = new Bounds(body.Vertices[i], Vector3.zero);
                        boxed = true;
                    }
                }
            }

            frame.Centre = box.center;
            frame.EyeRadius = 0.012f;

            if (eyes != null && eyes.vertexCount > 0)
            {
                var points = eyes.vertices;
                Vector3 left = Vector3.zero, right = Vector3.zero;
                int lefts = 0, rights = 0;

                foreach (var p in points)
                {
                    if (p.x < 0) { left += p; lefts++; }
                    else { right += p; rights++; }
                }

                frame.EyeLeft = left / Mathf.Max(1, lefts);
                frame.EyeRight = right / Mathf.Max(1, rights);
                frame.EyeRadius = 0;

                foreach (var p in points)
                {
                    frame.EyeRadius = Mathf.Max(frame.EyeRadius, Vector3.Distance(p, p.x < 0 ? frame.EyeLeft : frame.EyeRight));
                }
            }
            else
            {
                frame.EyeLeft = new Vector3(-0.032f, frame.Centre.y, box.max.z - 0.02f);
                frame.EyeRight = new Vector3(0.032f, frame.Centre.y, box.max.z - 0.02f);
            }

            var eyeY = frame.EyeY;
            frame.Brow = eyeY + 0.022f;

            if (brows != null && brows.vertexCount > 0)
            {
                frame.Brow = float.NegativeInfinity;

                foreach (var p in brows.vertices)
                {
                    frame.Brow = Mathf.Max(frame.Brow, p.y);
                }
            }

            // The nose tip: the front-most point of the midline below the eyes.
            frame.Nose = new Vector3(0, eyeY - 0.03f, box.max.z);
            var noseZ = float.NegativeInfinity;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (frame.OnHead[i] > 0.9f && Mathf.Abs(p.x) < 0.006f && p.y < eyeY - 0.005f && p.y > eyeY - 0.08f && p.z > noseZ)
                {
                    noseZ = p.z;
                    frame.Nose = p;
                }
            }

            // The chin: the lowest point of the midline still at the front of the face.
            frame.Chin = frame.Nose.y - 0.07f;

            for (var i = 0; i < body.Count; i++)
            {
                var p = body.Vertices[i];

                if (frame.OnHead[i] > 0.5f && Mathf.Abs(p.x) < 0.006f && p.y < frame.Nose.y && p.z > frame.Nose.z - 0.05f && p.y < frame.Chin)
                {
                    frame.Chin = p.y;
                }
            }

            frame.Top = box.max.y;
            frame.Back = box.min.z;
            frame.Front = box.max.z;
            frame.HalfWidth = Mathf.Max(-box.min.x, box.max.x);
            frame.Mouth = frame.Nose.y - 0.4f * (frame.Nose.y - frame.Chin);
            frame.Scale = (box.max.y - frame.Chin) / HeadHeight;

            return frame;
        }
    }
}
