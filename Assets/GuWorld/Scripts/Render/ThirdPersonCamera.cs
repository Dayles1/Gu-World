using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// Third-person camera: orbits behind the hero, turned by the mouse,
    /// zoomed by the wheel, and pulled in so it never ends up inside the
    /// ground or a wall. It follows softly — the eye height eases with the
    /// stance, the view widens a little when running (ported from
    /// client/render/camera.ts; the ground probe became a sphere cast).
    ///
    /// In first person (F5) it is the hero's eyes: at the head, looking where
    /// the mouse turns it, up and down further than over the shoulder. The
    /// middle of the screen is where blows go (AimPoint), in either view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        const float Sensitivity = 0.0024f;
        const float MinPitch = -0.45f;
        const float MaxPitch = 1.35f;
        /// <summary>Looking up and down from the eyes (first person), radians.</summary>
        const float EyesPitch = 1.45f;
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
        /// <summary>Seen from the hero's own eyes (F5), not from behind.</summary>
        public bool FirstPerson { get; private set; }
        /// <summary>A skill being aimed: the view closes in over the shoulder, a little narrower.</summary>
        public bool Aiming;

        /// <summary>
        /// The camera looks past the hero's right shoulder, so the middle of the screen (the crosshair)
        /// is beside them, not on their back; further aside and nearer while aiming.
        /// </summary>
        const float Shoulder = 0.42f;
        const float AimShoulder = 0.7f;
        const float AimCloser = 0.62f;
        const float AimNarrower = 8;
        float aimBlend;

        Camera view;
        float trembling;
        float wantedDistance = 5;
        float distance = 5;
        float eye = 1.55f;
        Vector3 focus;
        /// <summary>The point the camera orbits and looks at: the focus, moved past the shoulder.</summary>
        Vector3 pivot;
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
            Pitch = Mathf.Clamp(Pitch - (InvertY ? -dy : dy) * Sensitivity * SensitivityScale,
                FirstPerson ? -EyesPitch : MinPitch, FirstPerson ? EyesPitch : MaxPitch);

            // From the eyes, the wheel out goes back behind the hero.
            if (FirstPerson && wheel > 0)
            {
                SetFirstPerson(false);
            }
            else
            {
                wantedDistance = Mathf.Clamp(wantedDistance * (1 + wheel * 0.12f), MinDistance, MaxDistance);
            }
        }

        /// <summary>Into the hero's eyes or back behind them (the over-the-shoulder pitch kept within its range).</summary>
        public void SetFirstPerson(bool on)
        {
            FirstPerson = on;
            view.nearClipPlane = on ? 0.05f : 0.1f;

            if (!on)
            {
                Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
            }
        }

        /// <summary>
        /// Where the middle of the screen points: the first solid thing along it (not the hero, nor the
        /// hero's own body parts), or the point `reach` metres along it.
        /// </summary>
        public Vector3 AimPoint(float reach, Transform hero)
        {
            var ray = new Ray(transform.position, transform.forward);
            var count = UnityEngine.Physics.RaycastNonAlloc(ray, aimHits, reach + (FirstPerson ? 0 : distance), ~0, QueryTriggerInteraction.Ignore);
            var best = float.PositiveInfinity;
            var point = ray.GetPoint(reach + (FirstPerson ? 0 : distance));

            for (var i = 0; i < count; i++)
            {
                var hit = aimHits[i];

                // The stretch between the camera and the hero (over the shoulder) is not aimed at.
                if (hit.distance < best && (hero == null || !hit.collider.transform.IsChildOf(hero)) &&
                    (FirstPerson || hit.distance > distance * 0.8f))
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }

            return point;
        }

        readonly RaycastHit[] aimHits = new RaycastHit[32];

        public void Follow(float dt, Vector3 target, float eyeHeight, bool running, bool snap = false)
        {
            float FollowRate(float rate) => snap ? 1 : 1 - Mathf.Exp(-rate * dt);

            eye += (eyeHeight - eye) * FollowRate(6);
            focus.x += (target.x - focus.x) * FollowRate(22);
            focus.z += (target.z - focus.z) * FollowRate(22);
            focus.y += (target.y + eye - focus.y) * FollowRate(12);

            aimBlend = Mathf.MoveTowards(aimBlend, Aiming && !FirstPerson ? 1 : 0, (snap ? 1 : dt * 5));
            var fov = (running ? BaseFov + RunWidening : BaseFov) - AimNarrower * aimBlend;

            if (Mathf.Abs(view.fieldOfView - fov) > 0.01f)
            {
                view.fieldOfView += (fov - view.fieldOfView) * FollowRate(4);
            }

            var cosPitch = Mathf.Cos(Pitch);
            direction = new Vector3(Mathf.Sin(Yaw) * cosPitch, Mathf.Sin(Pitch), Mathf.Cos(Yaw) * cosPitch);

            if (FirstPerson)
            {
                // The eyes: at the head (no lag sideways, the head's own bob up and down), looking along the view.
                focus = new Vector3(target.x, focus.y, target.z);
                distance = 0;
                transform.position = focus - direction * 0.08f;
                transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            }
            else
            {
                // Past the right shoulder: the point looked at moved aside (not into a wall).
                var (_, right) = GroundAxes();
                var aside = new Vector3(right.x, 0, right.y);
                var shoulder = Mathf.Lerp(Shoulder, AimShoulder, aimBlend);

                if (UnityEngine.Physics.SphereCast(focus, ProbeRadius, aside, out var wall, shoulder, Obstacles, QueryTriggerInteraction.Ignore) && wall.collider != Ignore)
                {
                    shoulder = Mathf.Max(0, wall.distance - 0.05f);
                }

                pivot = focus + aside * shoulder;
                var allowed = ClearDistance(wantedDistance * Mathf.Lerp(1, AimCloser, aimBlend));
                distance = allowed < distance || snap ? allowed : Mathf.Lerp(distance, allowed, FollowRate(5));

                transform.position = pivot + direction * distance;
                transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
            }

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
            var hits = UnityEngine.Physics.SphereCastAll(pivot, ProbeRadius, direction, wanted, Obstacles, QueryTriggerInteraction.Ignore);
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
