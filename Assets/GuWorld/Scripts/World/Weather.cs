using UnityEngine;

namespace GuWorld.World
{
    public enum Sky
    {
        Clear,
        Cloudy,
        Rain,
    }

    /// <summary>
    /// The weather: how much of the sky is cloud (0…1) and how hard it rains
    /// (0…1). It follows from the game's minute alone — each three-hour stretch
    /// of the world has its own sky, picked the same way every time — so a
    /// loaded game has the weather it was saved with, and no state needs saving.
    /// One stretch eases into the next over its first forty minutes.
    ///
    /// Spring on Qing Mao Mountain, in the atlas' Southern Border (a humid,
    /// subtropical land of long rains and misty gorges): fair about half the
    /// time, often clouded, rain fairly often (more likely at night).
    /// </summary>
    public struct Weather
    {
        const double Stretch = 180;
        const double Change = 40;

        public float Cover;
        public float Rain;

        public Sky Kind => Rain > 0.5f ? Sky.Rain : Cover > 0.55f ? Sky.Cloudy : Sky.Clear;

        public static Weather Fair => new Weather { Cover = 0.25f, Rain = 0 };

        public static Weather Of(Sky sky) => sky switch
        {
            Sky.Rain => new Weather { Cover = 0.95f, Rain = 1 },
            Sky.Cloudy => new Weather { Cover = 0.72f, Rain = 0 },
            _ => Fair,
        };

        /// <summary>The weather at a game minute.</summary>
        public static Weather At(double minutes)
        {
            var stretch = (long)System.Math.Floor(minutes / Stretch);
            var into = minutes - stretch * Stretch;
            var now = Of(Pick(stretch));
            var before = Of(Pick(stretch - 1));
            var t = Mathf.Clamp01((float)(into / Change));
            t = t * t * (3 - 2 * t);

            return new Weather { Cover = Mathf.Lerp(before.Cover, now.Cover, t), Rain = Mathf.Lerp(before.Rain, now.Rain, t) };
        }

        /// <summary>The sky of one stretch, the same every time.</summary>
        static Sky Pick(long stretch)
        {
            unchecked
            {
                var h = (uint)(stretch * 2654435761L) ^ 0x9e3779b9u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h ^= h >> 13;
                var roll = (h & 0xffff) / 65536f;
                // Night stretches (21:00–06:00) rain a little more often.
                var hour = (stretch * 3 % 24 + 24) % 24;
                var night = hour >= 21 || hour < 6;
                var rain = night ? 0.3f : 0.2f;

                // An overcast land more often than not.
                return roll < rain ? Sky.Rain : roll < rain + 0.42f ? Sky.Cloudy : Sky.Clear;
            }
        }

        public static string Name(Sky sky) => sky switch
        {
            Sky.Rain => "дождь",
            Sky.Cloudy => "облачно",
            _ => "ясно",
        };
    }
}
