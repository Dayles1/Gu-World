using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// How the mountain's other clans stand toward the hero's (lore: the Bai and the Xiong have long been the Gu Yue's rivals
    /// on Qing Mao, kept from open war by a wary peace). Each rival clan's hostility runs from 0 to 100: the Gu Masters the
    /// hero kills of theirs raise it, the days let it cool back toward where it began. From half way their posts attack a
    /// Gu Yue on sight, without a warning; from three quarters they send war parties into the woods round the Gu Yue
    /// village to hunt the hero down. The demonic path's rogues belong to no clan and hate everyone alike.
    /// </summary>
    public static class Clans
    {
        /// <summary>The rival clans, and the demonic path's rogues (no clan, always hostile).</summary>
        public static readonly string[] Rivals = { "bai", "xiong" };
        public const string Demonic = "demonic";

        /// <summary>Hostility at which a clan's posts attack on sight, and at which it sends war parties.</summary>
        public const float OnSight = 50;
        public const float War = 75;
        /// <summary>How fast hostility cools back toward where it began, points a game day.</summary>
        const float CoolsPerDay = 3;

        static readonly Dictionary<string, float> hostility = new Dictionary<string, float>();

        /// <summary>A threshold crossed (the clan, its standing now in words, whether it rose).</summary>
        public static event Action<string, string, bool> Changed;

        /// <summary>Where a clan's hostility begins (and cools back to): the Xiong are the rougher neighbours.</summary>
        public static float Start(string clan) => clan == "xiong" ? 30 : clan == "bai" ? 20 : 0;

        public static bool IsRival(string clan) => Array.IndexOf(Rivals, clan) >= 0;

        public static float Hostility(string clan) =>
            clan == Demonic ? 100 : hostility.TryGetValue(clan ?? "", out var h) ? h : Start(clan);

        /// <summary>A clan's standing in words: wary, unfriendly, hostile, at war.</summary>
        public static string Standing(string clan) => Hostility(clan) switch
        {
            < 25 => "настороженность",
            < OnSight => "неприязнь",
            < War => "вражда",
            _ => "война",
        };

        /// <summary>Whether a clan's Gu Masters attack a Gu Yue on sight (no warning first).</summary>
        public static bool AttacksOnSight(string clan) => clan == Demonic || (IsRival(clan) && Hostility(clan) >= OnSight);

        /// <summary>Whether a clan sends war parties into the Gu Yue's woods.</summary>
        public static bool SendsWarParties(string clan) => IsRival(clan) && Hostility(clan) >= War;

        /// <summary>Hostility raised (a clan's Gu Master killed) or lowered; a threshold crossed is told.</summary>
        public static void Anger(string clan, float by)
        {
            if (!IsRival(clan) || by == 0)
            {
                return;
            }

            var before = Hostility(clan);
            var after = Mathf.Clamp(before + by, 0, 100);
            hostility[clan] = after;

            foreach (var line in new[] { 25f, OnSight, War })
            {
                if (before < line && after >= line)
                {
                    Changed?.Invoke(clan, Standing(clan), true);
                }
                else if (before >= line && after < line)
                {
                    Changed?.Invoke(clan, Standing(clan), false);
                }
            }
        }

        /// <summary>Game minutes pass: each clan's hostility cools back toward where it began.</summary>
        public static void Cool(double minutes)
        {
            if (!(minutes > 0))
            {
                return;
            }

            foreach (var clan in Rivals)
            {
                var now = Hostility(clan);
                var start = Start(clan);

                if (Mathf.Abs(now - start) > 0.01f)
                {
                    var step = (float)(CoolsPerDay * minutes / (24 * 60));
                    Anger(clan, now > start ? -Mathf.Min(step, now - start) : Mathf.Min(step, start - now));
                }
            }
        }

        /// <summary>The clans' hostility for the save (in the order of Rivals).</summary>
        public static float[] Save() => Array.ConvertAll(Rivals, Hostility);

        /// <summary>From a save (null: as at the start).</summary>
        public static void Load(float[] saved)
        {
            hostility.Clear();

            for (var i = 0; saved != null && i < Rivals.Length && i < saved.Length; i++)
            {
                if (float.IsFinite(saved[i]))
                {
                    hostility[Rivals[i]] = Mathf.Clamp(saved[i], 0, 100);
                }
            }
        }
    }
}
