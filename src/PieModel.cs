using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Setting;
using UnityEngine;

namespace ParaPie
{
    /// <summary>One option mirrored from a (hidden) UITogetherCard owned by the game.</summary>
    internal class OptionData
    {
        public UITogetherCard Card;     // null in preview mode
        public ulong CardGUID;
        public string Label = "";
        public Color Background = Color.white;
        public Color TextColor = Color.black;
        public string SuccessChance;
        public string Trait;
        public Sprite TraitIcon;
        public Sprite ItemIcon;
        public string Relationship;
        public bool HasVariants;
    }

    /// <summary>All the cards one character (or the storyteller) was dealt.</summary>
    internal class PageData
    {
        public ulong CharacterGUID;     // 0 = storyteller
        public string Name = "";
        public Sprite Portrait;
        public bool IsStoryteller;
        public readonly List<OptionData> Options = new List<OptionData>();
    }

    /// <summary>
    /// "Together mode" pie: used when the game itself opens the card hand (the together bar, if
    /// you keep it visible). Mirrors the hidden cards as slices.
    /// </summary>
    internal static class PieModel
    {
        private static readonly AccessTools.FieldRef<UITogetherCard, ulong> CharacterRef =
            AccessTools.FieldRefAccess<UITogetherCard, ulong>("_characterGUID");
        private static readonly AccessTools.FieldRef<UITogetherCard, ulong> SocialGroupRef =
            AccessTools.FieldRefAccess<UITogetherCard, ulong>("_socialGroupGUID");
        private static readonly AccessTools.FieldRef<UITogetherCard, List<ulong>> TargetsRef =
            AccessTools.FieldRefAccess<UITogetherCard, List<ulong>>("_targets");
        private static readonly MethodInfo CardOnClick = AccessTools.Method(typeof(UITogetherCard), "OnClick");

        public static List<PageData> Gather(UICharacters ui)
        {
            var pages = new List<PageData>();
            if (ui.List == null) return pages;

            foreach (var item in ui.List.CurrentItems)
            {
                var ci = item as UICharacterItem;
                if (ci == null || !ci.gameObject.activeInHierarchy || ci.OfferedTogetherCards == null) continue;
                if (!ci.OfferedTogetherCards.CardsShown) continue;

                var page = new PageData { CharacterGUID = ci.CharacterGUID };
                ReadCards(ci.OfferedTogetherCards, page.Options);
                if (page.Options.Count == 0) continue;

                var character = AssetManager.Instance != null ? AssetManager.Instance.GetCharacter(ci.CharacterGUID) : null;
                if (character != null)
                {
                    page.Name = character.Data.ShortName;
                    page.Portrait = character.ThumbnailSprite;
                }
                if (string.IsNullOrEmpty(page.Name) && ci.LabelName != null) page.Name = ci.LabelName.text;
                pages.Add(page);
            }

            var story = ui.StorytellerOfferedCards;
            if (story != null && story.CardsShown && ui.TogetherCardStorytellerPanel != null && ui.TogetherCardStorytellerPanel.activeSelf)
            {
                var page = new PageData { CharacterGUID = 0, IsStoryteller = true, Name = Plugin.StorytellerText.Value };
                ReadCards(story, page.Options);
                if (page.Options.Count > 0) pages.Add(page);
            }
            return pages;
        }

        private static void ReadCards(UIOfferedTogetherCards offered, List<OptionData> into)
        {
            if (offered.TogetherCardsList == null) return;
            foreach (var li in offered.TogetherCardsList.CurrentItems)
            {
                var card = li as UITogetherCard;
                if (card == null || !card.gameObject.activeSelf || card.CardGUID == 0 || !card.Interactable) continue;
                var o = new OptionData { Card = card, CardGUID = card.CardGUID };
                if (card.Label != null && card.Label.Text != null)
                {
                    o.Label = card.Label.Text.text;
                    o.TextColor = card.Label.Text.color;
                }
                if (card.ImageBackground != null) o.Background = card.ImageBackground.color;
                if (card.SuccessChanceContainer != null && card.SuccessChanceContainer.activeSelf && card.SuccessChanceLabel != null)
                    o.SuccessChance = card.SuccessChanceLabel.text;
                if (card.LabelPersonalityTrait != null && card.LabelPersonalityTrait.gameObject.activeSelf && card.LabelPersonalityTrait.Text != null)
                    o.Trait = card.LabelPersonalityTrait.Text.text;
                if (card.ImageIconPersonality != null && card.ImageIconPersonality.gameObject.activeSelf)
                    o.TraitIcon = card.ImageIconPersonality.sprite;
                if (card.ImageInventoryItem != null && card.ImageInventoryItem.gameObject.activeSelf)
                    o.ItemIcon = card.ImageInventoryItem.sprite;
                if (card.RelationshipLabelContainer != null && card.RelationshipLabelContainer.activeSelf && card.RelationshipLabelText != null)
                    o.Relationship = card.RelationshipLabelText.text;
                o.HasVariants = card.ChangeVariantButton != null && card.ChangeVariantButton.activeSelf;
                if (string.IsNullOrEmpty(o.Label)) continue;
                into.Add(o);
            }
        }

        public static PieContent BuildContent(List<PageData> pages, bool preview)
        {
            if (!PieMenu.SelectedPage.HasValue || !Contains(pages, PieMenu.SelectedPage.Value))
                PieMenu.SelectedPage = PieMenu.DefaultPage(pages);
            ulong key = PieMenu.SelectedPage.Value;
            PageData page = pages[0];
            foreach (var p in pages) if (p.CharacterGUID == key) { page = p; break; }

            var c = new PieContent
            {
                Key = (preview ? "preview:" : "together:") + key,
                HubName = page.Name,
                HubPortrait = page.Portrait,
                HubIsStoryteller = page.IsStoryteller,
                Hint = preview ? "Preview - click an option to close" : Plugin.HintText.Value,
            };
            if (preview) c.OnOutsideClick = PieMenu.ClosePreview;

            foreach (var o in page.Options)
            {
                var opt = o;
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(o.SuccessChance)) parts.Add(o.SuccessChance + " success");
                if (!string.IsNullOrEmpty(o.Trait)) parts.Add(o.Trait);
                if (!string.IsNullOrEmpty(o.Relationship)) parts.Add(o.Relationship);
                if (o.HasVariants) parts.Add("right-click for another version");
                c.Slices.Add(new Slice
                {
                    Label = o.Label,
                    Background = o.Background,
                    TextColor = CardInfo.Readable(o.TextColor),
                    Icon = o.TraitIcon != null ? o.TraitIcon : o.ItemIcon,
                    Badge = o.SuccessChance,
                    Info = parts.Count > 0 ? CardInfo.JoinInfo(parts) : null,
                    HasVariants = o.HasVariants,
                    Seed = o.CardGUID,
                    OnClick = () => PieMenu.Pick(opt, preview),
                    OnRightClick = () => { if (opt.HasVariants) PieMenu.SwitchVariant(opt); },
                    OnHover = h => PieMenu.Hover(opt, h),
                    OnHoverStay = () => PieMenu.KeepHover(opt),
                });
            }

            if (pages.Count > 1)
            {
                foreach (var p in pages)
                {
                    ulong k = p.CharacterGUID;
                    c.Chips.Add(new Chip
                    {
                        Key = k,
                        Name = p.Name,
                        Portrait = p.Portrait,
                        IsStoryteller = p.IsStoryteller,
                        Active = k == key,
                        Accent = p.Options.Count > 0 ? p.Options[0].Background : Palette.Honey,
                        OnClick = () => PieMenu.SelectedPage = k,
                    });
                }
            }
            return c;
        }

        private static bool Contains(List<PageData> pages, ulong key)
        {
            foreach (var p in pages) if (p.CharacterGUID == key) return true;
            return false;
        }

        /// <summary>Exactly what clicking the card does in the original UI.</summary>
        public static void Click(UITogetherCard card)
        {
            CardOnClick.Invoke(card, null);
        }

        public static bool TargetsAnotherCharacter(ulong cardGUID)
        {
            var setting = Settings.Get<Together>();
            var card = setting != null ? setting.GetCardByGUID(cardGUID) : null;
            return card != null && card.TargetsAnotherCharacter;
        }

        /// <summary>Highlights who the card could target in the character bar, with their success chance.</summary>
        public static void SetHover(UICharacters ui, UITogetherCard card, bool hovered)
        {
            if (card == null) return;
            if (!hovered) { ui.UnsetSuccessChanceHovering(card.CardGUID); return; }
            if (card.SuccessChanceContainer == null || !card.SuccessChanceContainer.activeSelf) return;
            ulong sg = SocialGroupRef(card);
            ulong user = CharacterRef(card);
            var targets = TargetsRef(card);
            if (targets == null || targets.Count == 0)
                targets = TogetherManager.Instance.GetCharactersThatCanBeTargettedForCard(sg, card.CardGUID, user);
            ui.SetSuccessChanceHovering(true, targets, sg, card.CardGUID, user);
        }

        public static List<PageData> PreviewPages(UICharacters ui)
        {
            var page = new PageData { Name = "Preview" };
            try
            {
                ulong guid = ui.PlayerOwner.Player.GetSelectedCharacterGUID();
                var c = guid != 0 ? AssetManager.Instance.GetCharacter(guid) : null;
                if (c != null) { page.CharacterGUID = guid; page.Name = c.Data.ShortName; page.Portrait = c.ThumbnailSprite; }
            }
            catch { }
            Add(page, "Tell a Joke", "#FFD98A", "#6B4A1E", "68%", null, null, false);
            Add(page, "Compliment", "#BFE6C8", "#2F5A3A", null, null, null, true);
            Add(page, "Share a Dream", "#BCD9F5", "#28466B", null, "Dreamer", null, false);
            Add(page, "Flirt", "#F7BCD0", "#7A2E4C", "45%", null, "Crush +1", false);
            Add(page, "Deep Talk", "#D6C8F2", "#45337A", null, null, null, false);
            Add(page, "Playful Tease", "#FFC9A8", "#7A3E1E", "80%", "Goofy", null, true);
            var story = new PageData { IsStoryteller = true, Name = Plugin.StorytellerText.Value };
            Add(story, "Plan a Trip", "#BCD9F5", "#28466B", null, null, null, false);
            Add(story, "Reminisce", "#FFD98A", "#6B4A1E", null, null, "Friend +1", false);
            Add(story, "Group Hug", "#F7BCD0", "#7A2E4C", null, null, null, false);
            return new List<PageData> { page, story };
        }

        private static void Add(PageData p, string label, string bg, string fg, string chance, string trait, string rel, bool variants)
        {
            Color b, f;
            ColorUtility.TryParseHtmlString(bg, out b);
            ColorUtility.TryParseHtmlString(fg, out f);
            p.Options.Add(new OptionData
            {
                Label = label, Background = b, TextColor = f, SuccessChance = chance,
                Trait = trait, Relationship = rel, HasVariants = variants, CardGUID = (ulong)(p.Options.Count + 1)
            });
        }
    }
}
