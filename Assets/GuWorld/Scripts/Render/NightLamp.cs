using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// A lamp lit at night (the Atmosphere lights it). It makes itself known while it is
    /// there: the villages' parts come and go as the hero moves (ChunkStreamer), and
    /// their lamps with them.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class NightLamp : MonoBehaviour
    {
        public static readonly List<Light> All = new List<Light>();

        Light lamp;

        void OnEnable()
        {
            lamp = GetComponent<Light>();
            All.Add(lamp);
        }

        void OnDisable() => All.Remove(lamp);
    }
}
