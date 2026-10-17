using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GuWorld.Render
{
    /// <summary>
    /// The body page's picture: the hero's own figure, standing with the arms a little
    /// out, its body's parts drawn over it in their colours (HitZones.Paint), lit and
    /// filmed by a camera of its own far under the world, into a texture the page
    /// shows. It can be turned round; a chosen part shows brighter than the rest.
    /// One at a time: opening it again keeps the one there is.
    /// </summary>
    public class BodyStudio
    {
        static readonly Vector3 Place = new Vector3(0, -3000, 0);
        static BodyStudio current;

        public RenderTexture Texture { get; private set; }
        public float Yaw;

        GameObject root;
        Transform turntable;
        Camera camera;
        PersonFigure figure;
        Dictionary<BodyPart, Renderer> shown;
        BodyPart? chosen;
        Look look;

        public static BodyStudio Open(HumanBody.Parts parts, Look look)
        {
            if (current != null && current.look != null && current.look.Gender == look.Gender && current.look.Hair == look.Hair && current.look.RobeColour == look.RobeColour)
            {
                return current;
            }

            Close();
            current = new BodyStudio();
            current.Build(parts, look);

            return current;
        }

        public static void Close()
        {
            if (current == null)
            {
                return;
            }

            if (current.root != null)
            {
                Object.Destroy(current.root);
            }

            if (current.Texture != null)
            {
                current.Texture.Release();
                Object.Destroy(current.Texture);
            }

            current = null;
        }

        void Build(HumanBody.Parts parts, Look look)
        {
            this.look = look;
            root = new GameObject("Body Studio");
            root.transform.position = Place;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);

            var height = HeroView.HeightOf(look);
            figure = new PersonFigure(turntable, parts, look, HumanBody.Build.ForHero(look), height);
            figure.Rig.ArmsOut = 0.42f;

            for (var i = 0; i < 4; i++)
            {
                figure.Pose(0.5f, PersonFigure.Standing);
            }

            var outer = new Material(Shader.Find("GuWorld/Tint"));
            var inner = new Material(outer);
            inner.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            inner.renderQueue = 3100;
            shown = HitZones.Paint(figure.Rig, outer, inner);

            Texture = new RenderTexture(720, 960, 24) { name = "Body Studio", antiAliasing = 4 };
            camera = new GameObject("Studio Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0, height * 0.55f, 3.3f);
            camera.transform.LookAt(root.transform.position + Vector3.up * height * 0.52f);
            camera.fieldOfView = 34;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 12;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.05f, 0.046f, 1);
            camera.targetTexture = Texture;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;

            foreach (var (at, intensity) in new[] { (new Vector3(1.6f, 2.2f, 2.4f), 3.2f), (new Vector3(-2, 1.4f, 1.2f), 1.6f), (new Vector3(0, 2, -2.4f), 1.8f) })
            {
                var lamp = new GameObject("Studio Light").AddComponent<Light>();
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = at;
                lamp.type = LightType.Point;
                lamp.range = 8;
                lamp.intensity = intensity;
                lamp.color = new Color(1, 0.96f, 0.9f);
                lamp.shadows = LightShadows.None;
            }

            Choose(null);
        }

        /// <summary>Turns the figure to `Yaw` (degrees; 0 faces the camera).</summary>
        public void Turn(float yaw)
        {
            Yaw = yaw;

            if (turntable != null)
            {
                turntable.localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        /// <summary>Shows one part brighter, the rest faint; null shows them all alike.</summary>
        public void Choose(BodyPart? part)
        {
            chosen = part;

            foreach (var pair in shown)
            {
                var colour = Wounds.Of(pair.Key).Colour;
                var alpha = part == null ? 0.55f : Wounds.Of(pair.Key).Name == Wounds.Of(part.Value).Name ? 0.9f : 0.14f;
                pair.Value.sharedMaterial.color = new Color(colour.r, colour.g, colour.b, alpha);
            }

            Turn(Yaw);
        }
    }
}
