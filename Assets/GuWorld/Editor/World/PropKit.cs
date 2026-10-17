using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The small things of village life, each drawn straight into the village's
    /// meshes where it lies (MeshKit.Transform carries it there): baskets for rubbish,
    /// barrels, crates, sacks, brooms, buckets, water jars; wine jars and bottles by
    /// the taverns, tables and stools; racks of swords and spears by the smithy and
    /// the gates; books and scrolls at the academy; firewood, hay, carts, laundry on a
    /// line, drying racks of herbs; lamps, braziers, stone lanterns; trees along the
    /// lanes and fallen leaves everywhere. Some are solid (a box to bump into).
    /// </summary>
    public static partial class QingMaoBuilder
    {
        static Material leafMaterial;
        static Material LeafLitter => leafMaterial != null ? leafMaterial : leafMaterial = Cutout("Fallen Leaves", 0xffffff, Texture("Leaf", 64, (u, v) =>
        {
            // One leaf: an almond shape with a midrib.
            var x = (u - 0.5f) * 2;
            var y = v * 2 - 1;
            var inside = x * x / Mathf.Max(0.01f, 0.42f * (1 - y * y)) + 0 < 1 && Mathf.Abs(y) < 0.95f;
            var rib = Mathf.Abs(x) < 0.04f;
            var tone = 0.75f + 0.25f * Mathf.Sin(u * 9 + v * 5);
            return inside ? new Color(tone * (rib ? 0.7f : 1), tone * (rib ? 0.6f : 0.85f), tone * 0.5f, 1) : new Color(0, 0, 0, 0);
        }, false, false), 1, 0.5f, true);

        /// <summary>Draws a prop into a kit (its Transform already set to where the prop lies).</summary>
        /// <summary>For the mesh report: a thing drawn alone.</summary>
        public static void DrawPropForReport(MeshKit kit, string kind, int seed) => DrawProp(kit, kind, seed);

        static void DrawProp(MeshKit kit, string kind, int seed)
        {
            var random = new System.Random(seed);
            float R() => (float)random.NextDouble();
            var up = Vector3.up;

            switch (kind)
            {
                case "barrel":
                    kit.Cylinder(Wood, Vector3.zero, up * 0.9f, 0.3f, 0.3f, 10);
                    kit.Cylinder(Iron, up * 0.15f, up * 0.2f, 0.31f, 0.31f, 10, false);
                    kit.Cylinder(Iron, up * 0.7f, up * 0.75f, 0.31f, 0.31f, 10, false);
                    break;
                case "crates":
                    kit.Box(Wood, up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), R() * 20);
                    kit.Box(Wood, new Vector3(0.45f, 0.25f, 0.1f), new Vector3(0.5f, 0.5f, 0.5f), R() * 30);
                    if (R() < 0.6f) kit.Box(Wood, new Vector3(0.05f, 0.82f, 0), new Vector3(0.5f, 0.45f, 0.5f), R() * 40);
                    break;
                case "sacks":
                    for (var i = 0; i < 3; i++) kit.Blob(Straw, new Vector3(-0.3f + i * 0.32f, 0.22f, (i % 2) * 0.15f), new Vector3(0.22f, 0.25f, 0.18f), seed + i);
                    break;
                case "broom":
                    // Leaning on the wall behind it.
                    kit.Cylinder(Wood, new Vector3(0, 0.15f, -0.25f), new Vector3(0, 1.4f, -0.05f), 0.02f, 0.02f, 4);
                    kit.Cylinder(Straw, new Vector3(0, 0, -0.3f), new Vector3(0, 0.38f, -0.24f), 0.16f, 0.03f, 6);
                    break;
                case "bucket":
                    kit.Cylinder(Wood, Vector3.zero, up * 0.32f, 0.17f, 0.2f, 8);
                    kit.Cylinder(Water, up * 0.26f, up * 0.27f, 0.18f, 0.18f, 8);
                    break;
                case "water jar":
                    kit.Cylinder(Clay, Vector3.zero, up * 0.2f, 0.2f, 0.32f, 10);
                    kit.Cylinder(Clay, up * 0.2f, up * 0.62f, 0.32f, 0.24f, 10);
                    kit.Cylinder(Wood, up * 0.62f, up * 0.65f, 0.27f, 0.27f, 10);
                    break;
                case "basket":
                case "goods basket":
                    kit.Cylinder(Straw, Vector3.zero, up * 0.35f, 0.2f, 0.3f, 8);
                    if (kind == "goods basket")
                    {
                        var goods = R() < 0.33f ? Greens : R() < 0.5f ? Mat("Ware Orange", 0xb86a24, 0.2f) : Mat("Ware Red", 0x8a2a20, 0.2f);
                        for (var i = 0; i < 5; i++) kit.Blob(goods, new Vector3((R() - 0.5f) * 0.3f, 0.36f, (R() - 0.5f) * 0.3f), Vector3.one * 0.1f, seed + i);
                    }
                    break;
                case "rubbish basket":
                    kit.Cylinder(Straw, Vector3.zero, up * 0.65f, 0.24f, 0.3f, 8);
                    kit.Blob(Mat("Rubbish", 0x3a3428, 0.05f), up * 0.62f, new Vector3(0.26f, 0.12f, 0.26f), seed);
                    break;
                case "rubbish heap":
                    kit.Blob(Mat("Rubbish", 0x3a3428, 0.05f), up * 0.15f, new Vector3(0.9f, 0.3f, 0.7f), seed);
                    kit.Box(Wood, new Vector3(0.3f, 0.2f, 0.1f), new Vector3(0.6f, 0.05f, 0.12f), R() * 90);
                    kit.Cylinder(Clay, new Vector3(-0.3f, 0.1f, 0.2f), new Vector3(-0.1f, 0.15f, 0.35f), 0.08f, 0.06f, 6);
                    break;
                case "flower pots":
                    for (var i = 0; i < 3; i++)
                    {
                        var at = new Vector3(-0.3f + i * 0.3f, 0, (i % 2) * 0.1f);
                        kit.Cylinder(Clay, at, at + up * 0.25f, 0.1f, 0.14f, 8);
                        kit.Blob(i == 1 ? Mat("Flowers", 0x8a4a6a, 0.1f) : Greens, at + up * 0.35f, new Vector3(0.14f, 0.14f, 0.14f), seed + i);
                    }
                    break;
                case "stool":
                    kit.Box(Wood, up * 0.42f, new Vector3(0.4f, 0.05f, 0.4f));
                    foreach (var (x, z) in new[] { (-0.15f, -0.15f), (0.15f, -0.15f), (-0.15f, 0.15f), (0.15f, 0.15f) }) kit.Box(DarkWood, new Vector3(x, 0.2f, z), new Vector3(0.04f, 0.4f, 0.04f));
                    break;
                case "bench":
                    kit.Box(Wood, up * 0.45f, new Vector3(1.6f, 0.06f, 0.35f));
                    foreach (var x in new[] { -0.65f, 0.65f }) kit.Box(DarkWood, new Vector3(x, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.32f));
                    break;
                case "firewood":
                    for (var row = 0; row < 4; row++)
                    {
                        for (var i = 0; i < 6; i++)
                        {
                            var y = 0.1f + row * 0.21f;
                            var x = -0.75f + i * 0.28f + (row % 2) * 0.12f;
                            kit.Cylinder(Logs, new Vector3(x, y, -0.25f), new Vector3(x, y, 0.25f), 0.1f, 0.1f, 5);
                        }
                    }
                    kit.Quad(TilesWorn, new Vector3(-1, 1.05f, -0.4f), new Vector3(1, 1.05f, -0.4f), new Vector3(1, 0.95f, 0.4f), new Vector3(-1, 0.95f, 0.4f), up);
                    break;
                case "hay":
                    kit.Blob(Straw, up * 0.55f, new Vector3(0.9f, 0.6f, 0.8f), seed);
                    break;
                case "drying rack":
                    foreach (var x in new[] { -0.85f, 0.85f }) kit.Box(Wood, new Vector3(x, 0.9f, 0), new Vector3(0.06f, 1.8f, 0.06f));
                    for (var i = 0; i < 3; i++) kit.Box(Wood, new Vector3(0, 0.7f + i * 0.45f, 0), new Vector3(1.8f, 0.04f, 0.04f));
                    var herb = R() < 0.5f ? Mat("Herbs", 0x4a5a2a, 0.05f, true) : Mat("Dried Peppers", 0x7a2218, 0.1f);
                    for (var i = 0; i < 9; i++) kit.Cylinder(herb, new Vector3(-0.75f + (i % 5) * 0.37f, 1.6f - (i / 5) * 0.45f, 0), new Vector3(-0.75f + (i % 5) * 0.37f, 1.3f - (i / 5) * 0.45f, 0), 0.06f, 0.03f, 4);
                    break;
                case "laundry":
                    foreach (var x in new[] { -1.4f, 1.4f }) kit.Box(Wood, new Vector3(x, 1.1f, 0), new Vector3(0.06f, 2.2f, 0.06f));
                    kit.Box(Wood, new Vector3(0, 2.05f, 0), new Vector3(2.8f, 0.02f, 0.02f));
                    for (var i = 0; i < 3; i++)
                    {
                        var cloth = i == 0 ? Cloth("Wash Blue", 0x34405a) : i == 1 ? Cloth("Wash White", 0xa8a294) : Cloth("Wash Brown", 0x5a4632);
                        kit.Box(cloth, new Vector3(-0.9f + i * 0.9f, 1.65f, 0), new Vector3(0.7f, 0.8f, 0.02f));
                    }
                    break;
                case "cart":
                    kit.Box(Wood, up * 0.75f, new Vector3(1.2f, 0.1f, 1.8f));
                    foreach (var x in new[] { -0.58f, 0.58f }) kit.Box(Wood, new Vector3(x, 0.95f, 0), new Vector3(0.06f, 0.35f, 1.8f));
                    foreach (var x in new[] { -0.7f, 0.7f }) kit.Cylinder(DarkWood, new Vector3(x - 0.05f, 0.5f, 0.1f), new Vector3(x + 0.05f, 0.5f, 0.1f), 0.5f, 0.5f, 10);
                    kit.Box(Wood, new Vector3(0.25f, 0.7f, 1.6f), new Vector3(0.06f, 0.06f, 1.6f));
                    kit.Box(Wood, new Vector3(-0.25f, 0.7f, 1.6f), new Vector3(0.06f, 0.06f, 1.6f));
                    if (R() < 0.5f) kit.Blob(Straw, new Vector3(0, 1.05f, 0), new Vector3(0.5f, 0.3f, 0.7f), seed);
                    break;
                case "stall":
                    kit.Box(Wood, new Vector3(0, 0.85f, 0), new Vector3(3, 0.12f, 1.2f));
                    kit.Box(DarkWood, new Vector3(0, 0.42f, 0), new Vector3(2.8f, 0.8f, 1));
                    foreach (var x in new[] { -1.45f, 1.45f })
                    {
                        kit.Box(DarkWood, new Vector3(x, 1.25f, 0.55f), new Vector3(0.1f, 2.5f, 0.1f));
                        kit.Box(DarkWood, new Vector3(x, 1.5f, -1.1f), new Vector3(0.1f, 3, 0.1f));
                    }
                    var awning = seed % 3 == 0 ? Cloth("Awning Red", 0x6a2820) : seed % 3 == 1 ? Cloth("Awning Indigo", 0x26304a) : Cloth("Awning Ochre", 0x7a5a30);
                    kit.Quad(awning, new Vector3(-1.7f, 3.05f, -1.3f), new Vector3(1.7f, 3.05f, -1.3f), new Vector3(1.7f, 2.45f, 0.9f), new Vector3(-1.7f, 2.45f, 0.9f), up);
                    for (var k = 0; k < 7; k++) kit.Blob(k % 2 == 0 ? Greens : Mat("Ware Orange", 0xb86a24, 0.2f), new Vector3(-1.1f + k * 0.36f, 1.05f, 0.15f * ((k % 2) * 2 - 1)), new Vector3(0.17f, 0.15f, 0.17f), seed * 7 + k);
                    break;
                case "well":
                    VillageWell(kit);
                    break;
                case "old tree":
                    // The square's old camphor: a huge gnarled trunk, roots spreading, a crown over half the square.
                    kit.Cylinder(Bark, Vector3.zero, up * 5, 0.9f, 0.6f, 10);
                    for (var i = 0; i < 5; i++)
                    {
                        var a = i * 1.26f;
                        kit.Cylinder(Bark, new Vector3(Mathf.Cos(a) * 0.6f, 0.4f, Mathf.Sin(a) * 0.6f), new Vector3(Mathf.Cos(a) * 2, -0.1f, Mathf.Sin(a) * 2), 0.35f, 0.1f, 6);
                        kit.Cylinder(Bark, up * 4.5f, new Vector3(Mathf.Cos(a) * 3.5f, 7.5f + R(), Mathf.Sin(a) * 3.5f), 0.4f, 0.15f, 6);
                        kit.Blob(i % 2 == 0 ? DarkLeaves : Leaves, new Vector3(Mathf.Cos(a) * 4, 8.5f + R(), Mathf.Sin(a) * 4), new Vector3(3.4f, 2.4f, 3.4f), seed + i);
                    }
                    kit.Blob(Leaves, up * 10, new Vector3(4.5f, 3, 4.5f), seed + 9);
                    // A ring of stone to sit on round it.
                    for (var i = 0; i < 12; i++)
                    {
                        var a = i * Mathf.PI / 6;
                        kit.Box(Stone, new Vector3(Mathf.Cos(a) * 2.6f, 0.22f, Mathf.Sin(a) * 2.6f), new Vector3(1.4f, 0.45f, 0.45f), -a * Mathf.Rad2Deg + 90);
                    }
                    break;
                case "tree":
                    kit.Cylinder(Bark, Vector3.zero, up * 2.6f, 0.2f, 0.14f, 6);
                    for (var i = 0; i < 4; i++)
                    {
                        var a = i * 1.7f + R();
                        kit.Blob(i % 2 == 0 ? Leaves : DarkLeaves, new Vector3(Mathf.Cos(a) * 0.9f, 3.3f + R() * 0.8f, Mathf.Sin(a) * 0.9f), new Vector3(1.4f, 1.1f, 1.4f), seed + i);
                    }
                    break;
                case "pine tree":
                    kit.Cylinder(PaleBark, Vector3.zero, new Vector3(0.4f, 3.5f, 0.1f), 0.18f, 0.12f, 6);
                    for (var i = 0; i < 3; i++) kit.Blob(PineNeedles, new Vector3(0.4f + Mathf.Cos(i * 2.1f) * 1.1f, 3.2f + i * 0.5f, Mathf.Sin(i * 2.1f) * 1.1f), new Vector3(1.3f, 0.45f, 1.1f), seed + i);
                    break;
                case "leaves":
                    for (var i = 0; i < 14; i++)
                    {
                        var at = new Vector3((R() - 0.5f) * 1.6f, 0.03f + R() * 0.02f, (R() - 0.5f) * 1.6f);
                        var a = R() * Mathf.PI * 2;
                        var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.07f;
                        var v = new Vector3(-u.z, 0, u.x) * 1.6f;
                        kit.QuadUV(LeafLitter, at - u - v, at + u - v, at + u + v, at - u + v, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    }
                    break;
                case "stones":
                    for (var i = 0; i < 4; i++) kit.Blob(Rubble, new Vector3((R() - 0.5f) * 0.9f, 0.08f, (R() - 0.5f) * 0.9f), new Vector3(0.18f + R() * 0.12f, 0.12f, 0.16f + R() * 0.1f), seed + i);
                    break;
                case "tavern table":
                case "tea table":
                case "dice table":
                case "book table":
                    kit.Box(Wood, up * 0.75f, new Vector3(1.2f, 0.06f, 0.75f));
                    foreach (var (x, z) in new[] { (-0.5f, -0.3f), (0.5f, -0.3f), (-0.5f, 0.3f), (0.5f, 0.3f) }) kit.Box(DarkWood, new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.74f, 0.06f));
                    foreach (var z in new[] { -0.75f, 0.75f }) kit.Box(Wood, new Vector3(0, 0.42f, z), new Vector3(1.1f, 0.05f, 0.3f));
                    if (kind == "tavern table")
                    {
                        // A jug and cups, a bottle or two.
                        kit.Cylinder(Clay, new Vector3(0.1f, 0.78f, 0), new Vector3(0.1f, 1.02f, 0), 0.08f, 0.05f, 8);
                        for (var i = 0; i < 3; i++) kit.Cylinder(Glaze, new Vector3(-0.35f + i * 0.3f, 0.78f, 0.22f), new Vector3(-0.35f + i * 0.3f, 0.84f, 0.22f), 0.04f, 0.045f, 6);
                        if (R() < 0.6f) Bottle(kit, new Vector3(-0.4f, 0.78f, -0.15f));
                    }
                    else if (kind == "tea table")
                    {
                        kit.Blob(Glaze, new Vector3(0, 0.85f, 0), new Vector3(0.12f, 0.09f, 0.12f), seed);
                        for (var i = 0; i < 4; i++) kit.Cylinder(Mat("Porcelain", 0xb8b8ae, 0.5f), new Vector3(-0.4f + i * 0.26f, 0.78f, 0.2f), new Vector3(-0.4f + i * 0.26f, 0.82f, 0.2f), 0.035f, 0.045f, 6);
                    }
                    else if (kind == "dice table")
                    {
                        kit.Cylinder(Wood, new Vector3(0, 0.78f, 0), new Vector3(0, 0.88f, 0), 0.12f, 0.1f, 8);
                        for (var i = 0; i < 6; i++) kit.Box(Mat("Coin", 0x8a7a42, 0.6f), new Vector3((R() - 0.5f) * 0.8f, 0.79f, (R() - 0.5f) * 0.5f), new Vector3(0.05f, 0.01f, 0.05f));
                    }
                    else
                    {
                        Books(kit, new Vector3(-0.3f, 0.78f, 0), random);
                        kit.Box(Mat("Open Book", 0xb0a688, 0.05f), new Vector3(0.25f, 0.79f, 0), new Vector3(0.4f, 0.02f, 0.28f), 8);
                        kit.Cylinder(Mat("Scroll", 0xb8aa86, 0.05f), new Vector3(0.1f, 0.81f, 0.25f), new Vector3(0.5f, 0.81f, 0.25f), 0.03f, 0.03f, 6);
                    }
                    break;
                case "books":
                    Books(kit, Vector3.zero, random);
                    Books(kit, new Vector3(0.35f, 0, 0.1f), random);
                    break;
                case "scrolls":
                    for (var i = 0; i < 5; i++) kit.Cylinder(Mat("Scroll", 0xb8aa86, 0.05f), new Vector3(-0.3f, 0.04f + (i % 3) * 0.07f, -0.2f + i * 0.08f), new Vector3(0.3f, 0.04f + (i % 3) * 0.07f, -0.2f + i * 0.08f), 0.035f, 0.035f, 6);
                    break;
                case "wine jars":
                    for (var i = 0; i < 5; i++)
                    {
                        var at = new Vector3(-0.45f + (i % 3) * 0.45f, 0, (i / 3) * 0.45f - 0.2f);
                        var s = 0.8f + R() * 0.4f;
                        kit.Cylinder(Glaze, at, at + up * 0.25f * s, 0.18f * s, 0.26f * s, 10);
                        kit.Cylinder(Glaze, at + up * 0.25f * s, at + up * 0.6f * s, 0.26f * s, 0.12f * s, 10);
                        kit.Cylinder(Cloth("Wine Red", 0x6a2620), at + up * 0.6f * s, at + up * 0.68f * s, 0.12f * s, 0.12f * s, 8);
                    }
                    break;
                case "bottles":
                    for (var i = 0; i < 6; i++) Bottle(kit, new Vector3((R() - 0.5f) * 0.9f, 0, (R() - 0.5f) * 0.6f), R() < 0.3f);
                    break;
                case "jars":
                    for (var i = 0; i < 6; i++)
                    {
                        var at = new Vector3(-0.5f + (i % 3) * 0.4f, (i / 3) * 0.35f, 0);
                        kit.Cylinder(i % 2 == 0 ? Clay : Glaze, at, at + up * 0.3f, 0.12f, 0.1f, 8);
                    }
                    kit.Box(Wood, new Vector3(-0.1f, 0.32f, 0), new Vector3(1.3f, 0.04f, 0.35f));
                    break;
                case "gu pots":
                    kit.Cylinder(Clay, Vector3.zero, up * 0.35f, 0.22f, 0.28f, 10);
                    kit.Cylinder(Clay, up * 0.35f, up * 0.4f, 0.24f, 0.24f, 10);
                    break;
                case "weapon rack":
                    foreach (var x in new[] { -0.85f, 0.85f }) kit.Box(DarkWood, new Vector3(x, 0.8f, 0), new Vector3(0.08f, 1.6f, 0.4f));
                    kit.Box(DarkWood, new Vector3(0, 1.45f, 0), new Vector3(1.8f, 0.06f, 0.08f));
                    kit.Box(DarkWood, new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.06f, 0.35f));
                    for (var i = 0; i < 6; i++)
                    {
                        var x = -0.7f + i * 0.28f;

                        if (i % 2 == 0)
                        {
                            // A spear: shaft and iron head.
                            kit.Cylinder(Wood, new Vector3(x, 0.28f, 0.06f), new Vector3(x, 2.2f, -0.04f), 0.02f, 0.02f, 4);
                            kit.Cylinder(Iron, new Vector3(x, 2.2f, -0.04f), new Vector3(x, 2.45f, -0.05f), 0.035f, 0, 4);
                        }
                        else
                        {
                            // A sword, point down: blade, guard, grip.
                            kit.Box(Iron, new Vector3(x, 0.85f, 0.04f), new Vector3(0.04f, 0.9f, 0.01f));
                            kit.Box(Bronze, new Vector3(x, 1.32f, 0.04f), new Vector3(0.14f, 0.03f, 0.04f));
                            kit.Cylinder(Cloth("Grip", 0x2a1e18), new Vector3(x, 1.33f, 0.04f), new Vector3(x, 1.52f, 0.04f), 0.018f, 0.018f, 4);
                        }
                    }
                    break;
                case "spears":
                    for (var i = 0; i < 4; i++)
                    {
                        var x = -0.3f + i * 0.2f;
                        kit.Cylinder(Wood, new Vector3(x, 0, 0.3f), new Vector3(x + 0.05f, 2.3f, -0.15f), 0.022f, 0.022f, 4);
                        kit.Cylinder(Iron, new Vector3(x + 0.05f, 2.3f, -0.15f), new Vector3(x + 0.055f, 2.55f, -0.2f), 0.035f, 0, 4);
                    }
                    break;
                case "anvil":
                    kit.Cylinder(Logs, Vector3.zero, up * 0.5f, 0.3f, 0.3f, 8);
                    kit.Box(Iron, up * 0.62f, new Vector3(0.6f, 0.22f, 0.22f));
                    kit.Cylinder(Iron, new Vector3(0.3f, 0.66f, 0), new Vector3(0.5f, 0.66f, 0), 0.08f, 0.01f, 5);
                    kit.Box(Iron, new Vector3(-0.1f, 0.76f, 0.05f), new Vector3(0.3f, 0.04f, 0.04f), 30);
                    break;
                case "charcoal":
                    kit.Blob(Mat("Charcoal", 0x141414, 0.05f), up * 0.2f, new Vector3(0.8f, 0.35f, 0.7f), seed);
                    break;
                case "sign":
                    kit.Box(DarkWood, up * 1.1f, new Vector3(0.08f, 2.2f, 0.08f));
                    kit.Box(DarkWood, new Vector3(0, 1.5f, 0.06f), new Vector3(0.45f, 1.1f, 0.05f));
                    kit.Box(Gold, new Vector3(0, 1.5f, 0.09f), new Vector3(0.28f, 0.85f, 0.01f));
                    break;
                case "wine flag":
                    kit.Box(DarkWood, up * 2.2f, new Vector3(0.08f, 4.4f, 0.08f));
                    kit.Box(Cloth("Wine Flag", 0x8a2a1e), new Vector3(0.38f, 3.3f, 0), new Vector3(0.7f, 1.9f, 0.03f));
                    break;
                case "red lantern pole":
                    kit.Box(DarkWood, up * 1.4f, new Vector3(0.1f, 2.8f, 0.1f));
                    kit.Box(DarkWood, new Vector3(0.35f, 2.75f, 0), new Vector3(0.8f, 0.06f, 0.06f));
                    kit.Cylinder(Glow(Mat("Red Lantern", 0xa83020, 0.3f)), new Vector3(0.65f, 2.2f, 0), new Vector3(0.65f, 2.6f, 0), 0.2f, 0.2f, 10);
                    break;
                case "lamp":
                    LanternPost(kit);
                    break;
                case "stone lantern":
                    StoneLantern(kit, new Vector3(0, 0, 0));
                    break;
                case "brazier":
                    foreach (var a in new[] { 0f, 2.1f, 4.2f }) kit.Cylinder(Iron, new Vector3(Mathf.Cos(a) * 0.25f, 0, Mathf.Sin(a) * 0.25f), new Vector3(Mathf.Cos(a) * 0.15f, 0.7f, Mathf.Sin(a) * 0.15f), 0.03f, 0.03f, 4);
                    kit.Cylinder(Iron, up * 0.7f, up * 0.95f, 0.22f, 0.32f, 10);
                    kit.Blob(WarmLantern, up * 0.98f, new Vector3(0.24f, 0.1f, 0.24f), seed);
                    break;
                case "banner":
                    ClanBanner(kit, Vector3.zero, 6.5f);
                    break;
                case "notice board":
                    foreach (var x in new[] { -0.9f, 0.9f }) kit.Box(DarkWood, new Vector3(x, 1, 0), new Vector3(0.12f, 2, 0.12f));
                    kit.Box(Wood, new Vector3(0, 1.4f, 0), new Vector3(1.8f, 1, 0.08f));
                    for (var i = 0; i < 5; i++) kit.Box(Mat("Paper Note", 0xb4ac96, 0.05f), new Vector3(-0.6f + i * 0.3f, 1.35f + (i % 2) * 0.2f, 0.05f), new Vector3(0.22f, 0.3f, 0.01f), (R() - 0.5f) * 8);
                    kit.Quad(TilesWorn, new Vector3(-1.1f, 2.15f, -0.4f), new Vector3(1.1f, 2.15f, -0.4f), new Vector3(1.1f, 2, 0.4f), new Vector3(-1.1f, 2, 0.4f), up);
                    break;
                case "drum":
                    foreach (var x in new[] { -0.5f, 0.5f }) kit.Box(DarkWood, new Vector3(x, 0.8f, 0), new Vector3(0.1f, 1.6f, 0.1f));
                    kit.Cylinder(Lacquer, new Vector3(0, 1.1f, -0.35f), new Vector3(0, 1.1f, 0.35f), 0.45f, 0.45f, 14);
                    kit.Cylinder(Mat("Drum Skin", 0x8a7a5a, 0.1f), new Vector3(0, 1.1f, 0.35f), new Vector3(0, 1.1f, 0.37f), 0.42f, 0.42f, 14);
                    break;
                case "hitching post":
                    foreach (var x in new[] { -0.9f, 0.9f }) kit.Box(DarkWood, new Vector3(x, 0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f));
                    kit.Box(Wood, new Vector3(0, 1, 0), new Vector3(1.9f, 0.08f, 0.08f));
                    break;
            }
        }

        static void Bottle(MeshKit kit, Vector3 at, bool fallen = false)
        {
            if (fallen)
            {
                kit.Cylinder(Glaze, at + new Vector3(0, 0.06f, 0), at + new Vector3(0.22f, 0.06f, 0.05f), 0.06f, 0.025f, 6);

                return;
            }

            // A wine gourd: a round belly, a narrow neck, a stopper.
            kit.Blob(Glaze, at + Vector3.up * 0.09f, new Vector3(0.08f, 0.09f, 0.08f), 3);
            kit.Cylinder(Glaze, at + Vector3.up * 0.16f, at + Vector3.up * 0.27f, 0.035f, 0.022f, 6);
            kit.Cylinder(Cloth("Stopper", 0x6a2620), at + Vector3.up * 0.27f, at + Vector3.up * 0.3f, 0.025f, 0.02f, 5);
        }

        static void Books(MeshKit kit, Vector3 at, System.Random random)
        {
            var covers = new[] { Mat("Book Blue", 0x283450, 0.1f), Mat("Book Brown", 0x4a3424, 0.1f), Mat("Book Ochre", 0x7a6034, 0.1f) };
            var y = at.y;

            for (var i = 0; i < 3 + random.Next(4); i++)
            {
                var h = 0.03f + (float)random.NextDouble() * 0.02f;
                kit.Box(covers[random.Next(covers.Length)], new Vector3(at.x, y + h / 2, at.z), new Vector3(0.24f, h, 0.18f), (float)random.NextDouble() * 20 - 10);
                y += h;
            }
        }
    }
}
