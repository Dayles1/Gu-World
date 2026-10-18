namespace GuWorld.Cultivation
{
    /// <summary>
    /// The weather and the hour in a fight (the game sets them every moment): rain dampens fire — its blasts weaker, its
    /// burns shorter — and feeds water, ice and thunder; the night feeds the Gu of the moon and the stars. In rain beasts
    /// and people see less far (the wolves' night sight is their own: Beast).
    /// </summary>
    public static class Battle
    {
        /// <summary>How hard it rains now (0…1), and whether it is night.</summary>
        public static float Rain;
        public static bool Night;

        static bool Fire(string line) => line == "огонь" || line == "взрывы";
        static bool Wet(string line) => line == "вода" || line == "лёд";
        static bool Moon(string line) => line == "лунная линия" || line == "звёзды";

        /// <summary>How much stronger (or weaker) a Gu's strike is in this weather and at this hour.</summary>
        public static float Scale(GuKind kind)
        {
            var line = kind?.Line ?? "";
            var scale = 1f;

            if (Fire(line))
            {
                scale *= 1 - 0.35f * Rain;
            }
            else if (Wet(line))
            {
                scale *= 1 + 0.2f * Rain;
            }
            else if (line == "гром")
            {
                scale *= 1 + 0.25f * Rain;
            }

            if (Night && Moon(line))
            {
                scale *= 1.2f;
            }

            return scale;
        }

        /// <summary>How long a burn lasts against its usual (rain puts fire out sooner).</summary>
        public static float BurnScale => 1 - 0.5f * Rain;

        /// <summary>How far beasts and people see in this weather, against a dry day.</summary>
        public static float Sight => 1 - 0.3f * Rain;

        /// <summary>What the weather and the hour do in a fight now, in a few words (none: nothing).</summary>
        public static string Effects
        {
            get
            {
                var rain = Rain > 0.5f ? "огонь −35%, вода и лёд +20%, гром +25%" : null;
                var night = Night ? "луна и звёзды +20%" : null;

                return rain != null && night != null ? $"{night} · {rain}" : rain ?? night;
            }
        }
    }
}
