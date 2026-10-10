using System.Collections.Generic;
using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Village
{
    /// <summary>
    /// The people of a village (Roster), living by the game's clock. They are
    /// made one a frame — a body is built the first time its look is needed —
    /// so the village fills in over the first moments instead of holding the
    /// game up. Each frame every one takes a step of their day; the one the
    /// player stands before, facing them, can be spoken to.
    /// </summary>
    public class Villagers : MonoBehaviour
    {
        VillageLayout layout;
        HumanBody.Parts parts;
        readonly Queue<VillagerSpec> waiting = new Queue<VillagerSpec>();
        readonly List<Villager> people = new List<Villager>();
        float lastHour = -1;

        public IReadOnlyList<Villager> People => people;
        public bool AllHere => waiting.Count == 0;

        public static Villagers Create(VillageLayout layout, HumanBody.Parts parts)
        {
            var villagers = new GameObject("Villagers").AddComponent<Villagers>();
            villagers.layout = layout;
            villagers.parts = parts;

            foreach (var person in Roster.People())
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
                var spec = waiting.Dequeue();
                var villager = new GameObject(spec.Name).AddComponent<Villager>();
                villager.transform.SetParent(transform, false);
                villager.Init(spec, people.Count, layout, parts);
                people.Add(villager);
            }

            // A leap of the clock (more than a quarter hour in one step): everyone is where they should be.
            var jumped = lastHour >= 0 && Mathf.Abs(Mathf.DeltaAngle(lastHour * 15, hour * 15)) > 0.25f * 15 + dt * 0.5f;
            lastHour = hour;

            foreach (var person in people)
            {
                person.Tick(hour, dt, camera, jumped);
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
                if (person.Asleep)
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
