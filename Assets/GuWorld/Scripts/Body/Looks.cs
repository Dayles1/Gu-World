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

        public Gender Gender => gender == "female" ? Gender.Female : Gender.Male;
        public HairStyle Hair => Parse(hair, DefaultHair(Gender));
        public Beard BeardStyle => Gender == Gender.Female ? Beard.None : Parse(beard, Beard.None);

        /// <summary>Whether the save holds a look at all (a game started before looks were chosen does not).</summary>
        public bool IsChosen => gender == "male" || gender == "female";

        /// <summary>The look as the models come: a parted cut for him, buns for her.</summary>
        public static Look Default(Gender gender) => new Look
        {
            gender = gender == Gender.Female ? "female" : "male",
            hair = Word(DefaultHair(gender)),
        };

        public static HairStyle DefaultHair(Gender gender) => gender == Gender.Female ? HairStyle.Buns : HairStyle.Parted;

        public Look With(Gender? gender = null, HairStyle? hair = null, Beard? beard = null)
        {
            var next = (Look)MemberwiseClone();

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
        public string Key => $"{Gender}/{Hair}/{BeardStyle}";

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
