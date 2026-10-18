using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// Finding one's way (part of the HUD): where the task in hand leads — a gold
    /// mark over it in the world (at the screen's edge, toward it, when it is out
    /// of sight), on the minimap (MenusHud) and on the map (MenusMap), and in the
    /// task's card how far and which way; a banner across the top for what
    /// matters (a task taken or done, a death); and, until the player has done
    /// each once, a line of advice on the keys.
    /// </summary>
    public partial class Menus
    {
        /// <summary>How long a banner stays, s (it fades in and out besides).</summary>
        const float BannerFor = 3.5f;
        /// <summary>The world mark keeps this far in from the screen's edges, points.</summary>
        static readonly Vector2 BeaconMargin = new Vector2(70, 110);

        RectTransform questBeacon;
        Image questBeaconMark;
        Text questBeaconLine;
        Text questWhere;
        RectTransform minimapQuest;
        CanvasGroup banner;
        Text bannerTitle;
        Text bannerLine;
        float bannerAt = float.NegativeInfinity;
        RectTransform hintBox;
        Text hintLine;

        void BuildGuide()
        {
            // The mark over the task's place: a gold diamond, how far under it.
            questBeacon = UiKit.Rect("Quest Beacon", hud);
            questBeacon.anchorMin = questBeacon.anchorMax = new Vector2(0.5f, 0.5f);
            questBeacon.sizeDelta = new Vector2(28, 28);
            var shade = UiKit.Image("Shade", questBeacon, new Color(0, 0, 0, 0.55f), UiKit.Diamond);
            shade.rectTransform.Fill(-3);
            questBeaconMark = UiKit.Image("Mark", questBeacon, UiKit.GoldLit, UiKit.Diamond);
            questBeaconMark.rectTransform.Fill();
            UiKit.Image("Heart", questBeacon, UiKit.Seal, UiKit.Diamond).rectTransform.Fill(9);
            questBeaconLine = UiKit.Label("", questBeacon, 15, UiKit.GoldLit, TextAnchor.UpperCenter, FontStyle.Bold).Shadowed();
            questBeaconLine.rectTransform.anchorMin = questBeaconLine.rectTransform.anchorMax = new Vector2(0.5f, 0);
            questBeaconLine.rectTransform.pivot = new Vector2(0.5f, 1);
            questBeaconLine.rectTransform.anchoredPosition = new Vector2(0, -4);
            questBeaconLine.rectTransform.sizeDelta = new Vector2(220, 22);
            questBeacon.gameObject.SetActive(false);

            // The banner: a dark band across the top with gold rules, a title and a line under it.
            var band = UiKit.Rect("Banner", hud).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), -460, -290, -460, 180);
            banner = band.gameObject.AddComponent<CanvasGroup>();
            banner.blocksRaycasts = false;
            banner.interactable = false;
            UiKit.Image("Back", band, UiKit.Hex(0x0E0D0C, 0.74f), UiKit.Chamfer).rectTransform.Fill();
            UiKit.Image("Rule", band, UiKit.GoldDeep).rectTransform.Place(new Vector2(0, 1), Vector2.one, 120, -2, 120, 0);
            UiKit.Image("Rule", band, UiKit.GoldDeep).rectTransform.Place(Vector2.zero, new Vector2(1, 0), 120, 0, 120, -2);
            bannerTitle = UiKit.Label("", band, 34, UiKit.GoldLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            bannerTitle.rectTransform.Place(new Vector2(0, 0.42f), Vector2.one, 20, 0, 20, 6);
            bannerLine = UiKit.Label("", band, 18, UiKit.Text, TextAnchor.MiddleCenter).Shadowed();
            bannerLine.rectTransform.Place(Vector2.zero, new Vector2(1, 0.45f), 20, 8, 20, 0);
            banner.gameObject.SetActive(false);

            // The advice, at the top in the middle.
            hintBox = UiKit.Panel("Hint", hud, UiKit.Hex(0x0E0D0C, 0.78f), UiKit.GoldDeep).rectTransform;
            hintBox.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), -400, -96, -400, 40);
            UiKit.Caps("Совет", hintBox, 12).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), 18, 0, -90, 0);
            hintLine = UiKit.Label("", hintBox, 17, UiKit.Text, TextAnchor.MiddleLeft).Shadowed();
            hintLine.rectTransform.Place(Vector2.zero, Vector2.one, 104, 0, 16, 0);
            hintBox.gameObject.SetActive(false);
        }

        /// <summary>A banner across the top for a few seconds: what happened, and a line under it.</summary>
        public void Banner(string title, string line = null)
        {
            if (bannerTitle == null)
            {
                return;
            }

            bannerTitle.text = title ?? "";
            bannerLine.text = line ?? "";
            bannerAt = Time.unscaledTime;
        }

        /// <summary>"к северо-западу": which way a stretch on the ground goes (+x east, +z north, as on the map).</summary>
        static string Toward(Vector2 offset)
        {
            var angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            var sector = Mathf.RoundToInt(Mathf.Repeat(angle, 360) / 45) % 8;

            return sector switch
            {
                0 => "к северу",
                1 => "к северо-востоку",
                2 => "к востоку",
                3 => "к юго-востоку",
                4 => "к югу",
                5 => "к юго-западу",
                6 => "к западу",
                _ => "к северо-западу",
            };
        }

        void UpdateGuide()
        {
            var target = host.QuestTarget;
            var hero = host.HeroAt;
            var playing = Current == Page.None;

            // The task's card: who or what, how far, which way.
            if (questWhere != null)
            {
                if (target.HasValue)
                {
                    var (at, what) = target.Value;
                    var offset = new Vector2(at.x - hero.x, at.z - hero.z);
                    questWhere.text = offset.magnitude < 6 ? $"{what} · здесь" : $"{what} · {Distance(offset.magnitude)} {Toward(offset)}";
                }
                else
                {
                    questWhere.text = "";
                }
            }

            // The mark in the world: over the place (a person's head), smaller far off; out of sight, at the edge toward it.
            var shown = false;
            var camera = Camera.main;

            if (target.HasValue && camera != null && playing)
            {
                var (at, _) = target.Value;
                var far = Vector3.Distance(hero, at);

                if (far > 4)
                {
                    var screen = camera.WorldToScreenPoint(at + Vector3.up * 2.3f);
                    var behind = screen.z < 0;

                    if (behind)
                    {
                        screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, -screen.z);
                    }

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, screen, null, out var local);
                    var half = hud.rect.size * 0.5f - BeaconMargin;
                    var outside = behind || Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y;

                    if (outside)
                    {
                        if (behind && Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y)
                        {
                            local.y = -half.y;
                        }

                        var scale = Mathf.Min(half.x / Mathf.Max(1, Mathf.Abs(local.x)), half.y / Mathf.Max(1, Mathf.Abs(local.y)));
                        local *= Mathf.Min(1, scale);
                    }

                    questBeacon.anchoredPosition = local;
                    var size = outside ? 20 : Mathf.Lerp(30, 18, Mathf.InverseLerp(10, 250, far));
                    questBeacon.sizeDelta = new Vector2(size, size);
                    questBeaconMark.color = outside ? new Color(UiKit.GoldLit.r, UiKit.GoldLit.g, UiKit.GoldLit.b, 0.75f) : UiKit.GoldLit;
                    questBeaconLine.text = Distance(far);
                    shown = true;
                }
            }

            if (questBeacon.gameObject.activeSelf != shown)
            {
                questBeacon.gameObject.SetActive(shown);
            }

            // The banner: in, held, out.
            var since = Time.unscaledTime - bannerAt;
            var alpha = since < 0.25f ? since / 0.25f : since < BannerFor ? 1 : 1 - (since - BannerFor) / 0.8f;
            alpha = Mathf.Clamp01(alpha);

            if (banner.gameObject.activeSelf != alpha > 0)
            {
                banner.gameObject.SetActive(alpha > 0);
            }

            banner.alpha = alpha;

            // The advice, while playing.
            var hint = playing ? host.Hint : null;

            if (hintBox.gameObject.activeSelf != (hint != null))
            {
                hintBox.gameObject.SetActive(hint != null);
            }

            if (hint != null && hintLine.text != hint)
            {
                hintLine.text = hint;
            }
        }
    }
}
