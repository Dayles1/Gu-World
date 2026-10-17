using System.Collections.Generic;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The chat (T, or / to start a command), as Minecraft has it: the log of what happened
    /// over a line to type in. A line beginning with / is a command (Commands); as one types,
    /// what fits is listed above the line with how the command is written, Tab puts the chosen
    /// one in, ↑ ↓ go through the list (or, with none, through the lines sent before), Enter
    /// sends, Esc closes. The world goes on meanwhile.
    /// </summary>
    public partial class Menus
    {
        const int ChatLines = 12;
        const int ShownSuggestions = 8;

        InputField chatField;
        Text chatUsage;
        RectTransform suggestionBox;
        readonly List<Text> suggestionRows = new List<Text>();
        List<string> suggestions = new List<string>();
        int suggestionChosen;
        string suggestedFor;
        /// <summary>Lines sent before (newest last), and how far back ↑ has gone (−1: not at all).</summary>
        readonly List<string> sent = new List<string>();
        int sentBack = -1;
        bool focusChat;
        string chatStart = "";

        /// <summary>Opens the chat; `start` is put in the line ("/" for a command).</summary>
        public void OpenChat(string start)
        {
            chatStart = start ?? "";
            Show(Page.Chat);
        }

        void ChatPage()
        {
            var panel = UiKit.Image("Chat", pageRoot, UiKit.Hex(0x0B0A0A, 0.5f), UiKit.Chamfer).rectTransform;
            panel.Place(Vector2.zero, Vector2.zero, 24, 104, -820, -440);
            var lines = UiKit.Rect("Lines", panel).Place(Vector2.zero, Vector2.one, 16, 64, 16, 10);
            lines.Stack<VerticalLayoutGroup>(2, null, TextAnchor.LowerLeft);
            var first = Mathf.Max(0, log.Count - ChatLines);

            for (var i = first; i < log.Count; i++)
            {
                var (text, _) = log[i];
                var line = UiKit.Label(ChatLine(text), lines, 17, UiKit.Text).Shadowed();
                line.Size(24);
            }

            var field = UiKit.Field(panel, chatStart, "Сообщение или /команда — Tab дополнит", 160, OnChatTyped);
            field.GetComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)field.transform).Place(Vector2.zero, new Vector2(1, 0), 8, 8, 8, -54);
            field.textComponent.supportRichText = false;
            chatField = field;

            chatUsage = UiKit.Label("", panel, 15, UiKit.Muted).Shadowed();
            chatUsage.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), 12, 4, 8, -26);

            suggestionBox = UiKit.Image("Suggestions", panel, UiKit.Hex(0x0B0A0A, 0.94f), UiKit.Chamfer).rectTransform;
            UiKit.Frame(suggestionBox, UiKit.Line);
            suggestionBox.anchorMin = suggestionBox.anchorMax = new Vector2(0, 0);
            suggestionBox.pivot = new Vector2(0, 0);
            suggestionBox.anchoredPosition = new Vector2(8, 62);
            suggestionRows.Clear();

            for (var i = 0; i < ShownSuggestions; i++)
            {
                var row = UiKit.Label("", suggestionBox, 17, UiKit.Text);
                row.rectTransform.anchorMin = row.rectTransform.anchorMax = new Vector2(0, 0);
                row.rectTransform.pivot = new Vector2(0, 0);
                row.rectTransform.sizeDelta = new Vector2(420, 24);
                row.rectTransform.anchoredPosition = new Vector2(12, 6 + i * 24);
                suggestionRows.Add(row);
            }

            suggestedFor = null;
            sentBack = -1;
            focusChat = true;
            RefreshSuggestions();
        }

        /// <summary>A log line as the chat shows it: a command sent in gold, a refusal in red.</summary>
        static string ChatLine(string text) =>
            text.StartsWith("> ") ? $"<color=#C9A563>{text}</color>" : text.StartsWith("!") ? $"<color=#E08A80>{text.Substring(1)}</color>" : text;

        void OnChatTyped(string text)
        {
            if (text != suggestedFor)
            {
                RefreshSuggestions();
            }
        }

        void RefreshSuggestions()
        {
            if (chatField == null)
            {
                return;
            }

            var text = chatField.text ?? "";
            suggestedFor = text;
            suggestions.Clear();
            string usage = null;

            if (Commands.Normalize(text).StartsWith("/") && host.Commands != null)
            {
                suggestions = host.Commands.Suggest(text, out usage);
            }

            suggestionChosen = 0;
            chatUsage.text = usage ?? (text.Length == 0 ? "Enter — отправить · Esc — закрыть · /help — команды" : "");
            ShowSuggestions();
        }

        void ShowSuggestions()
        {
            var shown = Mathf.Min(ShownSuggestions, suggestions.Count);
            suggestionBox.gameObject.SetActive(shown > 0);
            // The chosen one stays in the window as one goes down a long list.
            var start = Mathf.Clamp(suggestionChosen - shown + 1, 0, Mathf.Max(0, suggestions.Count - shown));
            var width = 200f;

            for (var i = 0; i < ShownSuggestions; i++)
            {
                // The first at the foot, next to the line.
                var row = suggestionRows[i];
                var index = start + i;

                if (i >= shown)
                {
                    row.text = "";

                    continue;
                }

                var chosen = index == suggestionChosen;
                row.text = suggestions[index];
                row.color = chosen ? UiKit.GoldLit : UiKit.Text;
                row.fontStyle = chosen ? FontStyle.Bold : FontStyle.Normal;
                width = Mathf.Max(width, row.preferredWidth + 28);
            }

            suggestionBox.sizeDelta = new Vector2(width, shown * 24 + 12);
        }

        /// <summary>The chat's keys each frame: Tab, ↑ ↓, Enter, Esc.</summary>
        void UpdateChat()
        {
            var keyboard = Keyboard.current;

            if (chatField == null || keyboard == null)
            {
                return;
            }

            // Focused a frame after opening, so the key that opened it is not typed in.
            if (focusChat)
            {
                focusChat = false;
                chatField.ActivateInputField();
                chatField.text = chatStart;
                chatField.caretPosition = chatField.text.Length;
                RefreshSuggestions();

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                host.CloseChat();

                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame && suggestions.Count > 0)
            {
                chatField.text = Commands.Complete(chatField.text, suggestions[suggestionChosen]);
                chatField.caretPosition = chatField.text.Length;
                RefreshSuggestions();
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                var up = keyboard.upArrowKey.wasPressedThisFrame;

                if (suggestions.Count > 0)
                {
                    suggestionChosen = (suggestionChosen + (up ? 1 : -1) + suggestions.Count) % suggestions.Count;
                    ShowSuggestions();
                }
                else if (sent.Count > 0)
                {
                    sentBack = Mathf.Clamp(sentBack + (up ? 1 : -1), -1, sent.Count - 1);
                    chatField.text = sentBack < 0 ? "" : sent[sent.Count - 1 - sentBack];
                    chatField.caretPosition = chatField.text.Length;
                    suggestedFor = chatField.text;
                }
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                SendChat(chatField.text);
            }

            // The field keeps the focus (a click elsewhere would take it).
            if (!chatField.isFocused)
            {
                chatField.ActivateInputField();
            }
        }

        void SendChat(string text)
        {
            text = (text ?? "").Trim();

            if (text.Length > 0)
            {
                sent.Add(text);

                if (Commands.Normalize(text).StartsWith("/"))
                {
                    Notify("> " + text);
                    var said = host.Commands?.Run(text);

                    if (!string.IsNullOrEmpty(said))
                    {
                        Notify(said);
                    }
                }
                else
                {
                    Notify($"<color=#F0D08A>{host.Look.FullName}:</color> {text}");
                }
            }

            host.CloseChat();
        }
    }
}
