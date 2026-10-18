using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The fusion page (U, or from the aperture): every recipe the hero knows on the left — what Gu it makes and from
    /// what, ready ones marked — filtered by what can be fused now or by the result's rank; on the right the chosen one:
    /// the Gu it makes and what it does, each Gu and material it takes against what the hero has (the Gu from the bag
    /// and the ring, never the vital one), the essence it costs at once and the chance it holds — failed, all that was
    /// put in is lost — and the button that fuses them.
    /// </summary>
    public partial class Menus
    {
        int fusionFilter;
        int fusionChosen;
        float fusionScroll;

        static readonly Color Enough = UiKit.Hex(0x8FD0A0);

        /// <summary>How many of a Gu the hero has to put into a fusion: in the bag, and in the ring besides the vital one.</summary>
        int HaveForFusion(string id)
        {
            var gu = host.Cultivator;
            var kind = GuBook.Find(id);

            return kind == null ? 0 : gu.Bag.Count(kind.Item) + gu.Gu.FindAll(g => g.kind == id && !g.vital).Count;
        }

        static Dictionary<string, int> Needed(Recipe recipe)
        {
            var counts = new Dictionary<string, int>();

            foreach (var id in recipe.Gu)
            {
                counts[id] = counts.TryGetValue(id, out var n) ? n + 1 : 1;
            }

            return counts;
        }

        bool Shown(Recipe recipe)
        {
            var result = GuBook.Find(recipe.Result);

            return fusionFilter switch
            {
                1 => host.Cultivator.Lacks(recipe) == null,
                2 => result.Rank == 2,
                3 => result.Rank == 3,
                4 => result.Rank >= 4,
                _ => true,
            };
        }

        void FusionPage()
        {
            var gu = host.Cultivator;
            var body = BookFrame("ring", "Слияние Гу", Page.Fusion);

            // The recipes, on the left.
            var left = UiKit.Panel("Recipes", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -620, 0);
            var column = UiKit.Rect("Column", left.transform).Fill();
            column.Stack<VerticalLayoutGroup>(8, new RectOffset(22, 22, 18, 16));
            var ready = System.Array.FindAll(GuBook.Recipes, r => gu.Lacks(r) == null).Length;
            UiKit.Label($"РЕЦЕПТЫ · {GuBook.Recipes.Length}   <size=14><color=#8FD0A0>готовы: {ready}</color></size>", column, 20, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(28);
            UiKit.Choices(column, new[] { "Все", "Готовы", "Ранг II", "Ранг III", "Ранг IV+" }, fusionFilter, i =>
            {
                fusionFilter = i;
                fusionScroll = 0;
                Show(Page.Fusion);
            }, 13);
            var list = Scroll(column, fusionScroll, y => fusionScroll = y);
            var shown = new List<int>();

            for (var i = 0; i < GuBook.Recipes.Length; i++)
            {
                if (Shown(GuBook.Recipes[i]))
                {
                    shown.Add(i);
                }
            }

            if (shown.Count > 0 && !shown.Contains(fusionChosen))
            {
                fusionChosen = shown[0];
            }

            foreach (var index in shown)
            {
                var recipe = GuBook.Recipes[index];
                var result = GuBook.Find(recipe.Result);
                var can = gu.Lacks(recipe) == null;
                var parts = new List<string>();

                foreach (var pair in Needed(recipe))
                {
                    parts.Add((pair.Value > 1 ? $"{pair.Value}× " : "") + GuBook.Find(pair.Key).Name.Replace("Гу ", ""));
                }

                foreach (var (item, count) in recipe.Materials)
                {
                    parts.Add($"{ItemBook.Find(item)?.Name.ToLowerInvariant()} ×{count}");
                }

                var i = index;
                var mark = can ? "<color=#8FD0A0>●</color>" : "<color=#6A5A44>○</color>";
                var row = UiKit.Button($"{mark}  <b>{result.Name}</b>  <size=12><color=#A69A86>ранг {Aperture.RankNumeral(result.Rank)}</color></size>\n<size=12><color=#A69A86>{string.Join(" + ", parts)}</color></size>",
                    list, () =>
                    {
                        fusionChosen = i;
                        Show(Page.Fusion);
                    }, index == fusionChosen ? ButtonKind.Tab : ButtonKind.Plain, 15);
                row.Size(56);
                var text = row.GetComponentInChildren<Text>();

                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleLeft;
                    text.rectTransform.offsetMin = new Vector2(16, text.rectTransform.offsetMin.y);
                }
            }

            if (shown.Count == 0)
            {
                UiKit.Label(fusionFilter == 1 ? "Пока ни одно слияние не готово: соберите Гу и материалы." : "Здесь пусто.", list, 15, UiKit.Muted).Size(40);
            }

            // The chosen recipe, on the right.
            var right = UiKit.Panel("Recipe", body);
            right.rectTransform.Place(Vector2.zero, Vector2.one, 640, 0, 0, 0);
            var details = UiKit.Rect("Details", right.transform).Fill();
            details.Stack<VerticalLayoutGroup>(8, new RectOffset(30, 30, 24, 22));

            if (shown.Count == 0)
            {
                UiKit.Label("Выберите рецепт слева.", details, 18, UiKit.Muted).Size(40);

                return;
            }

            var chosen = GuBook.Recipes[fusionChosen];
            var made = GuBook.Find(chosen.Result);
            var head = UiKit.Rect("Head", details);
            head.Size(84);
            var icon = UiKit.Icon(made.Icon, head, 72, made.Colour);
            icon.rectTransform.At(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(72, 72));
            var name = UiKit.Label($"<b>{made.Name}</b>\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(Aperture.RankColour(made.Rank))}>РАНГ {made.Rank} · {made.Kind} · {made.Line}</color></size>", head, 26);
            name.rectTransform.Place(Vector2.zero, Vector2.one, 96, 0, 0, 0);
            UiKit.Label(ItemBook.Find(made.Item)?.About ?? made.About ?? "", details, 15, UiKit.Muted).Size(60);
            UiKit.Label($"<b>Навык:</b> {made.SkillName} — {SkillLine(made)}", details, 15).Size(44);
            UiKit.Image("Rule", details, UiKit.Line).Size(2);

            UiKit.Caps("Что уходит в слияние", details, 13, UiKit.Gold).Size(22);

            foreach (var pair in Needed(chosen))
            {
                var kind = GuBook.Find(pair.Key);
                var have = HaveForFusion(pair.Key);
                Ingredient(details, kind.Icon, kind.Colour, kind.Name, pair.Value, have);
            }

            foreach (var (item, count) in chosen.Materials)
            {
                var what = ItemBook.Find(item);
                Ingredient(details, what?.Icon ?? "bag", UiKit.Text, what?.Name ?? item, count, gu.Bag.Count(item));
            }

            UiKit.Image("Rule", details, UiKit.Line).Size(2);
            var cost = gu.FuseCost(chosen);
            var essence = gu.EndlessEssence || gu.Aperture.Essence >= cost;
            Stat(details, "Эссенция разом", $"<color=#{(essence ? "8FD0A0" : "C2413A")}>{cost:0.#}%</color>  · в море {gu.Aperture.Essence:0}%" + (cost > gu.Aperture.Cap ? " · больше предела моря!" : ""));
            Stat(details, "Шанс", $"{gu.FuseChance(chosen) * 100:0}%" + (gu.FuseChance(chosen) >= 1 ? " (Гу уверенности)" : ""));
            UiKit.Label("Сорвётся — погибнут и Гу, и материалы. Рождённый Гу ложится в сумку: очистите его в апертуру.", details, 13, UiKit.Muted).Size(36);

            var space = UiKit.Rect("Space", details);
            space.Size(-1).flexibleHeight = 1;
            var lacks = gu.Lacks(chosen);
            var able = lacks == null && essence;
            var recipeNow = chosen;
            UiKit.Button(able ? UiKit.Spaced("Слить") : lacks != null ? $"Не хватает: {lacks}" : "Не хватает эссенции", details, () =>
            {
                if (!able)
                {
                    return;
                }

                var done = host.Cultivator.Fuse(recipeNow);
                Show(Page.Fusion);
                Banner(done ? "Слияние удалось" : "Слияние сорвалось", done ? $"{made.Name} · ранг {made.Rank} — в сумке" : "Гу и материалы погибли");
            }, able ? ButtonKind.Gold : ButtonKind.Plain, able ? 18 : 13).Size(54);
        }

        /// <summary>One thing a fusion takes: its picture, its name, how many it takes against how many the hero has.</summary>
        static void Ingredient(Transform parent, string icon, Color colour, string name, int need, int have)
        {
            var row = UiKit.Rect("Ingredient", parent);
            row.Size(34);
            var picture = UiKit.Icon(icon, row, 26, colour);
            picture.rectTransform.At(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(26, 26));
            var enough = have >= need;
            var line = UiKit.Label($"{name}  <color=#A69A86>×{need}</color>", row, 16);
            line.rectTransform.Place(Vector2.zero, Vector2.one, 40, 0, 150, 0);
            var count = UiKit.Label(enough ? $"есть {have}" : $"есть {have} · нужно ещё {need - have}", row, 14, enough ? Enough : UiKit.Danger, TextAnchor.MiddleRight);
            count.rectTransform.Place(new Vector2(1, 0), Vector2.one, -220, 0, 0, 0);
        }
    }
}
