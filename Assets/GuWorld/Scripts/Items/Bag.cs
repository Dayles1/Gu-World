using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Items
{
    /// <summary>A pile of one kind of thing in one cell of the bag.</summary>
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
        /// <summary>The game minute it spoils at; 0: it keeps.</summary>
        public double spoils;
        /// <summary>Which cell it lies in (saved: the bag keeps its order).</summary>
        public int cell;

        public ItemKind Kind => ItemBook.Find(id);
    }

    /// <summary>
    /// What the hero carries (the design's inventory): forty cells, eight by
    /// five, and a weight it cannot go over. Things of a kind pile up in a cell
    /// to its stack size; things that spoil only pile with others of nearly the
    /// same age (fresh petals do not join old ones), and whatever is taken out
    /// is taken from the pile that would spoil first.
    /// </summary>
    public class Bag
    {
        public const int Cells = 40;
        public const int Across = 8;
        public const float Capacity = 60;
        /// <summary>Spoiling things within this many game minutes of each other pile together.</summary>
        const double SameAge = 60;

        readonly ItemStack[] cells = new ItemStack[Cells];

        public ItemStack this[int cell] => cell >= 0 && cell < Cells ? cells[cell] : null;

        public float Weight
        {
            get
            {
                var sum = 0f;

                foreach (var stack in cells)
                {
                    if (stack != null)
                    {
                        sum += stack.count * (stack.Kind?.Weight ?? 0);
                    }
                }

                return sum;
            }
        }

        public int Count(string id)
        {
            var n = 0;

            foreach (var stack in cells)
            {
                if (stack != null && stack.id == id)
                {
                    n += stack.count;
                }
            }

            return n;
        }

        public bool Has(string id, int count) => Count(id) >= count;

        /// <summary>How many more of a kind would fit, by weight and by room.</summary>
        public int Room(string id, double now)
        {
            var kind = ItemBook.Find(id);

            if (kind == null)
            {
                return 0;
            }

            // Primeval stones are the money: they never weigh the bag past carrying (only room for their pile counts).
            var byWeight = kind.Weight > 0 && id != ItemBook.Stone ? Mathf.FloorToInt((Capacity - Weight) / kind.Weight + 1e-4f) : int.MaxValue;
            var spoils = kind.Keeps > 0 ? now + kind.Keeps : 0;
            var byRoom = 0;

            foreach (var stack in cells)
            {
                if (stack == null)
                {
                    byRoom += kind.Stack;
                }
                else if (Joins(stack, id, spoils))
                {
                    byRoom += kind.Stack - stack.count;
                }
            }

            return Mathf.Max(0, Mathf.Min(byWeight, byRoom));
        }

        static bool Joins(ItemStack stack, string id, double spoils) =>
            stack.id == id && stack.count < (stack.Kind?.Stack ?? 1) && Math.Abs(stack.spoils - spoils) <= SameAge;

        /// <summary>Puts things in (as many as fit); returns how many went in.</summary>
        public int Add(string id, int count, double now)
        {
            var kind = ItemBook.Find(id);

            if (kind == null || count <= 0)
            {
                return 0;
            }

            count = Mathf.Min(count, Room(id, now));
            var spoils = kind.Keeps > 0 ? now + kind.Keeps : 0;
            var left = count;

            // Onto piles of the same age first, then into empty cells.
            for (var i = 0; i < Cells && left > 0; i++)
            {
                if (cells[i] != null && Joins(cells[i], id, spoils))
                {
                    var put = Mathf.Min(left, kind.Stack - cells[i].count);
                    cells[i].count += put;
                    // The pile spoils when its oldest part does.
                    cells[i].spoils = Math.Min(cells[i].spoils, spoils);
                    left -= put;
                }
            }

            for (var i = 0; i < Cells && left > 0; i++)
            {
                if (cells[i] == null)
                {
                    var put = Mathf.Min(left, kind.Stack);
                    cells[i] = new ItemStack { id = id, count = put, spoils = spoils, cell = i };
                    left -= put;
                }
            }

            return count - left;
        }

        /// <summary>Takes things out — all of them or none — from the piles that spoil first.</summary>
        public bool Take(string id, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (!Has(id, count))
            {
                return false;
            }

            var piles = new List<int>();

            for (var i = 0; i < Cells; i++)
            {
                if (cells[i] != null && cells[i].id == id)
                {
                    piles.Add(i);
                }
            }

            // Spoiling piles first, soonest first; the ones that keep last.
            piles.Sort((a, b) => Order(cells[a]).CompareTo(Order(cells[b])));

            foreach (var i in piles)
            {
                var take = Mathf.Min(count, cells[i].count);
                cells[i].count -= take;
                count -= take;

                if (cells[i].count <= 0)
                {
                    cells[i] = null;
                }

                if (count <= 0)
                {
                    break;
                }
            }

            return true;
        }

        static double Order(ItemStack stack) => stack.spoils > 0 ? stack.spoils : double.MaxValue;

        /// <summary>Takes some (or all) of one cell's pile.</summary>
        public int TakeFrom(int cell, int count)
        {
            var stack = this[cell];

            if (stack == null || count <= 0)
            {
                return 0;
            }

            var take = Mathf.Min(count, stack.count);
            stack.count -= take;

            if (stack.count <= 0)
            {
                cells[cell] = null;
            }

            return take;
        }

        /// <summary>Splits a pile in two (half into the first empty cell); false when it cannot.</summary>
        public bool Split(int cell)
        {
            var stack = this[cell];

            if (stack == null || stack.count < 2)
            {
                return false;
            }

            var empty = Array.IndexOf(cells, null);

            if (empty < 0)
            {
                return false;
            }

            var half = stack.count / 2;
            stack.count -= half;
            cells[empty] = new ItemStack { id = stack.id, count = half, spoils = stack.spoils, cell = empty };

            return true;
        }

        /// <summary>Throws out what has spoiled by now; returns what was lost, by kind.</summary>
        public Dictionary<string, int> Spoil(double now)
        {
            Dictionary<string, int> lost = null;

            for (var i = 0; i < Cells; i++)
            {
                var stack = cells[i];

                if (stack != null && stack.spoils > 0 && stack.spoils <= now)
                {
                    lost ??= new Dictionary<string, int>();
                    lost.TryGetValue(stack.id, out var n);
                    lost[stack.id] = n + stack.count;
                    cells[i] = null;
                }
            }

            return lost;
        }

        /// <summary>
        /// Puts the bag in order: Gu first, then food, herbs and materials, each kind by
        /// name; piles of a kind joined where they can be (and are of an age).
        /// </summary>
        public void Sort()
        {
            var all = new List<ItemStack>();

            foreach (var stack in cells)
            {
                if (stack != null)
                {
                    all.Add(stack);
                }
            }

            all.Sort((a, b) =>
            {
                var ka = a.Kind;
                var kb = b.Kind;
                var c = (ka?.Category ?? ItemCategory.Material).CompareTo(kb?.Category ?? ItemCategory.Material);

                if (c == 0) c = string.CompareOrdinal(ka?.Name ?? a.id, kb?.Name ?? b.id);
                if (c == 0) c = Order(a).CompareTo(Order(b));

                return c;
            });

            Array.Clear(cells, 0, Cells);
            var at = 0;

            foreach (var stack in all)
            {
                var kind = stack.Kind;
                var previous = at > 0 ? cells[at - 1] : null;

                // Joined onto the pile before it where it is the same and of an age.
                if (previous != null && kind != null && previous.id == stack.id && Math.Abs(previous.spoils - stack.spoils) <= SameAge)
                {
                    var put = Mathf.Min(stack.count, kind.Stack - previous.count);
                    previous.count += put;
                    previous.spoils = Math.Min(previous.spoils, stack.spoils);
                    stack.count -= put;
                }

                if (stack.count > 0 && at < Cells)
                {
                    stack.cell = at;
                    cells[at++] = stack;
                }
            }
        }

        public void Clear() => Array.Clear(cells, 0, Cells);

        /// <summary>The bag as saved: its piles, each with its cell.</summary>
        public ItemStack[] Save()
        {
            var list = new List<ItemStack>();

            for (var i = 0; i < Cells; i++)
            {
                if (cells[i] != null)
                {
                    list.Add(new ItemStack { id = cells[i].id, count = cells[i].count, spoils = cells[i].spoils, cell = i });
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// The bag from a save, made sound: unknown things and empty piles dropped, piles
        /// cut to their stack size, a cell taken twice moved to a free one.
        /// </summary>
        public void Load(ItemStack[] saved)
        {
            Clear();

            if (saved == null)
            {
                return;
            }

            foreach (var stack in saved)
            {
                var kind = stack != null ? ItemBook.Find(stack.id) : null;

                if (kind == null || stack.count <= 0)
                {
                    continue;
                }

                var cell = stack.cell >= 0 && stack.cell < Cells && cells[stack.cell] == null ? stack.cell : Array.IndexOf(cells, null);

                if (cell < 0)
                {
                    break;
                }

                cells[cell] = new ItemStack
                {
                    id = stack.id,
                    count = Mathf.Min(stack.count, kind.Stack),
                    spoils = double.IsFinite(stack.spoils) && stack.spoils > 0 && kind.Keeps > 0 ? stack.spoils : 0,
                    cell = cell,
                };
            }
        }
    }
}
