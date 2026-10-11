using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Village
{
    /// <summary>
    /// A village's plan, as its builder laid it out (QingMaoBuilder): the houses'
    /// doorsteps, the named places people go to (the square, the stalls, the
    /// gates, the shrine, the academy, the spring…), and the walkways between
    /// them as a graph — street centres, the square, the doorsteps, the paths
    /// out of the gates. People walk along it (Route).
    /// </summary>
    public class VillageLayout : MonoBehaviour
    {
        [Serializable]
        public struct Spot
        {
            public string name;
            public Vector3 at;
            /// <summary>Which way one stands there (degrees about Y; a house's: facing out of its door).</summary>
            public float yaw;
        }

        /// <summary>Whose village it is (QingMao's settlement id: "gu_yue", "bai", "xiong").</summary>
        public string Settlement;
        public Spot[] Houses = new Spot[0];
        public Spot[] Places = new Spot[0];
        public Vector3[] Nodes = new Vector3[0];
        /// <summary>The walkways: pairs of node indices.</summary>
        public Vector2Int[] Edges = new Vector2Int[0];

        List<int>[] links;

        public bool TryPlace(string name, out Spot spot)
        {
            foreach (var place in Places)
            {
                if (place.name == name)
                {
                    spot = place;

                    return true;
                }
            }

            spot = default;

            return false;
        }

        /// <summary>The node nearest a point.</summary>
        public int Nearest(Vector3 at)
        {
            var best = -1;
            var distance = float.PositiveInfinity;

            for (var i = 0; i < Nodes.Length; i++)
            {
                var d = (Nodes[i] - at).sqrMagnitude;

                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// The way from one point to another along the walkways (A*): to the node
        /// nearest the start, along the graph, then on to the end itself.
        /// </summary>
        public List<Vector3> Route(Vector3 from, Vector3 to)
        {
            var route = new List<Vector3>();
            var start = Nearest(from);
            var goal = Nearest(to);

            if (start < 0 || goal < 0)
            {
                route.Add(to);

                return route;
            }

            if (links == null)
            {
                links = new List<int>[Nodes.Length];

                for (var i = 0; i < Nodes.Length; i++)
                {
                    links[i] = new List<int>();
                }

                foreach (var edge in Edges)
                {
                    links[edge.x].Add(edge.y);
                    links[edge.y].Add(edge.x);
                }
            }

            var cameFrom = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start] = 0 };
            var open = new List<int> { start };

            while (open.Count > 0)
            {
                var current = open[0];
                var best = cost[current] + Vector3.Distance(Nodes[current], Nodes[goal]);

                foreach (var candidate in open)
                {
                    var score = cost[candidate] + Vector3.Distance(Nodes[candidate], Nodes[goal]);

                    if (score < best)
                    {
                        best = score;
                        current = candidate;
                    }
                }

                if (current == goal)
                {
                    break;
                }

                open.Remove(current);

                foreach (var next in links[current])
                {
                    var through = cost[current] + Vector3.Distance(Nodes[current], Nodes[next]);

                    if (!cost.TryGetValue(next, out var known) || through < known)
                    {
                        cost[next] = through;
                        cameFrom[next] = current;

                        if (!open.Contains(next))
                        {
                            open.Add(next);
                        }
                    }
                }
            }

            if (!cost.ContainsKey(goal))
            {
                route.Add(to);

                return route;
            }

            for (var node = goal; ; node = cameFrom[node])
            {
                route.Insert(0, Nodes[node]);

                if (node == start)
                {
                    break;
                }
            }

            route.Add(to);

            return route;
        }
    }
}
