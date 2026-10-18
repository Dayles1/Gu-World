using System.Collections.Generic;
using GuWorld.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The map page: the mountain as the HUD's minimap draws it, whole, with its
    /// places and the hero; beside it the world of the atlas, with where Qing Mao
    /// Mountain lies in it, and what the atlas says of its region.
    ///
    /// The mountain's map zooms (the wheel, + and −, the buttons on it) toward the
    /// pointer and is dragged about with the left button. Closer in, the part in
    /// sight is drawn again finely — streets and roofs, as on the minimap — a few
    /// rows a frame, and laid over the whole map's picture once it is done. A grid
    /// of round distances lies over it, the coordinates under the pointer show in
    /// its corner, and a click (the button let go where it was pressed) puts a
    /// mark there: its coordinates, its height and how far it is show in the
    /// corner, with a button to go there at once. The mark stays (the minimap
    /// shows it too) until it is taken off.
    /// </summary>
    public partial class Menus
    {
        const float MapSize = 820;
        const float WorldSize = 420;
        /// <summary>The closest the map comes: about a metre and a half of the mountain to a point of the screen.</summary>
        const float MapZoomMax = 16;
        /// <summary>From this close the part in sight is drawn finely.</summary>
        const float MapDetailFrom = 2.5f;
        const int MapDetailSize = 1024;
        const int MapDetailRowsPerFrame = 96;
        /// <summary>A press that moves less than this (screen points) before it is let go is a click, not a drag.</summary>
        const float MapClickSlop = 6;
        const int MapGridLines = 24;

        // Kept between openings: the map opens where it was left.
        float mapZoom = 1;
        Vector2 mapCentre = new Vector2(0.5f, 0.5f);

        /// <summary>The mark put on the map (x and z in the world's metres, y its ground's height or NaN), or none.</summary>
        public Vector3? MapPin { get; private set; }

        RectTransform mapView;
        RectTransform mapContent;
        RawImage mapNearImage;
        RawImage mapDetailImage;
        RectTransform mapHero;
        RectTransform mapPinMark;
        RectTransform mapQuestMark;
        Text mapQuestLabel;
        RectTransform mapPinPanel;
        Text mapPinLine;
        Text mapPinDetail;
        Text mapCursorLine;
        Text mapScaleLine;
        RectTransform mapScaleBar;
        readonly List<(MapMark mark, RectTransform dot, Text label)> mapMarks = new List<(MapMark, RectTransform, Text)>();
        readonly List<RectTransform> mapGrid = new List<RectTransform>();
        readonly List<Text> mapGridLabels = new List<Text>();
        readonly List<RaycastResult> mapHits = new List<RaycastResult>();
        bool mapDragging;
        Vector2 mapDragFrom;
        Vector2 mapPressedAt;
        float mapPressMoved;
        float mapSettledAt;

        // The fine drawing of the part in sight (kept while the game runs; one at a time).
        LocalMap mapDetail;
        LocalMap.Painting mapPainting;

        void MapPage()
        {
            var body = BookFrame("map", "Карта", Page.Map);
            var map = host.Map;
            mapMarks.Clear();
            mapGrid.Clear();
            mapGridLabels.Clear();
            mapView = null;

            var left = UiKit.Panel("Mountain", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -(MapSize + 40), 0);
            var frame = UiKit.Rect("Frame", left.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(MapSize, MapSize));
            UiKit.Caps(host.PlaceName, left.transform, 14).rectTransform.Place(new Vector2(0, 1), Vector2.one, 22, -30, 22, 8);

            if (map != null)
            {
                // The window onto the map (it catches the wheel and the drag), and the map inside it, as big as the zoom makes it.
                var under = UiKit.Image("View", frame, UiKit.Hex(0x0E0D0C));
                under.rectTransform.Fill();
                under.raycastTarget = true;
                under.gameObject.AddComponent<RectMask2D>();
                mapView = under.rectTransform;
                mapContent = UiKit.Rect("Content", mapView);

                var picture = UiKit.Rect("Map", mapContent).gameObject.AddComponent<RawImage>();
                picture.texture = map.Texture;
                picture.rectTransform.Fill();
                picture.raycastTarget = false;

                mapNearImage = UiKit.Rect("Near", mapContent).gameObject.AddComponent<RawImage>();
                mapNearImage.raycastTarget = false;
                mapDetailImage = UiKit.Rect("Detail", mapContent).gameObject.AddComponent<RawImage>();
                mapDetailImage.raycastTarget = false;

                // The grid (its lines on the map, their distances along the view's top and left edges).
                for (var i = 0; i < MapGridLines; i++)
                {
                    mapGrid.Add(UiKit.Image("Grid", mapContent, new Color(1, 1, 1, 0.09f)).rectTransform);
                    var number = UiKit.Label("", mapView, 11, new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, 0.55f), TextAnchor.MiddleCenter).Shadowed();
                    number.rectTransform.sizeDelta = new Vector2(64, 16);
                    mapGridLabels.Add(number);
                }

                // The big places first (they keep their names where names would cover each other).
                var marks = new List<MapMark>(map.Marks);
                marks.Sort((x, y) => y.Major.CompareTo(x.Major));

                foreach (var mark in marks)
                {
                    var dot = UiKit.Image("Mark", mapContent, mark.Major ? UiKit.GoldLit : UiKit.Text, UiKit.Disc);
                    dot.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (mark.Major ? 12 : 7));
                    var label = UiKit.Label(mark.Label, mapContent, mark.Major ? 17 : 13, mark.Major ? UiKit.Text : UiKit.Muted, TextAnchor.MiddleCenter, mark.Major ? FontStyle.Bold : FontStyle.Normal).Shadowed();
                    label.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 24));
                    mapMarks.Add((mark, dot.rectTransform, label));
                }

                // Where the task leads.
                mapQuestMark = UiKit.Image("Quest", mapContent, UiKit.GoldLit, UiKit.Diamond).rectTransform;
                mapQuestMark.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
                UiKit.Image("Heart", mapQuestMark, UiKit.Seal, UiKit.Diamond).rectTransform.Fill(5);
                mapQuestLabel = UiKit.Label("", mapQuestMark, 14, UiKit.GoldLit, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
                mapQuestLabel.rectTransform.anchorMin = mapQuestLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
                mapQuestLabel.rectTransform.pivot = new Vector2(0.5f, 0);
                mapQuestLabel.rectTransform.anchoredPosition = new Vector2(0, 2);
                mapQuestLabel.rectTransform.sizeDelta = new Vector2(260, 20);

                // The mark: a pin, its point on the spot.
                mapPinMark = UiKit.Icon("pin", mapContent, 34, UiKit.Seal).rectTransform;
                mapPinMark.pivot = new Vector2(0.5f, 0.04f);
                var pinHole = UiKit.Image("Hole", mapPinMark, UiKit.OnGold, UiKit.Disc).rectTransform;
                pinHole.At(new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(9, 9));

                var hero = UiKit.Image("Hero", mapContent, UiKit.Seal, UiKit.Arrow);
                mapHero = hero.rectTransform;
                mapHero.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
                mapHero.localEulerAngles = new Vector3(0, 0, -host.HeroFacing * Mathf.Rad2Deg);

                UiKit.Frame(frame, UiKit.GoldDeep);
                var north = UiKit.Label("С", frame, 20, UiKit.GoldLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
                north.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(30, 30));

                // Where the pointer is, in the top right corner.
                mapCursorLine = UiKit.Label("", frame, 14, UiKit.Text, TextAnchor.MiddleRight).Shadowed();
                mapCursorLine.rectTransform.Place(new Vector2(0.5f, 1), Vector2.one, 30, -36, 16, 10);

                // The mark's corner: where it is, and going there.
                mapPinPanel = UiKit.Panel("Pin", frame, UiKit.Hex(0x0E0D0C), UiKit.GoldDeep).rectTransform;
                mapPinPanel.GetComponent<Image>().raycastTarget = true;
                mapPinPanel.Place(new Vector2(0, 1), new Vector2(0, 1), 14, -166, -344, 14);
                var pinIcon = UiKit.Icon("pin", mapPinPanel, 26, UiKit.Seal);
                pinIcon.rectTransform.At(new Vector2(0, 1), new Vector2(26, -26), new Vector2(26, 26));
                UiKit.Caps("Метка", mapPinPanel, 12).rectTransform.Place(new Vector2(0, 1), Vector2.one, 46, -34, 40, 12);
                mapPinLine = UiKit.Label("", mapPinPanel, 20, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                mapPinLine.rectTransform.Place(new Vector2(0, 1), Vector2.one, 16, -66, 16, 38);
                mapPinDetail = UiKit.Label("", mapPinPanel, 14, UiKit.Muted);
                mapPinDetail.rectTransform.Place(new Vector2(0, 1), Vector2.one, 16, -88, 16, 68);
                var teleport = UiKit.Button(UiKit.Spaced("Телепорт"), mapPinPanel, TeleportToPin, ButtonKind.Gold, 15);
                ((RectTransform)teleport.transform).Place(Vector2.zero, new Vector2(1, 0), 14, 12, 120, -50);
                var centreOn = UiKit.Button("", mapPinPanel, CentreMapOnPin, ButtonKind.Plain, 15);
                ((RectTransform)centreOn.transform).Place(new Vector2(1, 0), new Vector2(1, 0), -112, 12, 62, -50);
                UiKit.Icon("ring", centreOn.transform, 20, UiKit.Text).rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
                var remove = UiKit.Button("×", mapPinPanel, () => SetMapPin(null), ButtonKind.Plain, 22);
                ((RectTransform)remove.transform).Place(new Vector2(1, 0), new Vector2(1, 0), -56, 12, 14, -50);

                // The scale: a bar of a round length, and what it is.
                mapScaleBar = UiKit.Image("Scale Bar", frame, UiKit.GoldLit).rectTransform;
                mapScaleBar.anchorMin = mapScaleBar.anchorMax = new Vector2(0, 0);
                mapScaleBar.pivot = new Vector2(0, 0.5f);
                mapScaleBar.anchoredPosition = new Vector2(18, 22);
                mapScaleLine = UiKit.Label("", frame, 14, UiKit.Text, TextAnchor.MiddleLeft).Shadowed();
                mapScaleLine.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 18, 30, 60, -54);
                var help = UiKit.Label("колесо, + и − — масштаб · тянуть — левая кнопка · клик — метка", frame, 13, UiKit.Muted, TextAnchor.MiddleRight).Shadowed();
                help.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 10, 6, 70, -26);

                // The buttons, on the map's right.
                var buttons = UiKit.Rect("Buttons", frame).Place(new Vector2(1, 0), new Vector2(1, 0), -62, 14, 12, -14 - 4 * 52);
                buttons.Stack<VerticalLayoutGroup>(4);

                // Closer, farther, to the hero, the whole mountain.
                foreach (var (text, icon, act) in new (string, string, System.Action)[]
                {
                    ("+", null, () => ZoomMap(mapZoom * 1.6f, Vector2.zero)),
                    ("−", null, () => ZoomMap(mapZoom / 1.6f, Vector2.zero)),
                    ("", "ring", CentreMapOnHero),
                    ("", "map", () => { mapZoom = 1; mapCentre = new Vector2(0.5f, 0.5f); LayOutMap(); }),
                })
                {
                    var button = UiKit.Button(text, buttons, act, ButtonKind.Plain, 26);
                    button.Size(48, 50);

                    if (icon != null)
                    {
                        UiKit.Icon(icon, button.transform, 24, UiKit.Text).rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
                    }
                }

                ShowMapPin();
                LayOutMap();
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

        /// <summary>The map page's wheel, drag and keys, and the fine drawing of what is in sight (each frame while it is open).</summary>
        void UpdateMapPage()
        {
            var map = host.Map;

            if (mapView == null || map == null)
            {
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                // On the map itself, not on a button or the mark's corner over it.
                var over = RectTransformUtility.RectangleContainsScreenPoint(mapView, pointer, null) && PointerOnMap(pointer);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView, pointer, null, out var local);
                var scroll = mouse.scroll.ReadValue().y;

                if (over && Mathf.Abs(scroll) > 0.01f)
                {
                    ZoomMap(mapZoom * (scroll > 0 ? 1.25f : 0.8f), local);
                }

                if (over && mouse.leftButton.wasPressedThisFrame)
                {
                    mapDragging = true;
                    mapDragFrom = local;
                    mapPressedAt = pointer;
                    mapPressMoved = 0;
                }

                if (mapDragging)
                {
                    mapPressMoved = Mathf.Max(mapPressMoved, (pointer - mapPressedAt).magnitude);
                }

                // Let go where it was pressed: a click, the mark put there.
                if (mapDragging && mouse.leftButton.wasReleasedThisFrame && mapPressMoved < MapClickSlop)
                {
                    SetMapPin(MapPointAt(local));
                }

                if (!mouse.leftButton.isPressed)
                {
                    mapDragging = false;
                }

                if (mapDragging && mapPressMoved >= MapClickSlop && local != mapDragFrom)
                {
                    mapCentre -= (local - mapDragFrom) / (MapSize * mapZoom);
                    mapDragFrom = local;
                    LayOutMap();
                }
                else if (mapDragging)
                {
                    mapDragFrom = local;
                }

                if (mapCursorLine != null)
                {
                    var under = MapPointAt(local);
                    mapCursorLine.text = over ? $"<color=#A69A86>X</color> {under.x:0}   <color=#A69A86>Z</color> {under.z:0}" : "";
                }
            }

            if (keyboard != null)
            {
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                {
                    ZoomMap(mapZoom * 1.6f, Vector2.zero);
                }
                else if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                {
                    ZoomMap(mapZoom / 1.6f, Vector2.zero);
                }
            }

            DrawMapDetail(map);
        }

        /// <summary>Zooms to `zoom`, keeping the point of the map under `at` (the view's own units from its middle) where it is.</summary>
        void ZoomMap(float zoom, Vector2 at)
        {
            zoom = Mathf.Clamp(zoom, 1, MapZoomMax);
            var under = mapCentre + at / (MapSize * mapZoom);
            mapZoom = zoom;
            mapCentre = under - at / (MapSize * mapZoom);
            LayOutMap();
        }

        void CentreMapOnHero()
        {
            if (host.Map == null)
            {
                return;
            }

            mapZoom = Mathf.Max(mapZoom, 6);
            mapCentre = host.Map.ToMap(host.HeroAt);
            LayOutMap();
        }

        void CentreMapOnPin()
        {
            if (host.Map == null || !MapPin.HasValue)
            {
                return;
            }

            mapZoom = Mathf.Max(mapZoom, 4);
            mapCentre = host.Map.ToMap(MapPin.Value);
            LayOutMap();
        }

        /// <summary>The topmost thing of the menus under the pointer is the map's view (not a button or a panel over it).</summary>
        bool PointerOnMap(Vector2 pointer)
        {
            var events = EventSystem.current;

            if (events == null)
            {
                return true;
            }

            mapHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = pointer }, mapHits);

            return mapHits.Count > 0 && mapHits[0].gameObject == mapView.gameObject;
        }

        /// <summary>The world's point (x, z; y its ground's height where it is known, else NaN) under a point of the view (its own units from its middle).</summary>
        Vector3 MapPointAt(Vector2 local)
        {
            var map = host.Map;
            var uv = mapCentre + local / (MapSize * mapZoom);
            var x = map.Area.x + uv.x * map.Area.width;
            var z = map.Area.y + uv.y * map.Area.height;

            return new Vector3(x, QingMao.GroundAt(new Vector3(x, float.NaN, z)), z);
        }

        /// <summary>Puts the mark at a point (or takes it off, null) and shows it in its corner.</summary>
        void SetMapPin(Vector3? at)
        {
            MapPin = at;
            ShowMapPin();
            LayOutMap();
        }

        void ShowMapPin()
        {
            if (mapPinPanel == null)
            {
                return;
            }

            mapPinPanel.gameObject.SetActive(MapPin.HasValue);
            mapPinMark.gameObject.SetActive(MapPin.HasValue);

            if (!MapPin.HasValue)
            {
                return;
            }

            var pin = MapPin.Value;
            var hero = host.HeroAt;
            var distance = new Vector2(pin.x - hero.x, pin.z - hero.z).magnitude;
            mapPinLine.text = $"<color=#A69A86>X</color> {pin.x:0}   <color=#A69A86>Z</color> {pin.z:0}";
            mapPinDetail.text = (float.IsNaN(pin.y) ? "" : $"высота {pin.y:0} м · ") + $"от вас {Distance(distance)}";
        }

        /// <summary>"640 м", "2,4 км".</summary>
        static string Distance(float metres) => metres >= 1000 ? $"{metres / 1000:0.0} км" : $"{metres:0} м";

        /// <summary>Puts the mark at a point of the world (the checks' `-pin`).</summary>
        public void PinMap(float x, float z) => SetMapPin(new Vector3(x, QingMao.GroundAt(new Vector3(x, float.NaN, z)), z));

        public void TeleportToMapPin() => TeleportToPin();

        /// <summary>The hero put on the mark at once (on the ground there), the map closed.</summary>
        void TeleportToPin()
        {
            if (!MapPin.HasValue)
            {
                return;
            }

            var pin = MapPin.Value;
            host.Resume();
            host.Teleport(new Vector3(pin.x, float.NaN, pin.z));
            Notify($"Перенесены к метке: {pin.x:0}, {pin.z:0}");
        }

        /// <summary>Where a point of the map (0…1) falls in the content, from its middle.</summary>
        Vector2 OnMap(Vector2 uv) => (uv - new Vector2(0.5f, 0.5f)) * (MapSize * mapZoom);

        /// <summary>Puts the map, its finer drawings, the places and the hero where the zoom and the drag have them.</summary>
        void LayOutMap()
        {
            var map = host.Map;

            if (mapView == null || map == null)
            {
                return;
            }

            // Never past the map's edges.
            var half = 0.5f / mapZoom;
            mapCentre = new Vector2(Mathf.Clamp(mapCentre.x, half, 1 - half), Mathf.Clamp(mapCentre.y, half, 1 - half));
            var size = MapSize * mapZoom;
            mapContent.At(new Vector2(0.5f, 0.5f), -OnMap(mapCentre), new Vector2(size, size));
            mapSettledAt = Time.unscaledTime;

            PlaceOver(mapNearImage, host.NearMap, map, mapZoom >= 1.8f);
            PlaceOver(mapDetailImage, mapDetail, map, mapZoom >= MapDetailFrom);

            LayOutMapGrid(map, size);

            // The places: the big ones' names always, the others' where they fit (closer in, they all do); a name
            // at the view's edge is moved in to be read whole.
            var taken = new List<Rect>();
            var inView = OnMap(mapCentre);

            foreach (var (mark, dot, label) in mapMarks)
            {
                var at = OnMap(map.ToMap(mark.At));
                dot.anchoredPosition = at;
                var letters = label.fontSize;
                var width = mark.Label.Length * letters * 0.56f;
                var edge = MapSize * 0.5f - 6 - width * 0.5f;
                var x = Mathf.Abs(at.x - inView.x) < MapSize * 0.5f ? Mathf.Clamp(at.x, inView.x - edge, inView.x + edge) : at.x;
                label.rectTransform.anchoredPosition = new Vector2(x, at.y + 16);
                var box = new Rect(x - width * 0.5f, at.y + 6, width, letters + 6);
                var fits = !taken.Exists(r => r.Overlaps(box));
                label.gameObject.SetActive(fits);

                if (fits)
                {
                    taken.Add(box);
                }
            }

            if (mapQuestMark != null)
            {
                var quest = host.QuestTarget;
                mapQuestMark.gameObject.SetActive(quest.HasValue);

                if (quest.HasValue)
                {
                    mapQuestMark.anchoredPosition = OnMap(map.ToMap(quest.Value.at));
                    mapQuestLabel.text = "Задание: " + quest.Value.what;
                    mapQuestMark.SetAsLastSibling();
                }
            }

            if (mapPinMark != null && MapPin.HasValue)
            {
                mapPinMark.anchoredPosition = OnMap(map.ToMap(MapPin.Value));
                mapPinMark.SetAsLastSibling();
            }

            mapHero.anchoredPosition = OnMap(map.ToMap(host.HeroAt));
            mapHero.SetAsLastSibling();

            // A bar of a round length, about a sixth of the view.
            var metresPerUnit = map.Area.width / size;
            var want = metresPerUnit * MapSize / 6;
            var step = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(want)));
            var nice = want / step >= 5 ? 5 * step : want / step >= 2 ? 2 * step : step;
            mapScaleBar.sizeDelta = new Vector2(nice / metresPerUnit, 4);
            mapScaleLine.text = (nice >= 1000 ? $"{nice / 1000:0.#} км" : $"{nice:0} м") + $"   ·   ×{mapZoom:0.#}";
        }

        /// <summary>
        /// The grid: lines a round distance apart (some 140 points of the screen), across the part in sight, each
        /// with its coordinate at the view's top (x) or left (z) edge.
        /// </summary>
        void LayOutMapGrid(LocalMap map, float size)
        {
            var want = map.Area.width / size * 140;
            var step = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(want)));
            var spacing = want / step >= 5 ? 5 * step : want / step >= 2 ? 2 * step : step;
            var half = 0.5f / mapZoom;
            var inView = OnMap(mapCentre);
            var used = 0;

            for (var axis = 0; axis < 2; axis++)
            {
                var start = axis == 0 ? map.Area.x : map.Area.y;
                var span = axis == 0 ? map.Area.width : map.Area.height;
                var middle = axis == 0 ? mapCentre.x : mapCentre.y;
                var lo = start + (middle - half) * span;
                var hi = start + (middle + half) * span;

                for (var v = Mathf.Ceil(lo / spacing) * spacing; v <= hi && used < mapGrid.Count; v += spacing)
                {
                    var line = mapGrid[used];
                    var number = mapGridLabels[used];
                    used++;
                    line.gameObject.SetActive(true);
                    number.gameObject.SetActive(true);
                    var p = ((v - start) / span - 0.5f) * size;
                    number.text = spacing >= 1000 ? $"{v / 1000:0.#} км" : $"{v:0}";

                    if (axis == 0)
                    {
                        line.At(new Vector2(0.5f, 0.5f), new Vector2(p, 0), new Vector2(1, size));
                        number.rectTransform.At(new Vector2(0.5f, 1), new Vector2(p - inView.x, -46), new Vector2(64, 16));
                    }
                    else
                    {
                        line.At(new Vector2(0.5f, 0.5f), new Vector2(0, p), new Vector2(size, 1));
                        number.rectTransform.At(new Vector2(0, 0.5f), new Vector2(34, p - inView.y), new Vector2(64, 16));
                    }
                }
            }

            for (var i = used; i < mapGrid.Count; i++)
            {
                mapGrid[i].gameObject.SetActive(false);
                mapGridLabels[i].gameObject.SetActive(false);
            }
        }

        /// <summary>A finer map laid over the whole one where it lies (hidden when there is none, or not this close).</summary>
        void PlaceOver(RawImage image, LocalMap over, LocalMap map, bool shown)
        {
            image.enabled = shown && over != null && over.Texture != null;

            if (!image.enabled)
            {
                return;
            }

            image.texture = over.Texture;
            var min = OnMap(map.ToMap(new Vector3(over.Area.xMin, 0, over.Area.yMin)));
            var max = OnMap(map.ToMap(new Vector3(over.Area.xMax, 0, over.Area.yMax)));
            image.rectTransform.At(new Vector2(0.5f, 0.5f), (min + max) / 2, max - min);
        }

        /// <summary>
        /// Close in, once the view has stood still a moment: the part in sight (and some round it) drawn finely, unless
        /// what is drawn already covers it closely enough. Drawn a few rows a frame; the old one stays until it is done.
        /// </summary>
        void DrawMapDetail(LocalMap map)
        {
            if (mapPainting != null)
            {
                var done = mapPainting.Step(MapDetailRowsPerFrame);

                if (done != null)
                {
                    if (mapDetail != null)
                    {
                        Destroy(mapDetail.Texture);
                    }

                    mapDetail = done;
                    mapPainting = null;
                    LayOutMap();
                }

                return;
            }

            if (mapZoom < MapDetailFrom || Time.unscaledTime - mapSettledAt < 0.25f || WorldPlan.Current == null)
            {
                return;
            }

            // The ground in sight, in metres.
            var seen = map.Area.width / mapZoom;
            var middle = new Vector2(map.Area.x + mapCentre.x * map.Area.width, map.Area.y + mapCentre.y * map.Area.height);
            var sight = new Rect(middle - Vector2.one * seen / 2, Vector2.one * seen);

            bool Covers(LocalMap fine) => fine != null && fine.Area.Contains(sight.min) && fine.Area.Contains(sight.max) &&
                                          fine.Area.width / MapDetailSize <= seen / MapSize * 1.6f;

            if (Covers(mapDetail) || (mapZoom < 6 && host.NearMap != null && host.NearMap.Area.Contains(sight.min) && host.NearMap.Area.Contains(sight.max)))
            {
                return;
            }

            // Half again round what is seen, so that a little dragging does not need it drawn again.
            var span = seen * 1.5f;
            var area = new Rect(middle - Vector2.one * span / 2, Vector2.one * span);
            mapPainting = new LocalMap.Painting(area, MapDetailSize, WorldPlan.Current, true);
        }
    }
}
