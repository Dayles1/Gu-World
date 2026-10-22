using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// The wolf tide (lore: sys_wolf_tide — every few years the lightning wolves of Qing Mao swarm round Gu Yue village,
    /// wave after wave, beast kings at their head, and the clan's Gu Masters hold them off): waves of wolves come at the
    /// hero from one side and then another — grey wolves first, then lightning wolves, a bold lightning wolf with its pack,
    /// at the last a frenzy lightning wolf (a great tide: the thunder crown wolf as well). A wave is over when all but one
    /// of it are dead, or after a while; the next comes. All of them beaten, the tide is held off; the hero falling, it
    /// ebbs away.
    /// </summary>
    public class WolfTide : MonoBehaviour
    {
        /// <summary>Seconds before the next wave comes even if this one still fights.</summary>
        const float WaveFor = 100;
        /// <summary>A breath between one wave beaten and the next, s.</summary>
        const float Breather = 6;
        /// <summary>The hero gone this far from where the tide began, m: it ebbs away without them.</summary>
        const float LeftBehind = 450;

        WolfPacks packs;
        Transform hero;
        Systems.Health heroHealth;
        BeastKind[][] waves;
        /// <summary>The tide's wolves still fighting (this wave's and any left over from the last), and this wave's own.</summary>
        readonly List<Beast> current = new List<Beast>();
        readonly List<Beast> thisWave = new List<Beast>();
        /// <summary>The angle the last wave came from (radians; NaN: none yet).</summary>
        float lastAngle = float.NaN;
        int wave = -1;
        float waveAt;
        float clearedAt = -1;
        float nextLook;
        Vector3 centre;

        public bool Active => wave >= 0;
        /// <summary>Which wave it is (1…), of how many; how many of the tide's wolves still fight; which way it comes from (flat, from the hero).</summary>
        public int Wave => wave + 1;
        public int Waves => waves?.Length ?? 0;
        public int Left { get; private set; }
        public Vector3 From { get; private set; }
        /// <summary>Whether this is a great tide (the thunder crown wolf at its end).</summary>
        public bool Great { get; private set; }

        /// <summary>A wave comes (its number); the tide ends (held off, or ebbed away with the hero fallen).</summary>
        public event Action<int> WaveCame;
        public event Action<bool> Ended;

        public static WolfTide Make(WolfPacks packs, Transform hero)
        {
            var tide = new GameObject("Wolf Tide").AddComponent<WolfTide>();
            tide.packs = packs;
            tide.hero = hero;
            tide.heroHealth = hero != null ? hero.GetComponent<Systems.Health>() : null;

            return tide;
        }

        /// <summary>The tide begins (a great one with the thunder crown wolf at its end).</summary>
        public void Begin(bool great = false)
        {
            if (Active || packs == null || hero == null)
            {
                return;
            }

            var g = WolfKinds.Grey;
            var l = WolfKinds.Lightning;
            var list = new List<BeastKind[]>
            {
                new[] { g, g, g, g, g, g },
                new[] { g, g, g, g, l, l, l },
                Join(WolfPacks.PackOf(WolfKinds.Bold), new[] { g, g }),
                new[] { l, l, l, l, l, l, l },
                WolfPacks.PackOf(WolfKinds.Frenzy),
            };

            if (great)
            {
                list.Add(WolfPacks.PackOf(WolfKinds.ThunderCrown));
            }

            waves = list.ToArray();
            Great = great;
            wave = -1;
            lastAngle = float.NaN;
            current.Clear();
            centre = hero.position;
            Next();
        }

        /// <summary>The tide ends at once (its wolves stay where they are).</summary>
        public void Stop()
        {
            wave = -1;
            current.Clear();
            thisWave.Clear();
            Left = 0;
        }

        static BeastKind[] Join(BeastKind[] a, BeastKind[] b)
        {
            var all = new BeastKind[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);

            return all;
        }

        void Next()
        {
            wave++;
            clearedAt = -1;

            if (wave >= waves.Length)
            {
                wave = -1;
                Ended?.Invoke(true);

                return;
            }

            // From a side of its own: the first anywhere, each after it from well round the hero from where the last came.
            var angle = float.IsNaN(lastAngle)
                ? UnityEngine.Random.value * Mathf.PI * 2
                : lastAngle + Mathf.PI * UnityEngine.Random.Range(0.55f, 1.45f);
            lastAngle = angle;
            From = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            // The wolves left from the last wave fight on, and count on.
            thisWave.Clear();
            thisWave.AddRange(packs.CallUp(waves[wave], From));

            foreach (var wolf in thisWave)
            {
                wolf.Rouse();
            }

            current.AddRange(thisWave);
            Left = current.Count;
            waveAt = Time.time;
            WaveCame?.Invoke(Wave);
        }

        void Update()
        {
            if (!Active || Time.time < nextLook)
            {
                return;
            }

            nextLook = Time.time + 0.5f;

            // The hero fallen, or fled far from the fight: the tide ebbs away.
            if ((heroHealth != null && heroHealth.Down) || Vector3.Distance(hero.position, centre) > LeftBehind)
            {
                Stop();
                Ended?.Invoke(false);

                return;
            }

            // The dead, and any the hero has tamed since, are out of it.
            current.RemoveAll(w => w == null || w.Dead || w.Tamed);
            thisWave.RemoveAll(w => w == null || w.Dead || w.Tamed);
            Left = current.Count;

            // This wave beaten (all but a straggler) or gone on long enough: a breath, then the next. The last wave must be
            // beaten to the last wolf of the tide for it to be held off.
            var last = wave == waves.Length - 1;

            if ((last ? Left == 0 : thisWave.Count <= 1 || Time.time - waveAt > WaveFor) && clearedAt < 0)
            {
                clearedAt = Time.time;
            }

            if (clearedAt >= 0 && Time.time - clearedAt > (Left == 0 ? Breather * 0.5f : Breather))
            {
                Next();
            }
        }
    }
}
