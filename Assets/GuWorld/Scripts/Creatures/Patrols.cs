using System.Collections.Generic;
using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// The other clans' Gu Masters out on the mountain: posts round the Bai and the Xiong villages, each kept by a few of
    /// the clan's fighters, who come out when the hero is near (and go, unseen, when they are far) and warn the hero off
    /// their land. A post's fighters killed come back after a while. Gu Masters can also be called up beside the hero (the
    /// proving ground, the /masters command) — and those fight on sight.
    /// </summary>
    public class Patrols : MonoBehaviour
    {
        const float ComeOut = 200;
        const float GoAway = 340;
        /// <summary>Seconds before a post's fighters killed are back.</summary>
        const float Returns = 420;

        class Post
        {
            public Vector3 At;
            public Vector3 Land;
            public float LandRadius;
            public MasterKind[] Kinds;
            public readonly List<GuMaster> Masters = new List<GuMaster>();
            public float BackAt;
            public bool Called;
        }

        readonly List<Post> posts = new List<Post>();
        Transform hero;
        HumanBody.Parts parts;
        float nextLook;
        int seed = 7;

        /// <summary>The posts round the clans' villages (centre, radius of the village, its land beyond the walls).</summary>
        public static Patrols Create(Transform hero, HumanBody.Parts parts, IEnumerable<(Vector3 village, float radius, string clan)> villages)
        {
            var patrols = new GameObject("Patrols").AddComponent<Patrols>();
            patrols.hero = hero;
            patrols.parts = parts;

            foreach (var (village, radius, clan) in villages)
            {
                // Four posts on the ways out of the village, a little beyond its walls; one of them a leader's.
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * Mathf.PI / 2 + 0.6f;
                    var at = village + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius + 70 + 30 * (i % 2));
                    var kinds = i == 0
                        ? new[] { MasterKinds.Of(clan, 2), MasterKinds.Of(clan, 1), MasterKinds.Of(clan, 1) }
                        : i == 2
                            ? new[] { MasterKinds.Of(clan, 3), MasterKinds.Of(clan, 2) }
                            : new[] { MasterKinds.Of(clan, 1), MasterKinds.Of(clan, 1) };
                    patrols.posts.Add(new Post { At = at, Land = village, LandRadius = radius + 260, Kinds = kinds });
                }
            }

            return patrols;
        }

        void Update()
        {
            if (hero == null || Time.time < nextLook)
            {
                return;
            }

            nextLook = Time.time + 1;
            var at = hero.position;

            for (var i = posts.Count - 1; i >= 0; i--)
            {
                var post = posts[i];
                post.Masters.RemoveAll(m => m == null);
                var living = post.Masters.FindAll(m => !m.Dead);

                if (post.Called)
                {
                    if (post.Masters.Count == 0)
                    {
                        posts.RemoveAt(i);
                    }

                    continue;
                }

                var distance = Flat(at - post.At).magnitude;

                if (distance > GoAway)
                {
                    foreach (var master in living)
                    {
                        post.Masters.Remove(master);
                        Destroy(master.gameObject);
                    }

                    continue;
                }

                if (living.Count == 0 && post.Masters.Count == 0 && distance < ComeOut && Time.time >= post.BackAt)
                {
                    for (var k = 0; k < post.Kinds.Length; k++)
                    {
                        var angle = k * Mathf.PI * 2 / post.Kinds.Length;
                        var master = Place(post, post.At + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3, post.Kinds[k], false);
                        master.Land = post.Land;
                        master.LandRadius = post.LandRadius;
                    }
                }
                else if (living.Count == 0 && post.Masters.Count > 0)
                {
                    post.BackAt = Time.time + Returns;
                }
            }
        }

        /// <summary>Gu Masters called up beside the hero, 14–20 m off (toward a way, if given), fighting on sight; how many came.</summary>
        public int CallNear(MasterKind[] kinds, Vector3? toward = null)
        {
            if (hero == null || kinds.Length == 0)
            {
                return 0;
            }

            var angle = toward.HasValue ? Mathf.Atan2(toward.Value.z, toward.Value.x) + (Random.value - 0.5f) * 0.6f : Random.value * Mathf.PI * 2;
            var at = hero.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (14 + Random.value * 6);
            var post = new Post { At = at, Kinds = kinds, Called = true };
            posts.Add(post);

            for (var k = 0; k < kinds.Length; k++)
            {
                var a = k * Mathf.PI * 2 / kinds.Length;
                Place(post, at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (k == 0 ? 0 : 3), kinds[k], true);
            }

            return post.Masters.Count;
        }

        GuMaster Place(Post post, Vector3 at, MasterKind kind, bool hostile)
        {
            if (UnityEngine.Physics.Raycast(new Vector3(at.x, at.y + 60, at.z), Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore))
            {
                at = hit.point + Vector3.up * 0.05f;
            }
            else
            {
                at.y = GuWorld.World.QingMao.GroundAt(at) + 0.05f;
            }

            var master = GuMaster.Make(at, kind, hero, parts, seed++ * 7919, hostile);
            post.Masters.Add(master);

            return master;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
