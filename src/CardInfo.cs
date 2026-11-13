using System.Collections.Generic;
using System.Text;
using Setting;
using UnityEngine;

namespace ParaPie
{
    /// <summary>Reads Together Card data straight from the game settings (no UI card needed).</summary>
    internal static class CardInfo
    {
        // Friendlier names for the internal category names, in the order they appear in the pie.
        private static readonly string[][] CategoryNames =
        {
            new[] { "Default", "Friendly" },
            new[] { "GetToKnow", "Get to Know" },
            new[] { "Funny", "Funny" },
            new[] { "Flirty", "Romantic" },
            new[] { "LabelLevelUp", "Relationship" },
            new[] { "ShareTip", "Share a Tip" },
            new[] { "Skills", "Skills" },
            new[] { "Brag", "Brag" },
            new[] { "Complain", "Complain" },
            new[] { "Mean", "Mean" },
            new[] { "Request", "Requests" },
            new[] { "Introduction", "Introductions" },
            new[] { "Baby", "Baby" },
        };

        public static Together Setting { get { return Settings.Get<Together>(); } }

        public static TogetherCardCategory Category(TogetherCard card)
        {
            var t = Setting;
            ulong guid = card.Category != 0 ? card.Category : t.DefaultCategory;
            return t.GetCategoryByGUID(guid) ?? t.GetCategoryByGUID(t.DefaultCategory);
        }

        public static string CategoryName(TogetherCardCategory c)
        {
            if (c == null) return "Social";
            foreach (var pair in CategoryNames) if (pair[0] == c.DisplayName) return pair[1];
            return SplitCamel(c.DisplayName);
        }

        public static int CategoryOrder(TogetherCardCategory c)
        {
            if (c == null) return 99;
            for (int i = 0; i < CategoryNames.Length; i++) if (CategoryNames[i][0] == c.DisplayName) return i;
            return 50;
        }

        public static string Label(TogetherCard card, ulong skinGUID, ulong requestGUID)
        {
            string key = "TogetherCard_" + card.DisplayName;
            string param = null;
            try
            {
                if (card.InjectSkinIntoCard && skinGUID != 0)
                {
                    var skins = Settings.Get<Skins>();
                    var skin = skins.GetSkinByGUID(skinGUID);
                    var prop = skin != null ? skin.GetProperty(skins.SkinSelectionUITitlePropertyTag) : null;
                    if (prop != null) param = TranslationManager.Get("Skin_" + prop.StringValue);
                }
                if (card.InjectRequestIntoCard && requestGUID != 0)
                {
                    var goal = Settings.Get<Goals>().GetGoalByGUID(requestGUID);
                    if (goal != null) param = TranslationManager.Get("Goal_" + goal.DisplayName);
                }
            }
            catch { }
            string text = param != null ? TranslationManager.Get(key, param) : TranslationManager.Get(key);
            return string.IsNullOrEmpty(text) ? SplitCamel(card.DisplayName) : text;
        }

        /// <summary>The personality trait a card is tied to, e.g. "Goofy", or null.</summary>
        public static string Trait(TogetherCard card)
        {
            try
            {
                var reqs = TogetherManager.Instance.GetAllCardRequirements(card, false);
                if (reqs == null) return null;
                foreach (var r in reqs)
                {
                    if (!r.MustBeTrue) continue;
                    if (r.Type == ContextRequirementType.HasPersonalityTrait || r.Type == ContextRequirementType.PersonalityTraitStat)
                    {
                        var trait = Settings.Get<Personalities>().GetPersonalityTraitByGUID(r.PersonalityTrait);
                        if (trait != null) return TranslationManager.Get("Trait_" + trait.DisplayName);
                    }
                }
            }
            catch { }
            return null;
        }

        public static string SplitCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public static string JoinInfo(List<string> parts)
        {
            return string.Join("  •  ", parts.ToArray());
        }

        public static Color Readable(Color c)
        {
            return c.a < 0.05f ? Palette.Cocoa : c;
        }
    }
}
