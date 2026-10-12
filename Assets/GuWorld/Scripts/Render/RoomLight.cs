using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// The warm light of a room one can go into (a shop, a tavern): lit only while the camera is
    /// near enough to see in — a village's every room lit at once cost a fifth of the frames.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class RoomLight : MonoBehaviour
    {
        const float Near = 22;

        Light lamp;
        float next;

        void Awake()
        {
            lamp = GetComponent<Light>();
            lamp.enabled = false;
            next = Random.value * 0.4f;
        }

        void Update()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }

            next = Time.unscaledTime + 0.35f;
            var camera = Camera.main;
            lamp.enabled = camera != null && (camera.transform.position - transform.position).sqrMagnitude < Near * Near;
        }
    }
}
