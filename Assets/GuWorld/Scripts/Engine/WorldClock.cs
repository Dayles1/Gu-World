using UnityEngine;

namespace GuWorld.Engine
{
    public enum DayPhase
    {
        Night,
        Dawn,
        Morning,
        Day,
        Evening,
    }

    /// <summary>
    /// The world's clock: game minutes since the world began. The day and the
    /// time of day follow from it, so it is the one number that is saved
    /// (ported from client/engine/clock.ts).
    ///
    /// A new game always starts on the first morning; a loaded game goes on
    /// from the saved minute. A saved value that is not a finite,
    /// non-negative number of minutes is repaired to a new morning rather
    /// than trusted. Time runs at `Rate` game minutes per real second (one by
    /// default: a day lasts 24 real minutes).
    /// </summary>
    public class WorldClock
    {
        public const double MinutesPerDay = 24 * 60;
        /// <summary>A new game starts on day 1 at 07:00.</summary>
        public const double NewGameMinutes = 7 * 60;
        /// <summary>The most minutes a clock holds (about 1900 game years): beyond that a save is broken.</summary>
        public const double MaxMinutes = 1e9;

        /// <summary>Where each phase starts, in hours; the last one runs past midnight.</summary>
        static readonly (double from, DayPhase phase)[] Phases =
        {
            (5, DayPhase.Dawn),
            (7, DayPhase.Morning),
            (11, DayPhase.Day),
            (17, DayPhase.Evening),
            (20, DayPhase.Night),
        };

        public double Rate { get; }

        /// <summary>Game minutes since the world began: what is saved.</summary>
        public double Minutes { get; private set; }

        WorldClock(double minutes, double rate)
        {
            Minutes = minutes;
            Rate = rate;
        }

        public static bool IsValidMinutes(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= MaxMinutes;

        /// <summary>A new game: the first morning.</summary>
        public static WorldClock NewGame(double rate = 1) => new WorldClock(NewGameMinutes, rate);

        /// <summary>A loaded game's clock; `repaired` when the saved value could not be trusted.</summary>
        public static WorldClock FromSaved(double minutes, out bool repaired, double rate = 1)
        {
            repaired = !IsValidMinutes(minutes);

            return repaired ? NewGame(rate) : new WorldClock(minutes, rate);
        }

        /// <summary>The day, counted from 1.</summary>
        public int Day => (int)System.Math.Floor(Minutes / MinutesPerDay) + 1;

        /// <summary>Minutes since midnight, 0 ≤ m &lt; 1440.</summary>
        public double MinuteOfDay => Minutes % MinutesPerDay;

        /// <summary>0 at midnight, 0.25 at 06:00, 0.5 at noon.</summary>
        public float TimeOfDay => (float)(MinuteOfDay / MinutesPerDay);

        public DayPhase Phase
        {
            get
            {
                var hours = MinuteOfDay / 60;
                var phase = DayPhase.Night;

                foreach (var (from, name) in Phases)
                {
                    if (hours >= from)
                    {
                        phase = name;
                    }
                }

                return phase;
            }
        }

        /// <summary>"07:00".</summary>
        public string Label
        {
            get
            {
                var whole = (int)System.Math.Floor(MinuteOfDay);

                return $"{whole / 60:00}:{whole % 60:00}";
            }
        }

        /// <summary>Lets `seconds` of real time pass; anything not a finite positive number is ignored.</summary>
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
            {
                return;
            }

            Minutes = System.Math.Min(MaxMinutes, Minutes + seconds * Rate);
        }

        /// <summary>For the debug keys: jump the clock by some hours.</summary>
        public void Skip(double hours)
        {
            Minutes = Mathf.Clamp((float)(Minutes + hours * 60), 0, (float)MaxMinutes);
        }
    }
}
