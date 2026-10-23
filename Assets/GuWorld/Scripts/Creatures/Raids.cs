using System;
using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// Who comes hunting the hero out on the mountain: a rival clan's war party, once its hostility has come to war
    /// (Clans.War), from the side its village lies on, into the woods round the Gu Yue village; and the demonic path's
    /// rogues, who prey on any lone Gu Master far from a village — more of them, and stronger, the deeper in the wilds.
    /// They come only while the hero is out of the settlements; whoever is left once the hero has gone far is gone too.
    /// </summary>
    public class Raids : MonoBehaviour
    {
        /// <summary>Seconds between looks for whether someone comes (a little by chance), and the least between two that come.</summary>
        const float LookEvery = 20;
        const float Rest = 150;
        /// <summary>Farther than this from the hero, a raider still standing goes away, m.</summary>
        const float Forgets = 380;

        Transform hero;
        HumanBody.Parts parts;
        Func<bool> allowed;
        readonly List<GuMaster> raiders = new List<GuMaster>();
        readonly System.Random random = new System.Random();
        float nextLook;
        float restUntil;
        int seed = 31;

        /// <summary>Someone has come (who, a line for the banner).</summary>
        public event Action<string, string> Came;

        /// <summary>The demonic path's camp out in the wilds (null: none): its rogues are there whenever the hero comes near.</summary>
        public Vector3? Camp;
        readonly List<GuMaster> campers = new List<GuMaster>();
        float campBackAt;

        /// <summary>The raids round the hero; `allowed`: whether anyone may come now (the game running, not the proving ground).</summary>
        public static Raids Create(Transform hero, HumanBody.Parts parts, Func<bool> allowed)
        {
            var raids = new GameObject("Raids").AddComponent<Raids>();
            raids.hero = hero;
            raids.parts = parts;
            raids.allowed = allowed;
            raids.nextLook = Time.time + 60;

            return raids;
        }

        void Update()
        {
            if (hero == null || Time.time < nextLook)
            {
                return;
            }

            nextLook = Time.time + LookEvery * (0.75f + 0.5f * (float)random.NextDouble());
            raiders.RemoveAll(r => r == null);
            Guard();

            foreach (var raider in raiders.ToArray())
            {
                if (!raider.Dead && Flat(raider.transform.position - hero.position).magnitude > Forgets)
                {
                    raiders.Remove(raider);
                    Destroy(raider.gameObject);
                }
            }

            if (Time.time < restUntil || raiders.Exists(r => !r.Dead) || (allowed != null && !allowed()))
            {
                return;
            }

            var at = new Vector2(hero.position.x, hero.position.z);

            // In a settlement (or just outside its walls) no one comes.
            if (Array.Exists(QingMao.Settlements, s => Vector2.Distance(at, s.Centre) < s.Radius + 60))
            {
                return;
            }

            var home = QingMao.Find("gu_yue");
            var fromHome = Vector2.Distance(at, home.Centre);

            // A war party: from each clan at war, now and then, in the woods within two and a half kilometres of the Gu Yue village.
            foreach (var clan in Clans.Rivals)
            {
                if (Clans.SendsWarParties(clan) && fromHome < 2500 && random.NextDouble() < 0.12 + 0.2 * (Clans.Hostility(clan) - Clans.War) / 25)
                {
                    var village = QingMao.Find(clan).Centre;
                    var toward = new Vector3(village.x - at.x, 0, village.y - at.y).normalized;
                    var hard = Clans.Hostility(clan) >= 90;
                    var kinds = hard
                        ? new[] { MasterKinds.Of(clan, 3), MasterKinds.Of(clan, 2), MasterKinds.Of(clan, 1), MasterKinds.Of(clan, 1) }
                        : new[] { MasterKinds.Of(clan, 2), MasterKinds.Of(clan, 1), MasterKinds.Of(clan, 1) };
                    Send(kinds, toward, true);
                    Came?.Invoke(MasterKinds.ClanName(clan), $"Военный отряд — {kinds.Length} Гу-мастера — вышел охотиться на гуюэвцев");

                    return;
                }
            }

            // The demonic path's rogues: away from the village, more often the farther out.
            if (fromHome > 400 && random.NextDouble() < Mathf.Lerp(0.05f, 0.2f, Mathf.InverseLerp(400, 3000, fromHome)))
            {
                var rank = fromHome > 2200 && random.NextDouble() < 0.35 ? 3 : fromHome > 1200 ? 2 : 1;
                var count = rank == 3 ? 1 : 1 + random.Next(rank == 1 ? 3 : 2);
                var kinds = new MasterKind[count];

                for (var i = 0; i < count; i++)
                {
                    kinds[i] = MasterKinds.Demon(i == 0 ? rank : Mathf.Max(1, rank - 1));
                }

                var angle = (float)random.NextDouble() * Mathf.PI * 2;
                Send(kinds, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)), false);
                Came?.Invoke("Демонический путь", count == 1 ? "Кто-то крадётся следом — демонический Гу-мастер вышел на добычу"
                    : $"Шайка демонического пути ({count}) вышла на добычу");
            }
        }

        /// <summary>
        /// Raiders sent at the hero from a way (flat): they come out of the woods 50–60 m off and make for where the hero is,
        /// a war party at once, the rogues stalking until they are seen.
        /// </summary>
        void Send(MasterKind[] kinds, Vector3 from, bool charge)
        {
            restUntil = Time.time + Rest;
            var start = hero.position + from * (48 + 14 * (float)random.NextDouble());
            var side = Vector3.Cross(Vector3.up, from);

            for (var i = 0; i < kinds.Length; i++)
            {
                var at = start + side * ((i - (kinds.Length - 1) / 2f) * 3);

                if (UnityEngine.Physics.Raycast(new Vector3(at.x, at.y + 80, at.z), Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore))
                {
                    at = hit.point + Vector3.up * 0.05f;
                }
                else
                {
                    at.y = QingMao.GroundAt(at) + 0.05f;
                }

                var raider = GuMaster.Make(at, kinds[i], hero, parts, seed++ * 7919, true);
                // They go where the hero was; on the way, they fight on sight.
                raider.Post = hero.position;
                raider.Land = hero.position;
                raiders.Add(raider);

                if (charge)
                {
                    raider.Attack();
                }
            }
        }

        /// <summary>
        /// The camp's rogues: three of them (a leader among them now and then) round the fire while the hero is within
        /// 160 m; gone, unseen, when the hero is far; all killed, the camp is empty for ten minutes.
        /// </summary>
        void Guard()
        {
            if (Camp is not Vector3 camp)
            {
                return;
            }

            campers.RemoveAll(c => c == null);
            var far = Flat(hero.position - camp).magnitude;

            if (far > 260)
            {
                foreach (var camper in campers)
                {
                    if (!camper.Dead)
                    {
                        Destroy(camper.gameObject);
                    }
                }

                campers.Clear();

                return;
            }

            if (campers.Exists(c => !c.Dead))
            {
                return;
            }

            if (campers.Count > 0)
            {
                // All of them killed: the camp stands empty a while.
                campBackAt = Time.time + 600;
                campers.Clear();

                return;
            }

            if (far > 160 || Time.time < campBackAt || (allowed != null && !allowed()))
            {
                return;
            }

            var kinds = new[] { MasterKinds.Demon(random.NextDouble() < 0.3 ? 3 : 2), MasterKinds.Demon(2), MasterKinds.Demon(1) };

            for (var i = 0; i < kinds.Length; i++)
            {
                var a = i * Mathf.PI * 2 / kinds.Length + 0.4f;
                var at = camp + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4.5f;

                if (UnityEngine.Physics.Raycast(at + Vector3.up * 40, Vector3.down, out var hit, 100, ~0, QueryTriggerInteraction.Ignore))
                {
                    at = hit.point + Vector3.up * 0.05f;
                }

                var rogue = GuMaster.Make(at, kinds[i], hero, parts, seed++ * 7919, true);
                campers.Add(rogue);
            }
        }

        /// <summary>Everyone sent so far gone at once (a new game).</summary>
        public void Clear()
        {
            foreach (var raider in raiders)
            {
                if (raider != null)
                {
                    Destroy(raider.gameObject);
                }
            }

            raiders.Clear();

            foreach (var camper in campers)
            {
                if (camper != null)
                {
                    Destroy(camper.gameObject);
                }
            }

            campers.Clear();
            campBackAt = 0;
            restUntil = Time.time + 60;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
