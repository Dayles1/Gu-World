using System.Collections.Generic;
using System.Diagnostics;

namespace GuWorld.Engine
{
    /// <summary>
    /// What the game's one-off work has cost so far, step by step, for the timed runs' report
    /// (-bench): the people's bodies (the looks built — HumanBody's templates, each made once — and
    /// the bodies copied from them), the maps drawn, the trees gathered. Each step: how many times,
    /// how long in all, the longest (a long one is a frame the player sees stop).
    /// </summary>
    public static class Costs
    {
        class Step
        {
            public int Times;
            public double Ms;
            public double Worst;
        }

        static readonly Dictionary<string, Step> steps = new Dictionary<string, Step>();
        /// <summary>The steps in the order they were first timed.</summary>
        static readonly List<string> order = new List<string>();

        /// <summary>A step being timed: `using (Costs.Time("hair")) { … }`.</summary>
        public readonly struct Span : System.IDisposable
        {
            readonly string step;
            readonly long start;

            public Span(string step)
            {
                this.step = step;
                start = Stopwatch.GetTimestamp();
            }

            /// <summary>How long since it began, ms.</summary>
            public double Ms => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

            public void Dispose() => Add(step, Ms);
        }

        public static Span Time(string step) => new Span(step);

        public static void Add(string step, double ms)
        {
            if (!steps.TryGetValue(step, out var s))
            {
                steps[step] = s = new Step();
                order.Add(step);
            }

            s.Times++;
            s.Ms += ms;
            s.Worst = System.Math.Max(s.Worst, ms);
        }

        /// <summary>One line: each step's count, total and longest (ms).</summary>
        public static string Report()
        {
            var parts = new List<string>();

            foreach (var name in order)
            {
                var s = steps[name];
                parts.Add($"{name} {s.Times}× {s.Ms:0} ms (worst {s.Worst:0.0})");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "none";
        }
    }
}
