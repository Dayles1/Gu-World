using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// A person's picture: the Mannequin's joints, posed every frame from what
    /// the person does, worn by the realistic body for their look and build —
    /// drawn at their height (the models are grown-ups of 1.8 m). The hero's
    /// (HeroView) and every villager's are one of these.
    /// </summary>
    public class PersonFigure
    {
        const float ModelHeight = 1.8f;

        public readonly Transform Root;
        public readonly Mannequin Rig;
        public readonly HumanBody Body;

        /// <summary>The figure's size against the models' (1 = 1.8 m).</summary>
        public readonly float Scale;

        public bool HasBody => Body != null;

        public PersonFigure(Transform parent, HumanBody.Parts parts, Look look, HumanBody.Build build, float height)
        {
            Root = new GameObject("Figure").transform;
            Root.SetParent(parent, false);
            Scale = height / ModelHeight;
            Root.localScale = Vector3.one * Scale;
            Rig = new Mannequin(Root);

            if (parts.Model(look.Gender) == null)
            {
                Debug.LogWarning("GU World: no body model given; the plain figure stands in");
                HeroFigure.Build(Root, null);

                return;
            }

            try
            {
                Body = new HumanBody(parts, look, build, Rig);
                // Straight into a standing pose, not easing up from the rest one.
                Pose(0.5f, Standing);
            }
            catch (System.Exception error)
            {
                Debug.LogError($"GU World: the body could not be built: {error}");
                HeroFigure.Build(Root, null);
            }
        }

        /// <summary>Standing still on flat ground.</summary>
        public static MotionState Standing => new MotionState
        {
            speed = 0,
            walkSpeed = 1.4f,
            runSpeed = 3,
            stance = Physics.Stance.Stand,
            grounded = true,
        };

        /// <summary>Poses the figure for `dt` seconds of what the person does, and dresses the body in it.</summary>
        public void Pose(float dt, MotionState motion, float landingSpeed = 0)
        {
            if (dt <= 0)
            {
                return;
            }

            Rig.Update(dt, motion, landingSpeed);
            Body?.Update();
        }

        Renderer[] drawn;
        Material[][] outlinedMaterials;
        Material[][] plainMaterials;
        bool outlined = true;

        /// <summary>The body's dark outline drawn (near) or left out (far: there it is a pixel wide and draws the whole body again).</summary>
        public void SetOutline(bool on)
        {
            if (outlined == on)
            {
                return;
            }

            outlined = on;

            if (drawn == null)
            {
                drawn = Root.GetComponentsInChildren<Renderer>(true);
                outlinedMaterials = new Material[drawn.Length][];
                plainMaterials = new Material[drawn.Length][];

                for (var i = 0; i < drawn.Length; i++)
                {
                    outlinedMaterials[i] = drawn[i].sharedMaterials;
                    plainMaterials[i] = System.Array.ConvertAll(outlinedMaterials[i], Toon.Plain);
                }
            }

            for (var i = 0; i < drawn.Length; i++)
            {
                if (drawn[i] != null)
                {
                    drawn[i].sharedMaterials = on ? outlinedMaterials[i] : plainMaterials[i];
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (Root.gameObject.activeSelf != visible)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        public void Destroy()
        {
            // Hidden at once (Destroy waits for the end of the frame).
            Root.gameObject.SetActive(false);
            Object.Destroy(Root.gameObject);
        }
    }
}
