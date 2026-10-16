using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Engine;
using UnityEngine;

namespace GuWorld.Village
{
    /// <summary>
    /// The people of a village (Roster), living by the game's clock. They are
    /// made a few a frame, within a few milliseconds shared by every village —
    /// a body is built the first time its look is needed — so the village fills
    /// in over the first moments instead of holding the game up; a village far
    /// from the camera waits to be made until one comes near it. Each frame
    /// every one takes a step of their day; the one the player stands before,
    /// facing them, can be spoken to.
    /// </summary>
    public class Villagers : MonoBehaviour
    {
        /// <summary>How long making people may take in a frame, all villages together, ms (one is always made).</summary>
        const double BudgetMs = 6;
        /// <summary>A village's people are made once the camera is this near its houses, m.</summary>
        const float MakeNear = 450;

        static int budgetFrame = -1;
        static double spent;

        VillageLayout layout;
        HumanBody.Parts parts;
        readonly Queue<VillagerSpec> waiting = new Queue<VillagerSpec>();
        readonly List<Villager> people = new List<Villager>();
        float lastHour = -1;
        /// <summary>The houses' middle and how far the farthest is from it.</summary>
        Vector3 centre;
        float reach = -1;

        public IReadOnlyList<Villager> People => people;
        public bool AllHere => waiting.Count == 0;
        /// <summary>Far from the camera, with people still to be made when it comes near.</summary>
        public bool Away { get; private set; }

        public static Villagers Create(VillageLayout layout, HumanBody.Parts parts) => Create(layout, parts, Roster.People());

        /// <summary>A village's people: who they are (made one a frame, as they are needed).</summary>
        public static Villagers Create(VillageLayout layout, HumanBody.Parts parts, System.Collections.Generic.IEnumerable<VillagerSpec> specs)
        {
            var villagers = new GameObject($"Villagers {layout.Settlement}").AddComponent<Villagers>();
            villagers.layout = layout;
            villagers.parts = parts;

            foreach (var person in specs)
            {
                villagers.waiting.Enqueue(person);
            }

            return villagers;
        }

        /// <summary>A step of the village's day: the hour (0…24), seconds passed, where the camera is.</summary>
        public void Tick(float hour, float dt, Vector3 camera)
        {
            if (waiting.Count > 0 && layout.Houses.Length > 0)
            {
                Make(camera);
            }

            // A leap of the clock (more than a quarter hour in one step): everyone is where they should be.
            var jumped = lastHour >= 0 && Mathf.Abs(Mathf.DeltaAngle(lastHour * 15, hour * 15)) > 0.25f * 15 + dt * 0.5f;
            lastHour = hour;

            foreach (var person in people)
            {
                person.Tick(hour, dt, camera, jumped);
            }
        }

        /// <summary>Makes the next people, while this frame's time for it lasts — when the camera is near enough to see them.</summary>
        void Make(Vector3 camera)
        {
            if (reach < 0)
            {
                centre = Vector3.zero;

                foreach (var house in layout.Houses)
                {
                    centre += house.at / layout.Houses.Length;
                }

                reach = 0;

                foreach (var house in layout.Houses)
                {
                    reach = Mathf.Max(reach, Vector3.Distance(house.at, centre));
                }
            }

            var dx = camera.x - centre.x;
            var dz = camera.z - centre.z;
            Away = dx * dx + dz * dz > (reach + MakeNear) * (reach + MakeNear);

            if (Away)
            {
                return;
            }

            if (budgetFrame != Time.frameCount)
            {
                budgetFrame = Time.frameCount;
                spent = 0;
            }

            while (waiting.Count > 0 && spent < BudgetMs)
            {
                var spec = waiting.Dequeue();
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                var villager = new GameObject(spec.Name).AddComponent<Villager>();
                villager.transform.SetParent(transform, false);
                villager.Init(spec, people.Count, layout, parts);
                people.Add(villager);
                var ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                spent += ms;
                Costs.Add("villager made", ms);
            }
        }

        /// <summary>Whom one can speak to from here: awake, within reach, and before one.</summary>
        public Villager Before(Vector3 at, Vector3 forward, float reach = 2.8f)
        {
            Villager best = null;
            var bestDistance = reach;
            forward.y = 0;
            forward.Normalize();

            foreach (var person in people)
            {
                if (person.Asleep || person.Down)
                {
                    continue;
                }

                var to = person.transform.position - at;
                to.y = 0;
                var distance = to.magnitude;

                if (distance < bestDistance && (distance < 1 || Vector3.Dot(to / distance, forward) > 0.35f))
                {
                    best = person;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
