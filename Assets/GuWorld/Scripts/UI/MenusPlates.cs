using System.Collections.Generic;
using GuWorld.Creatures;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// Who the beasts are, over their heads: each wolf near (or hunting the hero, or just struck) has its name, its
    /// level and its health over it — the beast kings' names in their rung's colour, a thunder shield turning the bar
    /// blue, a berserk king's name red. A thousand or myriad beast king that hunts the hero has its bar across the top of
    /// the screen as well.
    /// </summary>
    public partial class Menus
    {
        const int PlateCount = 16;

        readonly List<(RectTransform root, Text name, RectTransform fill, Image fillImage)> plates = new List<(RectTransform, Text, RectTransform, Image)>();
        RectTransform bossBar;
        Text bossName;
        Text bossState;
        RectTransform bossFill;
        Image bossFillImage;
        Camera plateCamera;

        static readonly Color[] TierColours =
        {
            UiKit.Hex(0xE6DCC8),
            UiKit.Hex(0xF0D08A),
            UiKit.Hex(0xF0A050),
            UiKit.Hex(0xE86A8A),
        };

        void BuildPlates()
        {
            var layer = UiKit.Rect("Plates", hud).Fill();
            layer.SetAsFirstSibling();

            for (var i = 0; i < PlateCount; i++)
            {
                var root = UiKit.Rect("Plate", layer);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.sizeDelta = new Vector2(170, 34);
                var name = UiKit.Label("", root, 14, UiKit.Text, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
                name.rectTransform.Place(Vector2.zero, Vector2.one, -40, 10, -40, 0);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                var back = UiKit.Image("Bar", root, UiKit.Hex(0x000000, 0.6f));
                back.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 25, 0, 25, -7);
                var fill = UiKit.Image("Fill", back.transform, UiKit.Danger);
                fill.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 1, 1, 1, 1);
                root.gameObject.SetActive(false);
                plates.Add((root, name, fill.rectTransform, fill));
            }

            bossBar = UiKit.Rect("Boss", hud).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), -380, -96, -380, 30);
            bossName = UiKit.Label("", bossBar, 22, TierColours[3], TextAnchor.UpperCenter, FontStyle.Bold).Shadowed();
            bossName.rectTransform.Place(Vector2.zero, Vector2.one, 0, 26, 0, 0);
            var bossBack = UiKit.Image("Bar", bossBar, UiKit.Hex(0x000000, 0.65f), UiKit.Chamfer);
            bossBack.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 0, 4, 0, -24);
            UiKit.Frame(bossBack.transform, UiKit.GoldDeep);
            bossFillImage = UiKit.Image("Fill", bossBack.transform, UiKit.Danger);
            bossFill = bossFillImage.rectTransform;
            bossFill.Place(Vector2.zero, new Vector2(0, 1), 3, 3, 3, 3);
            bossState = UiKit.Label("", bossBack.transform, 12, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            bossState.rectTransform.Fill();
            bossBar.gameObject.SetActive(false);
        }

        void UpdatePlates()
        {
            if (plates.Count == 0)
            {
                return;
            }

            if (plateCamera == null && host.View != null)
            {
                plateCamera = host.View.GetComponent<Camera>();
            }

            var used = 0;
            IFoe boss = null;
            var bossDistance = float.PositiveInfinity;
            var playing = Current == Page.None;

            if (plateCamera != null && playing)
            {
                var eye = plateCamera.transform.position;
                var canvasRect = (RectTransform)hud;

                foreach (var wolf in Foes.All)
                {
                    if (!(wolf is Object alive) || alive == null || wolf.Dead || used >= plates.Count)
                    {
                        continue;
                    }

                    var top = wolf.transform.position + Vector3.up * (wolf.Height + 0.45f);
                    var distance = Vector3.Distance(eye, top);
                    var near = distance < 30 + 15 * wolf.Tier || wolf.Hunting || Time.time - wolf.StruckAt < 8;

                    if (wolf.Tier >= 2 && wolf.Hunting && distance < 90 && distance < bossDistance)
                    {
                        boss = wolf;
                        bossDistance = distance;
                    }

                    if (!near || distance > 70)
                    {
                        continue;
                    }

                    var screen = plateCamera.WorldToScreenPoint(top);

                    if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                    {
                        continue;
                    }

                    var (root, name, fill, fillImage) = plates[used++];
                    root.gameObject.SetActive(true);
                    root.anchoredPosition = local + new Vector2(0, 10);
                    // Smaller far off, never too small to read.
                    root.localScale = Vector3.one * Mathf.Clamp(16 / Mathf.Max(1, distance), 0.95f, 1.2f);
                    var colour = wolf.Tamed ? UiKit.EssenceLit : wolf.Berserk ? UiKit.Danger : TierColours[Mathf.Clamp(wolf.Tier, 0, 3)];
                    name.text = $"{wolf.Title} <size=11>· {(wolf is GuMaster ? wolf.Rung : $"ур. {wolf.Level}")}</size>" + (wolf.Tamed ? " <size=11>· ваш</size>" : "");
                    name.color = colour;
                    fill.anchorMax = new Vector2(Mathf.Clamp01(wolf.Health / wolf.MaxHealth), 1);
                    fillImage.color = wolf.Shielded ? UiKit.Hex(0x6FA8FF) : wolf.Tamed ? UiKit.Essence : UiKit.Danger;
                }
            }

            for (var i = used; i < plates.Count; i++)
            {
                if (plates[i].root.gameObject.activeSelf)
                {
                    plates[i].root.gameObject.SetActive(false);
                }
            }

            bossBar.gameObject.SetActive(boss != null && playing);

            if (boss != null)
            {
                bossName.text = $"{boss.Title}  <size=15><color=#A69A86>{boss.Rung}{(boss is GuMaster ? "" : $" · уровень {boss.Level}")}</color></size>";
                bossName.color = TierColours[Mathf.Clamp(boss.Tier, 0, 3)];
                bossFill.anchorMax = new Vector2(Mathf.Clamp01(boss.Health / boss.MaxHealth), 1);
                bossFillImage.color = boss.Shielded ? UiKit.Hex(0x6FA8FF) : boss.Berserk ? UiKit.Hex(0xE0533F) : UiKit.Danger;
                bossState.text = $"{Mathf.CeilToInt(boss.Health)} / {Mathf.CeilToInt(boss.MaxHealth)}" + boss.State;
            }
        }
    }
}
