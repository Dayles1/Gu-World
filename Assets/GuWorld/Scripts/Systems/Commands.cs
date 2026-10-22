using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GuWorld.Cultivation;
using GuWorld.Engine;
using GuWorld.Items;
using UnityEngine;

namespace GuWorld.Systems
{
    /// <summary>What the chat's commands act on: the game.</summary>
    public interface ICommandHost
    {
        WorldClock Clock { get; }
        Cultivator Cultivator { get; }
        Health Health { get; }
        Quests Quests { get; }
        Vector3 HeroAt { get; }
        /// <summary>Places one can go to by name (settlements, the peak, the village's squares and gates…), X and Z.</summary>
        IReadOnlyDictionary<string, Vector3> Places { get; }
        void Teleport(Vector3 at);
        /// <summary>The weather held (null: the world's own).</summary>
        void SetWeather(GuWorld.World.Sky? sky);
        bool FirstPerson { get; set; }
        bool Hitboxes { get; set; }
        /// <summary>The hero dies (and the game begins again).</summary>
        void Kill();
        /// <summary>Wolves brought near the hero; how many came.</summary>
        int SpawnWolves(int count);
        /// <summary>A pack of a kind of wolf brought near the hero (a beast king with its pack); how many came.</summary>
        int SpawnPack(GuWorld.Creatures.BeastKind kind, int count);
        int SpawnMasters(string clan, int rank, int count);
        void StartTide(bool great);
        /// <summary>Whether the admin mode is on: the commands that change the game (cheats) work only then.</summary>
        bool Admin { get; }
    }

    /// <summary>A command of the chat: its name (and others it answers to), how it is written, what it does.</summary>
    public class Command
    {
        public string Name;
        public string[] Aliases = new string[0];
        public string Usage;
        public string About;
        /// <summary>What may come at an argument (its index), given the arguments so far.</summary>
        public Func<string[], int, IEnumerable<string>> Suggest = (_, _) => Enumerable.Empty<string>();
        /// <summary>Does it; what to tell (an error begins with "!").</summary>
        public Func<string[], string> Run;
    }

    /// <summary>
    /// The chat's commands (T or / opens the chat), the way Minecraft has them: `/time set night`,
    /// `/tp gu_yue`, `/give primeval_stone 20`… As one types, what fits is suggested — the commands,
    /// then each argument's choices (places, things, hours) — and Tab completes it. Each command
    /// also answers to a Russian name (/время, /тп…), and a line typed with the Russian layout by
    /// mistake (".ез 100 200") is read as the keys meant ("/tp 100 200").
    /// </summary>
    public class Commands
    {
        readonly ICommandHost host;
        readonly List<Command> all = new List<Command>();

        public IReadOnlyList<Command> All => all;

        /// <summary>The commands that change the game (they need the admin mode); the rest only look or set the view.</summary>
        static readonly HashSet<string> Cheats = new HashSet<string>
        {
            "time", "tp", "weather", "give", "heal", "essence", "rank", "talent", "gu", "kill", "wolves", "tide", "masters",
        };

        public Commands(ICommandHost host)
        {
            this.host = host;
            Build();
        }

        // ── Running ───────────────────────────────────────────────────

        /// <summary>Runs a line ("/time set 6"); what to tell, an error beginning with "!".</summary>
        public string Run(string line)
        {
            var words = Words(Normalize(line));

            if (words.Length == 0)
            {
                return null;
            }

            var command = Find(words[0]);

            if (command == null)
            {
                return $"!Нет такой команды: /{words[0]}. Список — /help";
            }

            // A cheat (anything that changes the game, not only looks at it) waits for the admin mode.
            if (!host.Admin && (Cheats.Contains(command.Name) || (command.Name == "quest" && words.Length > 1)))
            {
                return $"!/{command.Name} — команда режима администратора. Включите его в админ-панели (F10)";
            }

            try
            {
                return command.Run(words.Skip(1).ToArray());
            }
            catch (Exception error)
            {
                Debug.LogWarning($"GU World: /{command.Name} failed: {error}");

                return $"!Не вышло: {command.Usage}";
            }
        }

        /// <summary>
        /// What fits where the line ends: whole commands while the first word is typed, then the
        /// argument's choices — each a full replacement for the last word. `usage`: how the command
        /// found so far is written.
        /// </summary>
        public List<string> Suggest(string line, out string usage)
        {
            usage = null;
            line = Normalize(line);
            var endsInSpace = line.EndsWith(" ");
            var words = Words(line);
            var result = new List<string>();

            if (words.Length == 0 || (words.Length == 1 && !endsInSpace))
            {
                var typed = words.Length == 0 ? "" : words[0].ToLowerInvariant();

                foreach (var command in all)
                {
                    var name = Names(command).FirstOrDefault(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase));

                    if (name != null)
                    {
                        result.Add("/" + name);
                    }
                }

                var only = words.Length == 1 ? Find(words[0]) : null;
                usage = only?.Usage;

                return result;
            }

            var found = Find(words[0]);

            if (found == null)
            {
                return result;
            }

            usage = found.Usage;
            var args = words.Skip(1).ToArray();
            var index = endsInSpace ? args.Length : args.Length - 1;
            var prefix = endsInSpace ? "" : args[args.Length - 1];

            foreach (var choice in found.Suggest(args, index))
            {
                if (choice.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(choice, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(choice);
                }
            }

            return result;
        }

        /// <summary>The line with its last word replaced by a suggestion (Tab).</summary>
        public static string Complete(string line, string suggestion)
        {
            line = Normalize(line);

            if (suggestion.StartsWith("/"))
            {
                return suggestion + " ";
            }

            var cut = line.EndsWith(" ") ? line.Length : line.LastIndexOf(' ') + 1;

            return line.Substring(0, cut) + suggestion + " ";
        }

        Command Find(string name)
        {
            name = name.TrimStart('/').ToLowerInvariant();

            return all.FirstOrDefault(c => Names(c).Contains(name));
        }

        static IEnumerable<string> Names(Command command) => new[] { command.Name }.Concat(command.Aliases);

        static string[] Words(string line) => line.TrimStart('/').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// The line as meant: one typed with the Russian layout ("." where "/" was pressed, Cyrillic where
        /// the Latin letters' keys were) is turned back into what the keys say — but not a Russian command name.
        /// </summary>
        public static string Normalize(string line)
        {
            line = (line ?? "").TrimStart();

            if (line.StartsWith("."))
            {
                line = "/" + line.Substring(1);
            }

            var first = line.TrimStart('/').Split(' ')[0].ToLowerInvariant();

            if (first.Length > 0 && first.Any(c => c >= 'а' && c <= 'я' || c == 'ё') && !RussianNames.Contains(first))
            {
                return string.Concat(line.Select(ToLatin));
            }

            return line;
        }

        static readonly HashSet<string> RussianNames = new HashSet<string>();

        const string Cyrillic = "йцукенгшщзхъфывапролджэячсмитьбюё";
        const string Latin = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`";

        static char ToLatin(char c)
        {
            var lower = char.ToLowerInvariant(c);
            var at = Cyrillic.IndexOf(lower);

            return at < 0 ? c : Latin[at];
        }

        static float Number(string word) => float.Parse(word.Replace(',', '.'), CultureInfo.InvariantCulture);

        static bool TryNumber(string word, out float value) =>
            float.TryParse((word ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // ── The commands ──────────────────────────────────────────────

        void Add(Command command)
        {
            all.Add(command);

            foreach (var alias in command.Aliases)
            {
                if (alias.Any(c => c >= 'а' && c <= 'я'))
                {
                    RussianNames.Add(alias);
                }
            }
        }

        void Build()
        {
            Add(new Command
            {
                Name = "help",
                Aliases = new[] { "помощь", "?" },
                Usage = "/help [команда]",
                About = "список команд или как пишется одна",
                Suggest = (_, i) => i == 0 ? all.Select(c => c.Name) : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length > 0 && Find(a[0]) is Command one)
                    {
                        return $"{one.Usage} — {one.About}" + (one.Aliases.Length > 0 ? $" (ещё: {string.Join(", ", one.Aliases.Select(n => "/" + n))})" : "");
                    }

                    return "Команды: " + string.Join("  ", all.Select(c => "/" + c.Name)) + ". Tab — дополнить, ↑ — прошлые.";
                },
            });

            Add(new Command
            {
                Name = "time",
                Aliases = new[] { "время" },
                Usage = "/time set <утро|день|вечер|ночь|часы> · /time add <часы>",
                About = "время суток",
                Suggest = (a, i) => i == 0 ? new[] { "set", "add" } : i == 1 && a.Length > 0 && a[0] == "set"
                    ? new[] { "dawn", "morning", "noon", "day", "evening", "night", "midnight", "6", "12", "18", "22" }
                    : i == 1 ? new[] { "1", "3", "6", "12", "24" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var clock = host.Clock;

                    if (a.Length == 0)
                    {
                        return $"День {clock.Day}, {clock.Label}";
                    }

                    if (a[0] == "add" && a.Length > 1)
                    {
                        // Time only goes forward (back, the Gu would have eaten meals not yet given).
                        if (!(Number(a[1]) > 0))
                        {
                            return "!Время идёт только вперёд: /time add <часы больше нуля>";
                        }

                        clock.Skip(Number(a[1]));

                        return $"Прошло {Number(a[1]):0.#} ч: день {clock.Day}, {clock.Label}";
                    }

                    var word = a[0] == "set" && a.Length > 1 ? a[1] : a[0];
                    var hour = word.ToLowerInvariant() switch
                    {
                        "dawn" or "рассвет" => 5.5f,
                        "morning" or "утро" => 7,
                        "noon" or "day" or "день" or "полдень" => 12,
                        "evening" or "вечер" => 18.5f,
                        "night" or "ночь" => 22,
                        "midnight" or "полночь" => 0,
                        _ => ParseHour(word),
                    };

                    if (hour < 0)
                    {
                        return "!Не понял время: " + word;
                    }

                    var now = (float)(clock.MinuteOfDay / 60);
                    clock.Skip(((hour - now) % 24 + 24) % 24);

                    return $"Время: день {clock.Day}, {clock.Label}";
                },
            });

            Add(new Command
            {
                Name = "tp",
                Aliases = new[] { "teleport", "тп" },
                Usage = "/tp <место> · /tp <x> <z> · /tp <x> <y> <z> (~ — от себя)",
                About = "перенестись",
                Suggest = (a, i) => i == 0 ? host.Places.Keys.Concat(new[] { "~" }) : new[] { "~" },
                Run = a =>
                {
                    if (a.Length == 1)
                    {
                        if (host.Places.TryGetValue(a[0].ToLowerInvariant(), out var place))
                        {
                            host.Teleport(place);

                            return $"Перенесены: {a[0]}";
                        }

                        return "!Нет такого места. Tab подскажет";
                    }

                    var at = host.HeroAt;
                    float Coord(string word, float own) => word.StartsWith("~") ? own + (word.Length > 1 ? Number(word.Substring(1)) : 0) : Number(word);

                    if (a.Length == 2)
                    {
                        host.Teleport(new Vector3(Coord(a[0], at.x), float.NaN, Coord(a[1], at.z)));
                    }
                    else if (a.Length >= 3)
                    {
                        host.Teleport(new Vector3(Coord(a[0], at.x), Coord(a[1], at.y), Coord(a[2], at.z)));
                    }
                    else
                    {
                        return "!" + "/tp <место> · /tp <x> <z>";
                    }

                    var to = host.HeroAt;

                    return $"Перенесены: {to.x:0}, {to.y:0}, {to.z:0}";
                },
            });

            Add(new Command
            {
                Name = "weather",
                Aliases = new[] { "погода" },
                Usage = "/weather <clear|cloudy|rain|auto>",
                About = "погода (auto — как идёт сама)",
                Suggest = (_, i) => i == 0 ? new[] { "clear", "cloudy", "rain", "auto" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var word = a.Length > 0 ? a[0].ToLowerInvariant() : "auto";
                    GuWorld.World.Sky? sky = word switch
                    {
                        "clear" or "ясно" => GuWorld.World.Sky.Clear,
                        "cloudy" or "облачно" => GuWorld.World.Sky.Cloudy,
                        "rain" or "дождь" => GuWorld.World.Sky.Rain,
                        _ => null,
                    };
                    host.SetWeather(sky);

                    return sky.HasValue ? $"Погода: {GuWorld.World.Weather.Name(sky.Value)}" : "Погода идёт сама";
                },
            });

            Add(new Command
            {
                Name = "give",
                Aliases = new[] { "дать" },
                Usage = "/give <вещь> [сколько]",
                About = "положить в сумку",
                Suggest = (_, i) => i == 0 ? ItemBook.All.Select(k => k.Id) : i == 1 ? new[] { "1", "10", "50" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length == 0)
                    {
                        return "!/give <вещь> [сколько]";
                    }

                    var kind = ItemBook.Find(a[0]) ?? ItemBook.All.FirstOrDefault(k => k.Name.StartsWith(a[0], StringComparison.OrdinalIgnoreCase));

                    if (kind == null)
                    {
                        return "!Нет такой вещи. Tab подскажет";
                    }

                    var count = a.Length > 1 ? Mathf.Max(1, (int)Number(a[1])) : 1;
                    var kept = host.Cultivator.Bag.Add(kind.Id, count, host.Clock.Minutes);

                    return kept > 0 ? $"В сумке: {kind.Name} ×{kept}" + (kept < count ? " (больше не влезло)" : "") : "!Сумка полна";
                },
            });

            Add(new Command
            {
                Name = "heal",
                Aliases = new[] { "лечить" },
                Usage = "/heal [сколько]",
                About = "вылечиться",
                Suggest = (_, i) => i == 0 ? new[] { "100" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    host.Health.Heal(a.Length > 0 ? Number(a[0]) : host.Health.Max);

                    return $"Жизнь: {host.Health.Current:0} / {host.Health.Max:0}";
                },
            });

            Add(new Command
            {
                Name = "essence",
                Aliases = new[] { "эссенция" },
                Usage = "/essence [проценты|max]",
                About = "эссенция в апертуре",
                Suggest = (_, i) => i == 0 ? new[] { "max", "0", "50" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var aperture = host.Cultivator.Aperture;
                    aperture.Essence = a.Length == 0 || a[0] == "max" ? aperture.Cap : Mathf.Clamp(Number(a[0]), 0, aperture.Cap);

                    return $"Эссенция: {aperture.Essence:0}% из {aperture.Cap:0}%";
                },
            });

            Add(new Command
            {
                Name = "rank",
                Aliases = new[] { "ранг" },
                Usage = "/rank <1–5> [стадия 1–4]",
                About = "ранг и малая ступень (для проверки)",
                Suggest = (_, i) => i == 0 ? new[] { "1", "2", "3", "4", "5" } : i == 1 ? new[] { "1", "2", "3", "4" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length == 0)
                    {
                        var ap = host.Cultivator.Aperture;

                        return $"Ранг {ap.Rank}, {GuWorld.Cultivation.Aperture.Name(ap.Stage)}, плотность ×{ap.Density:0.##}";
                    }

                    var stage = a.Length > 1 ? Mathf.Clamp((int)Number(a[1]), 1, 4) - 1 : 0;
                    host.Cultivator.SetRealm((int)Number(a[0]), (GuWorld.Cultivation.Stage)stage);

                    return $"Ранг {host.Cultivator.Aperture.Rank}, {GuWorld.Cultivation.Aperture.Name(host.Cultivator.Aperture.Stage)}";
                },
            });

            Add(new Command
            {
                Name = "talent",
                Aliases = new[] { "талант" },
                Usage = "/talent <20–100>",
                About = "талант, % апертуры (для проверки)",
                Suggest = (_, i) => i == 0 ? new[] { "100", "99", "88", "70", "50", "30" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length > 0)
                    {
                        host.Cultivator.SetTalent(Number(a[0]));
                    }

                    var ap = host.Cultivator.Aperture;

                    return $"Талант: {ap.TalentName} · море полнится за {GuWorld.Cultivation.Talents.Span(GuWorld.Cultivation.Aperture.RefillHours(ap.Talent))}";
                },
            });

            Add(new Command
            {
                Name = "gu",
                Aliases = new[] { "гу" },
                Usage = "/gu <гу|ранг 1–5|all>",
                About = "положить в сумку Гу — один, все одного ранга, или все",
                Suggest = (_, i) => i == 0 ? new[] { "1", "2", "3", "4", "5", "all" }.Concat(GuWorld.Cultivation.GuBook.All.Select(k => k.Id)) : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length == 0)
                    {
                        return $"!/gu <гу|ранг|all> — всего Гу: {GuWorld.Cultivation.GuBook.Count}";
                    }

                    var one = GuWorld.Cultivation.GuBook.Find(a[0]) ??
                              GuWorld.Cultivation.GuBook.All.FirstOrDefault(k => k.Name.IndexOf(a[0], StringComparison.OrdinalIgnoreCase) >= 0);
                    var rank = int.TryParse(a[0], out var r) ? r : 0;
                    var kinds = one != null ? new[] { one } : GuWorld.Cultivation.GuBook.All.Where(k => a[0] == "all" || k.Rank == rank).ToArray();

                    if (kinds.Length == 0)
                    {
                        return "!Нет такого Гу. Tab подскажет";
                    }

                    var given = 0;

                    foreach (var kind in kinds)
                    {
                        given += host.Cultivator.Bag.Add(kind.Item, 1, host.Clock.Minutes);
                    }

                    return given > 0 ? $"В сумке Гу: {given}" + (given < kinds.Length ? $" из {kinds.Length} (сумка полна)" : "") : "!Сумка полна";
                },
            });

            Add(new Command
            {
                Name = "use",
                Aliases = new[] { "гу-клавиша" },
                Usage = "/use <1–4> · /use <гу> · /use refine",
                About = "применить Гу с клавиши или по имени; refine — очистить все Гу из сумки в апертуру",
                Suggest = (_, i) => i == 0 ? new[] { "1", "2", "3", "4", "refine" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    if (a.Length == 0)
                    {
                        return "!/use <1–4|гу|refine>";
                    }

                    var gu = host.Cultivator;

                    if (a[0] == "refine")
                    {
                        var n = 0;

                        for (var cell = 0; cell < GuWorld.Items.Bag.Cells; cell++)
                        {
                            if (gu.Bag[cell]?.Kind?.Use == ItemUse.Refine)
                            {
                                gu.Aperture.Essence = gu.Aperture.Cap;

                                if (gu.Refine(cell)) n++;
                            }
                        }

                        return $"Очищено Гу: {n}";
                    }

                    if (int.TryParse(a[0], out var key))
                    {
                        return gu.Use(key - 1) ? $"Клавиша {key}: применено" : $"!Клавиша {key}: не вышло";
                    }

                    var kind = GuWorld.Cultivation.GuBook.Find(a[0]);

                    return kind != null && gu.UseGu(kind.Id) ? $"{kind.Name}: применено" : "!Не вышло";
                },
            });

            Add(new Command
            {
                Name = "kill",
                Aliases = new[] { "убить" },
                Usage = "/kill",
                About = "погибнуть (игра начнётся заново)",
                Run = _ =>
                {
                    host.Kill();

                    return "Вы погибли";
                },
            });

            Add(new Command
            {
                Name = "wolves",
                Aliases = new[] { "волки", "wolf", "beasts", "звери" },
                Usage = "/wolves [сколько] [grey|lightning|bold|frenzy|thunder_crown|boar|boar_king|bear|monkey|monkey_king|snake]",
                About = "звери рядом (короли зверей приходят со своей стаей)",
                Suggest = (_, i) => i == 0 ? new[] { "1", "3", "5" } : i == 1 ? GuWorld.Creatures.BeastKinds.All.Select(k => k.Id) : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var count = a.Length > 0 ? Mathf.Clamp((int)Number(a[0]), 1, 12) : 3;
                    var kind = a.Length > 1 ? GuWorld.Creatures.BeastKinds.Find(a[1]) : GuWorld.Creatures.WolfKinds.Grey;

                    if (kind == null)
                    {
                        return "!Нет таких зверей. Tab подскажет";
                    }

                    var came = host.SpawnPack(kind, count);

                    return came > 0 ? $"Пришло зверей: {came} ({kind.Name})" : "!Здесь зверям негде встать";
                },
            });

            Add(new Command
            {
                Name = "tide",
                Aliases = new[] { "прилив" },
                Usage = "/tide [great]",
                About = "волчий прилив: волны волков с королями зверей (great — с Волком Громовой Короны)",
                Suggest = (_, i) => i == 0 ? new[] { "great" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    host.StartTide(a.Length > 0 && a[0].ToLowerInvariant() == "great");

                    return "Волчий прилив начался";
                },
            });

            Add(new Command
            {
                Name = "masters",
                Aliases = new[] { "мастера", "гу-мастера" },
                Usage = "/masters [bai|xiong|gu_yue] [ранг 1–3] [сколько]",
                About = "Гу-мастера другого клана рядом — нападают сразу",
                Suggest = (_, i) => i == 0 ? new[] { "bai", "xiong", "gu_yue" } : i == 1 ? new[] { "1", "2", "3" } : i == 2 ? new[] { "1", "2", "3" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var clan = a.Length > 0 ? a[0].ToLowerInvariant() : "bai";

                    if (clan != "bai" && clan != "xiong" && clan != "gu_yue")
                    {
                        return "!Нет такого клана: bai, xiong или gu_yue";
                    }

                    var rank = a.Length > 1 ? Mathf.Clamp((int)Number(a[1]), 1, 3) : 1;
                    var count = a.Length > 2 ? Mathf.Clamp((int)Number(a[2]), 1, 6) : 1;
                    var came = host.SpawnMasters(clan, rank, count);

                    return came > 0 ? $"Пришли Гу-мастера: {came} ({GuWorld.Creatures.MasterKinds.Of(clan, rank).Name})" : "!Здесь им негде встать";
                },
            });

            Add(new Command
            {
                Name = "quest",
                Aliases = new[] { "задание" },
                Usage = "/quest [done]",
                About = "задание сейчас; done — выполнить его",
                Suggest = (_, i) => i == 0 ? new[] { "done" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    var quests = host.Quests;
                    var current = quests.Current;

                    if (current == null)
                    {
                        return "Задания нет";
                    }

                    if (a.Length > 0 && a[0] == "done")
                    {
                        quests.Count(current.Event, current.Target);

                        return $"«{current.Title}» — выполнено, вернитесь к: {current.Giver}";
                    }

                    return $"«{current.Title}»: {current.Goal} ({quests.Progress} / {current.Target})";
                },
            });

            Add(new Command
            {
                Name = "view",
                Aliases = new[] { "вид" },
                Usage = "/view [first|third]",
                About = "вид от первого лица или из-за спины (F5)",
                Suggest = (_, i) => i == 0 ? new[] { "first", "third" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    host.FirstPerson = a.Length == 0 ? !host.FirstPerson : a[0] == "first";

                    return host.FirstPerson ? "Вид от первого лица" : "Вид из-за спины";
                },
            });

            Add(new Command
            {
                Name = "hitboxes",
                Aliases = new[] { "хитбоксы" },
                Usage = "/hitboxes [on|off]",
                About = "показать хитбоксы (F3+B)",
                Suggest = (_, i) => i == 0 ? new[] { "on", "off" } : Enumerable.Empty<string>(),
                Run = a =>
                {
                    host.Hitboxes = a.Length == 0 ? !host.Hitboxes : a[0] == "on";

                    return host.Hitboxes ? "Хитбоксы показаны" : "Хитбоксы скрыты";
                },
            });

            Add(new Command
            {
                Name = "where",
                Aliases = new[] { "где" },
                Usage = "/where",
                About = "где вы",
                Run = _ =>
                {
                    var at = host.HeroAt;

                    return $"x {at.x:0}, y {at.y:0}, z {at.z:0}";
                },
            });
        }

        /// <summary>An hour as written: "6", "18.5", "7:30" (−1 when it is none).</summary>
        static float ParseHour(string word)
        {
            var parts = word.Split(':');

            if (TryNumber(parts[0], out var hours) && hours >= 0 && hours <= 24)
            {
                var minutes = parts.Length > 1 && TryNumber(parts[1], out var m) ? m : 0;

                return hours + minutes / 60;
            }

            return -1;
        }
    }
}
