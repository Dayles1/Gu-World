using System;
using UnityEngine;

namespace GuWorld.Body
{
    public enum Gender
    {
        Male,
        Female,
    }

    public enum HairStyle
    {
        Short,
        Parted,
        Buns,
        Bob,
        Ponytail,
        Long,
        Bald,
    }

    /// <summary>The hero's hanfu: dark, grey or white; the clan's bamboo green, indigo, earth brown.</summary>
    public enum Robe
    {
        Dark,
        Grey,
        White,
        Green,
        Indigo,
        Brown,
    }

    /// <summary>The skin: fair, warm, tanned (a mountain clan's youths work in the sun).</summary>
    public enum Skin
    {
        Fair,
        Warm,
        Tan,
    }

    /// <summary>The hero's height among the fifteen-year-olds: shorter, middling, taller.</summary>
    public enum Stature
    {
        Short,
        Middle,
        Tall,
    }

    /// <summary>A sign worn on the clothes, showing whose one is: a clan's mark.</summary>
    public enum Emblem
    {
        None,
        /// <summary>The Gu Yue clan's: a crescent moon, for the moonlight Gu at the heart of its arts.</summary>
        Crescent,
    }

    public enum Beard
    {
        None,
        Stubble,
        Mustache,
        Goatee,
        Full,
    }

    /// <summary>
    /// How the hero looks (as client/body/looks.ts, plus the gender): saved
    /// with the game, in the web's words ("male", "parted", "none"…). The hero
    /// is fifteen and the hair is always black, so neither is a choice.
    /// </summary>
    [Serializable]
    public class Look
    {
        /// <summary>The only hair colour: black (the web's "black").</summary>
        public static readonly Color HairBlack = new Color(0x1c / 255f, 0x1b / 255f, 0x22 / 255f);

        public string gender = "";
        public string hair = "";
        public string hair_color = "black";
        public string beard = "none";
        public string eyes = "brown";
        /// <summary>The hanfu's colour ("dark", "grey", "white", "green", "indigo", "brown").</summary>
        public string robe = "dark";
        /// <summary>The skin ("fair", "warm", "tan") and the height ("short", "middle", "tall").</summary>
        public string skin = "fair";
        public string stature = "middle";
        /// <summary>The hero's own name (the clan's name goes before it); empty until given.</summary>
        public string name = "";

        public Gender Gender => gender == "female" ? Gender.Female : Gender.Male;
        public HairStyle Hair => Parse(hair, DefaultHair(Gender));
        public Beard BeardStyle => Gender == Gender.Female ? Beard.None : Parse(beard, Beard.None);
        public Robe RobeColour => Parse(robe, Robe.Dark);
        public Skin SkinTone => Parse(skin, Skin.Fair);
        public Stature Height => Parse(stature, Stature.Middle);

        /// <summary>The height against a middling one's.</summary>
        public float HeightFactor => Height == Stature.Short ? 0.955f : Height == Stature.Tall ? 1.045f : 1;

        /// <summary>The skin's evened brightness (Paint.Saturate's tone, 0–255).</summary>
        public float SkinBrightness => SkinTone == Skin.Tan ? 146 : SkinTone == Skin.Warm ? 176 : 204;

        /// <summary>The clothes' dye for the hanfu's colour (Clothes.For).</summary>
        public int Dye => 8 + (int)RobeColour;

        /// <summary>Whether the save holds a look at all (a game started before looks were chosen does not).</summary>
        public bool IsChosen => gender == "male" || gender == "female";

        /// <summary>The look as the models come: a parted cut for him, buns for her.</summary>
        public static Look Default(Gender gender) => new Look
        {
            gender = gender == Gender.Female ? "female" : "male",
            hair = Word(DefaultHair(gender)),
        };

        public static HairStyle DefaultHair(Gender gender) => gender == Gender.Female ? HairStyle.Buns : HairStyle.Parted;

        /// <summary>
        /// The hero as others call them: the clan's name before their own (lore: fac_gu_yue_clan —
        /// "Gu Yue Fang Yuan"); without a name of their own, a student of the clan.
        /// </summary>
        public string FullName
        {
            get
            {
                var own = (name ?? "").Trim();

                return own.Length > 0 ? "Гу Юэ " + own : Gender == Gender.Female ? "Ученица клана Гу Юэ" : "Ученик клана Гу Юэ";
            }
        }

        /// <summary>A name as the hero may have it: letters, spaces and hyphens, at most 16, trimmed.</summary>
        public static string CleanName(string text)
        {
            var kept = new System.Text.StringBuilder();

            foreach (var c in text ?? "")
            {
                if ((char.IsLetter(c) || c == ' ' || c == '-') && kept.Length < 16)
                {
                    kept.Append(c);
                }
            }

            return kept.ToString().Trim();
        }

        public Look With(Gender? gender = null, HairStyle? hair = null, Beard? beard = null, Robe? robe = null, string name = null,
            Skin? skin = null, Stature? stature = null)
        {
            var next = (Look)MemberwiseClone();

            if (skin.HasValue)
            {
                next.skin = Word(skin.Value);
            }

            if (stature.HasValue)
            {
                next.stature = Word(stature.Value);
            }

            if (name != null)
            {
                next.name = CleanName(name);
            }

            if (robe.HasValue)
            {
                next.robe = Word(robe.Value);
            }

            if (gender.HasValue)
            {
                next.gender = gender.Value == Gender.Female ? "female" : "male";
            }

            if (hair.HasValue)
            {
                next.hair = Word(hair.Value);
            }

            if (beard.HasValue)
            {
                next.beard = Word(beard.Value);
            }

            next.hair_color = "black";

            if (next.Gender == Gender.Female)
            {
                next.beard = "none";
            }

            return next;
        }

        /// <summary>A stable key: two looks with the same key are drawn the same.</summary>
        public string Key => $"{Gender}/{Hair}/{BeardStyle}/{RobeColour}/{SkinTone}/{Height}";

        public static string Word<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();

        static T Parse<T>(string word, T fallback) where T : struct, Enum =>
            Enum.TryParse<T>(word, true, out var value) && Enum.IsDefined(typeof(T), value) ? value : fallback;

        public static string Name(HairStyle hair) => hair switch
        {
            HairStyle.Short => "Короткая",
            HairStyle.Parted => "С пробором",
            HairStyle.Buns => "Пучки",
            HairStyle.Bob => "Каре",
            HairStyle.Ponytail => "Хвост",
            HairStyle.Long => "Длинные",
            _ => "Лысый",
        };

        public static string Name(Robe robe) => robe switch
        {
            Robe.Grey => "Серое",
            Robe.White => "Белое",
            Robe.Green => "Зелёное",
            Robe.Indigo => "Синее",
            Robe.Brown => "Бурое",
            _ => "Тёмное",
        };

        public static string Name(Skin skin) => skin switch
        {
            Skin.Warm => "Тёплая",
            Skin.Tan => "Смуглая",
            _ => "Светлая",
        };

        public static string Name(Stature stature) => stature switch
        {
            Stature.Short => "Пониже",
            Stature.Tall => "Повыше",
            _ => "Средний",
        };

        /// <summary>A look by chance (the gender kept): a hairstyle, a hanfu, a skin, a height.</summary>
        public Look Random(System.Random random)
        {
            T Any<T>() where T : struct, Enum
            {
                var values = (T[])Enum.GetValues(typeof(T));

                return values[random.Next(values.Length)];
            }

            var styles = Gender == Gender.Female
                ? new[] { HairStyle.Buns, HairStyle.Bob, HairStyle.Ponytail, HairStyle.Long, HairStyle.Parted }
                : new[] { HairStyle.Short, HairStyle.Parted, HairStyle.Ponytail, HairStyle.Long, HairStyle.Buns };

            return With(hair: styles[random.Next(styles.Length)], robe: Any<Robe>(), skin: Any<Skin>(), stature: Any<Stature>());
        }

        public static string Name(Beard beard) => beard switch
        {
            Beard.Stubble => "Щетина",
            Beard.Mustache => "Усы",
            Beard.Goatee => "Эспаньолка",
            Beard.Full => "Борода",
            _ => "Нет",
        };
    }
}
