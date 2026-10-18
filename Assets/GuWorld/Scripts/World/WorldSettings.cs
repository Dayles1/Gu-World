using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// A location's world settings (config/gu_world.php `world.locations`):
    /// the box a hero may be in and where they appear. Every distance is in
    /// metres; Y is up; yaw is radians around Y (0 looks along +Z).
    /// </summary>
    [System.Serializable]
    public struct LocationSettings
    {
        public string id;
        /// <summary>The name shown to the player.</summary>
        public string title;
        public Bounds bounds;
        public Vector3 spawn;
        public float spawnYaw;

        public static LocationSettings FromMinMax(
            string id,
            string title,
            Vector3 min,
            Vector3 max,
            Vector3 spawn,
            float spawnYaw)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);

            return new LocationSettings { id = id, title = title, bounds = bounds, spawn = spawn, spawnYaw = spawnYaw };
        }

        /// <summary>The engine's proving ground — not part of the world of Gu.</summary>
        public static LocationSettings TestGrounds => FromMinMax(
            "test_grounds",
            "Полигон (проверка движка)",
            new Vector3(-256, -20, -256),
            new Vector3(256, 120, 256),
            new Vector3(0, 0, 12),
            3.14159f);

        /// <summary>The test polygon — an immortal's aperture (a blessed land): the proving ground's field grown to a square kilometre, for trying every Gu.</summary>
        public static LocationSettings Polygon => FromMinMax(
            "polygon",
            "Благословенная земля · апертура бессмертного",
            new Vector3(-500, -20, -500),
            new Vector3(500, 160, 500),
            new Vector3(0, 0, 12),
            3.14159f);
    }
}
