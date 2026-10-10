using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// Third-person camera: orbits behind the hero, turned by the mouse,
    /// zoomed by the wheel, and pulled in so it never ends up inside the
    /// ground or a wall. It follows softly — the eye height eases with the
    /// stance, the view widens a little when running (ported from
    /// client/render/camera.ts; the ground probe became a sphere cast).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        const float Sensitivity = 0.0024f;
        const float MinPitch = -0.45f;
        const float MaxPitch = 1.35f;
        const float MinDistance = 1.8f;
        const float MaxDistance = 14;
        /// <summary>How much wider the view is when running, degrees.</summary>
        const float RunWidening = 7;
        const float ProbeRadius = 0.2f;

        /// <summary>Radians around Y; 0 puts the camera on +Z looking toward -Z.</summary>
        public float Yaw;
        public float Pitch = 0.32f;
        /// <summary>The player's setting: how fast the mouse turns the view.</summary>
        public float SensitivityScale = 1;
        /// <summary>What the camera must not see through (everything but the hero).</summary>
        public LayerMask Obstacles = ~0;
        /// <summary>The hero's collider, ignored by the probe.</summary>
        public Collider Ignore;
        /// <summary>Behind the hero (false while something else — the hero's creation — places the camera).</summary>
        public bool Following = true;
        /// <summary>The player's setting: moving the mouse up looks down.</summary>
        public bool InvertY;
        /// <summary>The player's setting: the field of view while walking, degrees.</summary>
        public float BaseFov = 65;

        Camera view;
        float trembling;
        float wantedDistance = 5;
        float distance = 5;
        float eye = 1.55f;
        Vector3 focus;
        Vector3 direction;

        void Awake()
        {
            view = GetComponent<Camera>();
            view.fieldOfView = BaseFov;
            view.nearClipPlane = 0.1f;
        }

        /// <summary>The way the camera looks, as a body facing (0 looks along +Z).</summary>
        public float Facing => Yaw + Mathf.PI;

        /// <summary>
        /// Forward and right on the ground (X, Z), for turning input into motion.
        /// Unity's axes are left-handed: looking along +Z, right is +X — so
        /// right is forward turned a quarter clockwise seen from above.
        /// </summary>
        public (Vector2 forward, Vector2 right) GroundAxes()
        {
            var sin = Mathf.Sin(Yaw);
            var cos = Mathf.Cos(Yaw);

            return (new Vector2(-sin, -cos), new Vector2(-cos, sin));
        }

        /// <summary>A jolt (a blow taken), 0…1.</summary>
        public void Shake(float amount)
        {
            trembling = Mathf.Min(1, trembling + amount);
        }

        /// <summary>Mouse movement in pixels (Y up, as Unity reports it) and wheel notches (positive: zoom out).</summary>
        public void Look(float dx, float dy, float wheel)
        {
            // The mouse to the right turns the view to the right: clockwise seen from above, a growing yaw here.
            Yaw += dx * Sensitivity * SensitivityScale;
            Pitch = Mathf.Clamp(Pitch - (InvertY ? -dy : dy) * Sensitivity * SensitivityScale, MinPitch, MaxPitch);
            wantedDistance = Mathf.Clamp(wantedDistance * (1 + wheel * 0.12f), MinDistance, MaxDistance);
        }

        public void Follow(float dt, Vector3 target, float eyeHeight, bool running, bool snap = false)
        {
            float FollowRate(float rate) => snap ? 1 : 1 - Mathf.Exp(-rate * dt);

            eye += (eyeHeight - eye) * FollowRate(6);
            focus.x += (target.x - focus.x) * FollowRate(22);
            focus.z += (target.z - focus.z) * FollowRate(22);
            focus.y += (target.y + eye - focus.y) * FollowRate(12);

            var fov = running ? BaseFov + RunWidening : BaseFov;

            if (Mathf.Abs(view.fieldOfView - fov) > 0.01f)
            {
                view.fieldOfView += (fov - view.fieldOfView) * FollowRate(4);
            }

            var cosPitch = Mathf.Cos(Pitch);
            direction = new Vector3(Mathf.Sin(Yaw) * cosPitch, Mathf.Sin(Pitch), Mathf.Cos(Yaw) * cosPitch);

            var allowed = ClearDistance(wantedDistance);
            distance = allowed < distance || snap ? allowed : Mathf.Lerp(distance, allowed, FollowRate(5));

            transform.position = focus + direction * distance;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);

            if (trembling > 0.001f)
            {
                var jolt = trembling * trembling * 0.06f * Mathf.Rad2Deg;
                transform.rotation *= Quaternion.Euler((Random.value - 0.5f) * jolt, (Random.value - 0.5f) * jolt, 0);
                trembling *= Mathf.Exp(-7 * dt);
            }
        }

        /// <summary>How far back the camera can go before something is in the way.</summary>
        float ClearDistance(float wanted)
        {
            var hits = UnityEngine.Physics.SphereCastAll(focus, ProbeRadius, direction, wanted, Obstacles, QueryTriggerInteraction.Ignore);
            var nearest = wanted;

            foreach (var hit in hits)
            {
                if (hit.collider != Ignore && hit.distance > 0 && hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            return nearest < wanted ? Mathf.Max(0.6f, nearest - 0.1f) : wanted;
        }
    }
}
