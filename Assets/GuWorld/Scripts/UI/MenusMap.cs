using GuWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The map page: the mountain as the HUD's minimap draws it, whole, with its
    /// places and the hero; beside it the world of the atlas, with where Qing Mao
    /// Mountain lies in it, and what the atlas says of its region.
    /// </summary>
    public partial class Menus
    {
        const float MapSize = 820;
        const float WorldSize = 420;

        void MapPage()
        {
            var body = BookFrame("map", "Карта", Page.Map);
            var map = host.Map;

            var left = UiKit.Panel("Mountain", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -(MapSize + 40), 0);
            var frame = UiKit.Rect("Frame", left.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(MapSize, MapSize));
            UiKit.Caps(host.PlaceName, left.transform, 14).rectTransform.Place(new Vector2(0, 1), Vector2.one, 22, -30, 22, 8);

            if (map != null)
            {
                var picture = UiKit.Rect("Map", frame).gameObject.AddComponent<RawImage>();
                picture.texture = map.Texture;
                picture.rectTransform.Fill();
                picture.raycastTarget = false;
                UiKit.Frame(frame, UiKit.GoldDeep);

                // The big places first; a smaller one's name only where it does not cover another's.
                var taken = new System.Collections.Generic.List<Rect>();
                var marks = new System.Collections.Generic.List<MapMark>(map.Marks);
                marks.Sort((x, y) => y.Major.CompareTo(x.Major));

                foreach (var mark in marks)
                {
                    var at = (map.ToMap(mark.At) - new Vector2(0.5f, 0.5f)) * MapSize;
                    var dot = UiKit.Image("Mark", frame, mark.Major ? UiKit.GoldLit : UiKit.Text, UiKit.Disc);
                    dot.rectTransform.At(new Vector2(0.5f, 0.5f), at, Vector2.one * (mark.Major ? 12 : 7));
                    var size = mark.Major ? 17 : 13;
                    var box = new Rect(at.x - mark.Label.Length * size * 0.28f, at.y + 6, mark.Label.Length * size * 0.56f, size + 6);

                    if (taken.Exists(r => r.Overlaps(box)))
                    {
                        continue;
                    }

                    taken.Add(box);
                    var label = UiKit.Label(mark.Label, frame, size, mark.Major ? UiKit.Text : UiKit.Muted, TextAnchor.MiddleCenter, mark.Major ? FontStyle.Bold : FontStyle.Normal).Shadowed();
                    label.rectTransform.At(new Vector2(0.5f, 0.5f), at + new Vector2(0, 16), new Vector2(240, 24));
                }

                var hero = UiKit.Image("Hero", frame, UiKit.Seal, UiKit.Arrow);
                hero.rectTransform.At(new Vector2(0.5f, 0.5f), (map.ToMap(host.HeroAt) - new Vector2(0.5f, 0.5f)) * MapSize, new Vector2(26, 26));
                hero.rectTransform.localEulerAngles = new Vector3(0, 0, -host.HeroFacing * Mathf.Rad2Deg);
                var north = UiKit.Label("С", frame, 20, UiKit.GoldLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
                north.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(30, 30));
                var scale = UiKit.Label($"{map.Area.width / 1000:0.0} км с запада на восток · север сверху", frame, 14, UiKit.Muted, TextAnchor.MiddleRight).Shadowed();
                scale.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 10, 6, 14, -26);
            }
            else
            {
                UiKit.Label("Карты этого места нет.", frame, 20, UiKit.Muted, TextAnchor.MiddleCenter).rectTransform.Fill();
            }

            // The world of the atlas.
            var right = UiKit.Panel("World", body);
            right.rectTransform.Place(Vector2.zero, Vector2.one, MapSize + 80, 0, 0, 0);
            var world = UiKit.Rect("World Map", right.transform).Place(new Vector2(0, 1), new Vector2(0, 1), 28, -(WorldSize + 72), -(28 + WorldSize), 72);
            UiKit.Caps($"{Atlas.WorldName} — атлас", right.transform, 14).rectTransform.Place(new Vector2(0, 1), Vector2.one, 28, -56, 28, 26);
            var texture = Atlas.WorldMap();

            if (texture != null)
            {
                var picture = UiKit.Rect("Atlas", world).gameObject.AddComponent<RawImage>();
                picture.texture = texture;
                picture.rectTransform.Fill();
                picture.raycastTarget = false;
                UiKit.Frame(world, UiKit.GoldDeep);
                var here = (Atlas.ToMap(Atlas.QingMao.At) - new Vector2(0.5f, 0.5f)) * WorldSize;
                var ring = UiKit.Image("Here", world, UiKit.Seal, UiKit.Ring);
                ring.rectTransform.At(new Vector2(0.5f, 0.5f), here, new Vector2(26, 26));
                var dot = UiKit.Image("Dot", world, UiKit.GoldLit, UiKit.Disc);
                dot.rectTransform.At(new Vector2(0.5f, 0.5f), here, new Vector2(7, 7));
                var label = UiKit.Label(Atlas.QingMao.Name, world, 15, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
                label.rectTransform.At(new Vector2(0.5f, 0.5f), here + new Vector2(96, 0), new Vector2(170, 22));
            }

            var info = UiKit.Rect("Info", right.transform).Place(new Vector2(0, 0), new Vector2(1, 1), WorldSize + 56, 24, 28, 72);
            info.Stack<VerticalLayoutGroup>(6);
            var place = Atlas.QingMao;
            var region = place.Region;
            UiKit.Label(place.Name, info, 26, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(34);
            UiKit.Caps(region.Name, info, 13, UiKit.EssenceLit).Size(22);
            UiKit.Label($"По атласу: около {place.Width / 1000:0} км в поперечнике, вершина {place.Top:0} м над морем, {place.Rock}. " +
                        $"Ранги мастеров здесь — {place.RankMin}–{place.RankMax}.", info, 16).Size(70);
            UiKit.Label("Три деревни делят одну гору и враждуют: Гу Юэ — на поясе горы, Сюн — на передней горе, Бай — у глубокого омута под водопадом на задней. " +
                        "У подножия — хутора простых людей.", info, 16, UiKit.Muted).Size(70);
            UiKit.Label($"<b>Климат:</b> {region.Climate}", info, 16).Size(26);
            UiKit.Label($"<b>Погода:</b> {string.Join(", ", region.Weather)}", info, 16).Size(48);
            UiKit.Label($"<b>Природа:</b> {string.Join(", ", region.Nature)}", info, 16).Size(48);
            UiKit.Label($"<b>Опасности:</b> {string.Join(", ", region.Hazards)}", info, 16).Size(48);

            // Under the world map, in its column.
            var below = UiKit.Rect("Below", right.transform).Place(Vector2.zero, new Vector2(0, 1), 28, 24, -(28 + WorldSize), 72 + WorldSize + 20);
            below.Stack<VerticalLayoutGroup>(6);
            UiKit.Label("Играть можно только на горе Цинмао: на карте — вся гора, двадцать километров с запада на восток. " +
                        "Остальной мир атласа откроется в следующих арках.", below, 16, UiKit.Muted).Size(70);
        }
    }
}
