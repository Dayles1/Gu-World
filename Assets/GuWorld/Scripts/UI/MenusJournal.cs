using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The journal (J): the academy's task in hand (or who waits with the next), the side tasks taken — each with how far
    /// along it is and a way to give it up — and the hero's merit and standing with the clan, with where to earn more.
    /// </summary>
    public partial class Menus
    {
        void JournalPage()
        {
            var body = BookFrame("book", "Журнал", Page.Journal);
            var quests = host.Quests;

            // The tasks.
            var left = UiKit.Panel("Tasks", body);
            left.rectTransform.Place(Vector2.zero, Vector2.one, 0, 0, 520, 0);
            var tasks = UiKit.Rect("Column", left.transform).Fill();
            tasks.Stack<VerticalLayoutGroup>(8, new RectOffset(26, 26, 22, 20));

            Heading(tasks, "ЗАДАНИЕ АКАДЕМИИ");

            if (quests.Current is QuestKind current)
            {
                TaskCard(tasks, current, quests.ProgressOf(current), quests.Ready, false);
            }
            else if (quests.Next is QuestKind next)
            {
                UiKit.Label($"Вас ждёт {next.GiverTitle} {next.Giver} — с новым поручением.", tasks, 17, UiKit.Text).Size(40);
            }
            else
            {
                UiKit.Label("Все задания академии выполнены.", tasks, 17, UiKit.Muted).Size(40);
            }

            Heading(tasks, $"ПОБОЧНЫЕ ПОРУЧЕНИЯ · {quests.Sides.Count} из {Quests.MaxSides}");

            if (quests.Sides.Count == 0)
            {
                UiKit.Label("Пока нет. Поговорите с жителями деревни (E) — у многих найдётся дело; поручения клана дают в павильоне.", tasks, 16, UiKit.Muted).Size(54);
            }

            foreach (var side in quests.Sides)
            {
                TaskCard(tasks, side, quests.ProgressOf(side), quests.IsReady(side), true);
            }

            // The clan.
            var right = UiKit.Panel("Clan", body);
            right.rectTransform.Place(new Vector2(1, 0), Vector2.one, -496, 0, 0, 0);
            var clan = UiKit.Rect("Column", right.transform).Fill();
            clan.Stack<VerticalLayoutGroup>(8, new RectOffset(26, 26, 22, 20));
            Heading(clan, "КЛАН ГУ ЮЭ");
            Stat(clan, "Вклад", $"{quests.Merit}");
            Stat(clan, "Всего заработано", $"{quests.MeritEarned}");
            Stat(clan, "Положение", quests.Standing);
            Stat(clan, "Скидка в лавках", quests.Discount > 0 ? $"{quests.Discount * 100:0}%" : "нет");
            UiKit.Image("Rule", clan, UiKit.Line).Size(2);
            UiKit.Label($"Вклад дают задания и поручения. Распорядительница {SideQuests.Steward} в павильоне клана каждый день даёт три новых " +
                        "поручения и держит сокровищницу: там за вклад — Гу, которых не купить за камни.", clan, 16, UiKit.Muted).Size(120);
            UiKit.Image("Rule", clan, UiKit.Line).Size(2);
            Heading(clan, "КЛАНЫ ГОРЫ");

            foreach (var rival in GuWorld.Creatures.Clans.Rivals)
            {
                Stat(clan, GuWorld.Creatures.MasterKinds.ClanName(rival), $"{GuWorld.Creatures.Clans.Standing(rival)} · {GuWorld.Creatures.Clans.Hostility(rival):0}");
            }

            UiKit.Label("Убитые Гу-мастера клана злят его: от 50 дозоры нападают без слов, от 75 его отряды охотятся на вас в лесах. Со временем вражда остывает.",
                clan, 15, UiKit.Muted).Size(80);
        }

        /// <summary>One task: its title and giver, what it asks, how far along, what it pays — and, for a side task, giving it up.</summary>
        void TaskCard(Transform parent, QuestKind quest, int progress, bool ready, bool side)
        {
            var card = UiKit.Panel(quest.Title, parent, UiKit.InkLight);
            card.Size(side ? 128 : 110);
            var inside = UiKit.Rect("Inside", card.transform).Fill(14);
            inside.Stack<VerticalLayoutGroup>(2);
            UiKit.Label($"<b>{quest.Title}</b>  <color=#A69A86>· {quest.Giver}</color>", inside, 18, UiKit.Text).Size(28);
            UiKit.Label(ready ? $"Готово — вернитесь к: {quest.Giver}" : quest.Goal, inside, 15, ready ? UiKit.EssenceLit : UiKit.Text).Size(24);
            UiKit.Label($"{progress} / {quest.Target}   ·   награда: {Items.ItemBook.Stones(quest.RewardStones)}" + (quest.RewardMerit > 0 ? $", вклад {quest.RewardMerit}" : ""),
                inside, 14, UiKit.GoldLit).Size(22);

            if (side)
            {
                var row = UiKit.Rect("Row", inside);
                row.Size(30);
                row.Stack<HorizontalLayoutGroup>(6, null, TextAnchor.MiddleRight).childForceExpandWidth = false;
                UiKit.Button("Оставить поручение", row, () =>
                {
                    host.Quests.Drop(quest);
                    Show(Page.Journal);
                }, ButtonKind.Plain, 13).Size(30, 200, 0);
            }
        }
    }
}
