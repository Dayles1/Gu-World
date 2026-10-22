using System.Collections.Generic;
using GuWorld.Cultivation;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// A wild Gu out on the mountain (lore: Gu live wild in nature, and a Gu Master catches them — the liquor worm in the
    /// wild, the jade skin Gu in the stone forest): a small light of its colour, in its own shape, circling and bobbing
    /// over its spot. Held E beside it, the hero tries to catch it: the essence is spent at once, and it is caught (into
    /// the bag, to be refined) or slips away — the higher its rank above the hero's, the likelier. Either way the spot is
    /// empty a while, and another of its kind settles there later. The common rank one Gu live near the village; the
    /// rarer, stronger ones deeper in the mountain.
    /// </summary>
    public class WildGu : MonoBehaviour
    {
        /// <summary>The game minute now (set by the game each frame).</summary>
        public static double Now;

        public static readonly List<WildGu> All = new List<WildGu>();

        /// <summary>Seconds the catching takes (E held).</summary>
        public const float CatchTime = 1.5f;
        /// <summary>Game minutes before another settles in an emptied spot.</summary>
        public const double SettlesMinutes = 18 * 60;
        /// <summary>Its light shines only this near the camera (a light is not cheap).</summary>
        const float ShineNear = 40;

        public GuKind Kind { get; private set; }
        public double ReadyAt;

        public bool Ready => Now >= ReadyAt;

        Vector3 spot;
        Transform mote;
        Light shine;
        float phase;
        float shown = 1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>A wild Gu of a kind over a point on the ground.</summary>
        public static WildGu Make(Vector3 at, GuKind kind, int seed)
        {
            var go = new GameObject("Wild Gu " + kind.Id);
            go.transform.position = at;
            var wild = go.AddComponent<WildGu>();
            wild.Kind = kind;
            wild.spot = at;
            wild.phase = seed * 1.37f;
            wild.mote = new GameObject("Mote").transform;
            wild.mote.SetParent(go.transform, false);
            wild.mote.localScale = GuShapes.Size(kind) * 0.45f;
            wild.mote.gameObject.AddComponent<MeshFilter>().sharedMesh = GuShapes.For(kind);
            var renderer = wild.mote.gameObject.AddComponent<MeshRenderer>();
            var glow = GuSkills.Glow(kind.Colour);
            glow.color = kind.Colour * 2.4f;
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wild.shine = wild.mote.gameObject.AddComponent<Light>();
            wild.shine.type = LightType.Point;
            wild.shine.color = kind.Colour;
            wild.shine.range = 3.5f;
            wild.shine.intensity = 1.6f;
            wild.shine.shadows = LightShadows.None;

            return wild;
        }

        /// <summary>
        /// The chance a Gu Master of a rank catches it: likely for a Gu of their rank or below, less for every rank it
        /// stands above them.
        /// </summary>
        public float ChanceFor(int masterRank) => Mathf.Clamp(0.85f - 0.25f * (Kind.Rank - masterRank), 0.1f, 0.95f);

        /// <summary>The essence it takes to catch (% of the aperture), for a Gu Master of a rank and density.</summary>
        public float CostFor(Aperture aperture) => aperture.CostOf(10, Kind.Rank);

        /// <summary>The try made: its spot is empty a while whether it was caught or slipped away.</summary>
        public void Gone() => ReadyAt = Now + SettlesMinutes;

        void Update()
        {
            var target = Ready ? 1f : 0;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 2);
            var visible = shown > 0.01f;

            if (mote.gameObject.activeSelf != visible)
            {
                mote.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            // Circling its spot a little above the grass, bobbing, turning; gone (when caught or fled) it rises and fades.
            var t = Time.time * 0.9f + phase;
            var lift = (1 - shown) * 3;
            mote.position = spot + new Vector3(Mathf.Cos(t) * 0.7f, 0.9f + Mathf.Sin(t * 2.3f) * 0.18f + lift, Mathf.Sin(t) * 0.7f);
            mote.rotation = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 30, t * 90, 0);
            mote.localScale = GuShapes.Size(Kind) * 0.45f * shown;
            var camera = Camera.main;
            shine.enabled = camera != null && (camera.transform.position - mote.position).sqrMagnitude < ShineNear * ShineNear;
        }

        /// <summary>Where the light is now (the prompt and the catch look at it).</summary>
        public Vector3 Where => mote.position;
    }
}
