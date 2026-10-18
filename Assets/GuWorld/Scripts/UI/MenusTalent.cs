using GuWorld.Cultivation;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The way to begin (the first step of a new game, before the hero is made): five
    /// difficulties, each the talent one is born with — from an A talent of 99% (with a
    /// chance of the Ten Extreme Physique) to a D talent of 30% — with the sea it gives
    /// and how soon it refills.
    /// </summary>
    public partial class Menus
    {
        void TalentChoice()
        {
            Backdrop();
            var card = UiKit.Panel("Way", pageRoot);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280, 900));
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(10, new RectOffset(56, 56, 40, 34));

            UiKit.Label("ВЫБОР ПУТИ", column, 38, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(50);
            UiKit.Label("Сложность — это талант, с которым вы родились. Он задаёт, как высоко стоит море эссенции в апертуре " +
                        "и как быстро оно наполняется само: лучший талант даёт и больше, и быстрее.", column, 17, UiKit.Muted).Size(52);

            for (var i = 0; i < Talents.Paths.Length; i++)
            {
                var index = i;
                var path = Talents.Paths[i];
                var image = UiKit.Image(path.Name, column, Color.white, UiKit.Chamfer);
                image.raycastTarget = true;
                UiKit.Frame(image.transform, i == 0 ? UiKit.Gold : UiKit.Line);
                var hover = image.gameObject.AddComponent<Hover>();
                hover.Set(image, UiKit.InkLight, UiKit.InkHover, () =>
                {
                    host.ChosenPath = index;
                    Show(Page.Creator);
                });
                image.Size(118);

                // The grade's seal, the name and the numbers, the words.
                var seal = UiKit.SealText(path.Grade.ToString(), image.transform, 70);
                seal.rectTransform.At(new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(70, 70));
                var name = UiKit.Label(path.Name, image.transform, 26, UiKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
                name.rectTransform.Place(Vector2.zero, Vector2.one, 124, 0, 520, 14);
                var hours = Aperture.RefillHours(path.Talent);
                var numbers = UiKit.Label($"Талант {path.Grade} · {path.Talent:0}% · море наполняется за {Talents.Span(hours)}" +
                                          (path.ExtremeChance > 0 ? $" · шанс {path.ExtremeChance * 100:0}% на 100%" : ""),
                    image.transform, 16, Aperture.RankColour(1) * 1.4f, TextAnchor.UpperRight);
                numbers.rectTransform.Place(Vector2.zero, Vector2.one, 520, 0, 26, 18);
                var about = UiKit.Label(path.About, image.transform, 16, UiKit.Muted, TextAnchor.UpperLeft);
                about.rectTransform.Place(Vector2.zero, Vector2.one, 124, 8, 26, 52);
            }

            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;
            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleRight).childForceExpandWidth = false;
            buttons.Size(56);
            UiKit.Label("Выберите путь — затем создание героя", buttons, 16, UiKit.Muted, TextAnchor.MiddleLeft).Size(56, 900);
            UiKit.Button("Назад", buttons, () => FadeThrough(host.BackToTitle)).Size(56, 200);
        }
    }
}
