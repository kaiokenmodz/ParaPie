using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Setting;
using UnityEngine;

namespace ParaPie
{
    /// <summary>
    /// Sims-style social pie: click another Para and get every social you can do with them right
    /// now (no together bar), plus their normal interactions. Socials are played through the game's
    /// own Together Card pipeline, auto-targeted at the Para you clicked, so relationships, success
    /// chances, outcomes and cooldowns all behave exactly like the base game.
    /// </summary>
    internal static class SocialPie
    {
        private const int MaxPerRing = 12;

        private class CardSlice
        {
            public TogetherCardChoice Choice;
            public TogetherCard Card;
            public TogetherCardCategory Category;
            public string Label;
            public string Badge;
            public string Info;
        }

        private static readonly MethodInfo ListItemClick = AccessTools.Method(typeof(UIInteractionsListItem), "OnListItemClicked");

        private static bool _active;
        private static UIInteractions _uii;
        private static ulong _user, _target, _groupGUID;
        private static readonly List<CardSlice> _cards = new List<CardSlice>();
        private static ulong? _category;
        private static int _page;

        // Auto-play state: we open the game's card mode with just the chosen card, then click it.
        private static ulong _pendingCard, _pendingUser, _pendingTarget;
        private static float _pendingSince = -1f;

        /// <summary>True while a social chosen from the pie is being played by the card system.</summary>
        public static bool AutoPlaying;

        public static bool IsActive { get { return _active; } }

        // ------------------------------------------------------------------ opening

        /// <summary>Postfix of UIInteractions.Show: take over the menu when another Para was clicked.</summary>
        public static void OnInteractionsShown(UIInteractions uii)
        {
            bool handle = PieMenu.IsReplacingCards && Plugin.SocialPie.Value && uii.ClickedCharacterGUID != 0 && !AutoPlaying;
            ulong user = 0;
            try { user = uii.PlayerOwner.Player.GetSelectedCharacterGUID(); } catch { }
            if (user == 0 || uii.ClickedCharacterGUID == user) handle = false;
            var chars = UI.GetOrNull<UICharacters>(uii.PlayerOwnerIndex);
            if (chars != null && (chars.IsTogetherCardsMode || chars.IsShowingOutcomes)) handle = false;

            PieMenu.SetHidden(uii.UIInteractionLists.gameObject, handle, false);
            if (!handle) { _active = false; return; }

            bool retarget = !_active || _target != uii.ClickedCharacterGUID || _user != user;
            _active = true;
            _uii = uii;
            if (retarget)
            {
                _user = user;
                _target = uii.ClickedCharacterGUID;
                _category = null;
                _page = 0;
                PieMenu.ReopenView();
            }
            RefreshCards();
            Plugin.Log.LogInfo("ParaPie: social pie for " + _target + " - " + _cards.Count + " socials" + (_groupGUID == 0 ? " (not hanging out)" : ""));
        }

        private static void RefreshCards()
        {
            _cards.Clear();
            _groupGUID = 0;
            var group = SocialGroupManager.Instance.GetCharacterCurrentSocialGroup(_user);
            if (group == null || group.CharactersInGroup.Count < 2 || !group.CharactersInGroup.Contains(_target)) return;
            _groupGUID = group.GUID;

            var together = CardInfo.Setting;
            Dictionary<ulong, List<TogetherCardChoice>> dealt;
            bool oldShowAll = together.ShowAllAvailableCards;
            var oldInitiative = group.NPCInitiative;
            try
            {
                // Sims style: every social the Para qualifies for, not a random hand.
                together.ShowAllAvailableCards = Plugin.ShowAllSocials.Value;
                group.NPCInitiative = (false, 0uL, default(TogetherCardChoice));
                dealt = TogetherManager.Instance.PickCharacterCards(group);
            }
            finally
            {
                together.ShowAllAvailableCards = oldShowAll;
                group.NPCInitiative = oldInitiative;
            }

            List<TogetherCardChoice> mine;
            if (dealt == null || !dealt.TryGetValue(_user, out mine)) return;
            var seen = new HashSet<ulong>();
            foreach (var choice in mine)
            {
                var card = together.GetCardByGUID(choice.CardGUID);
                if (card == null || !seen.Add(card.GUID)) continue;
                if (card.TargetsAnotherCharacter)
                {
                    var targets = choice.Characters != null && choice.Characters.Count > 0
                        ? choice.Characters
                        : TogetherManager.Instance.GetCharactersThatCanBeTargettedForCard(group.GUID, card.GUID, _user);
                    if (targets == null || !targets.Contains(_target)) continue;   // that social is aimed at someone else
                }
                _cards.Add(MakeCardSlice(choice, card, group));
            }
            _cards.Sort((a, b) =>
            {
                int c = CardInfo.CategoryOrder(a.Category).CompareTo(CardInfo.CategoryOrder(b.Category));
                return c != 0 ? c : string.Compare(a.Label, b.Label, System.StringComparison.CurrentCulture);
            });
        }

        private static CardSlice MakeCardSlice(TogetherCardChoice choice, TogetherCard card, SocialGroup group)
        {
            var s = new CardSlice { Choice = choice, Card = card, Category = CardInfo.Category(card) };
            s.Label = CardInfo.Label(card, choice.SkinGUID, choice.RequestGUID);
            var parts = new List<string>();
            if (card.HasSuccessChance)
            {
                float best = -1f;
                var targets = card.TargetsAnotherCharacter
                    ? new List<ulong> { _target }
                    : TogetherManager.Instance.GetCharactersThatCanBeTargettedForCard(group.GUID, card.GUID, _user);
                if (targets != null)
                    foreach (var t in targets)
                    {
                        var sc = TogetherManager.Instance.GetSuccessChanceForSpecificTargetOnCard(group.GUID, card.GUID, _user, t);
                        if (sc.hasIt && sc.value > best) best = sc.value;
                    }
                if (best >= 0f)
                {
                    s.Badge = Mathf.RoundToInt(Mathf.Min(100f, best)) + "%";
                    parts.Add(s.Badge + " success");
                }
            }
            string trait = CardInfo.Trait(card);
            if (!string.IsNullOrEmpty(trait)) parts.Add(trait);
            if (!card.TargetsAnotherCharacter && group.CharactersInGroup.Count > 2) parts.Add("with the whole group");
            s.Info = parts.Count > 0 ? CardInfo.JoinInfo(parts) : null;
            return s;
        }

        // ------------------------------------------------------------------ per frame

        public static PieContent Build()
        {
            if (!_active) return null;
            if (_uii == null || !_uii.IsVisible) { _active = false; return null; }

            var target = AssetManager.Instance.GetCharacter(_target);
            var user = AssetManager.Instance.GetCharacter(_user);
            var lists = _uii.UIInteractionLists.CurrentItems;
            int depth = lists.Count - 1;

            var c = new PieContent
            {
                HubName = target != null ? target.Data.ShortName : "",
                HubPortrait = target != null ? target.ThumbnailSprite : null,
                OpenAtMouse = true,
                OnOutsideClick = () => { if (_uii != null) _uii.Back(); },
            };
            string userName = user != null ? user.Data.ShortName : "";
            c.Hint = _groupGUID != 0 ? string.Format(Plugin.SocialHintText.Value, userName, c.HubName)
                                     : string.Format(Plugin.NotTogetherHintText.Value, userName, c.HubName);

            if (_category.HasValue)
            {
                c.Key = "social:" + _target + ":cat:" + _category.Value + ":" + _page;
                c.Slices.Add(Back(() => { _category = null; _page = 0; }));
                var inCat = _cards.Where(x => x.Category != null && x.Category.GUID == _category.Value).ToList();
                AddPaged(c, inCat.Select(CardToSlice).ToList());
            }
            else if (depth > 0)
            {
                c.Key = "social:" + _target + ":int:" + depth;
                c.Slices.Add(Back(PopInteractionList));
                var deepest = lists[depth] as UIInteractionsList;
                if (deepest != null) AddPaged(c, InteractionSlices(deepest));
            }
            else
            {
                c.Key = "social:" + _target + ":root:" + _page;
                var root = new List<Slice>();
                if (_cards.Count <= Plugin.FlatSocialLimit.Value)
                {
                    root.AddRange(_cards.Select(CardToSlice));
                }
                else
                {
                    foreach (var grp in _cards.GroupBy(x => x.Category != null ? x.Category.GUID : 0UL))
                    {
                        var cat = grp.First().Category;
                        ulong guid = grp.Key;
                        int count = grp.Count();
                        root.Add(new Slice
                        {
                            Label = CardInfo.CategoryName(cat),
                            Background = cat != null ? cat.BackgroundColor : Palette.Honey,
                            TextColor = CardInfo.Readable(cat != null ? cat.TextColor : Palette.Cocoa),
                            HasChildren = true, Tag = "category",
                            Info = count + (count == 1 ? " social" : " socials"),
                            Seed = guid,
                            OnClick = () => { _category = guid; _page = 0; PlaySubmenuSound(); },
                        });
                    }
                }
                var rootList = lists.Count > 0 ? lists[0] as UIInteractionsList : null;
                if (rootList != null) root.AddRange(InteractionSlices(rootList));
                AddPaged(c, root);
            }
            return c;
        }

        private static void AddPaged(PieContent c, List<Slice> slices)
        {
            int room = MaxPerRing - c.Slices.Count;
            if (slices.Count <= room) { c.Slices.AddRange(slices); return; }
            int per = room - 1;
            int pages = (slices.Count + per - 1) / per;
            if (_page >= pages) _page = 0;
            c.Slices.AddRange(slices.Skip(_page * per).Take(per));
            c.Slices.Add(new Slice
            {
                Label = "More… (" + (_page + 1) + "/" + pages + ")",
                Background = Palette.Cream,
                TextColor = Palette.SoftCocoa,
                HasChildren = true, Tag = "more",
                Seed = 7,
                OnClick = () => { _page = (_page + 1) % pages; PlaySubmenuSound(); },
            });
        }

        private static Slice CardToSlice(CardSlice s)
        {
            var cs = s;
            return new Slice
            {
                Label = s.Label,
                Background = s.Category != null ? s.Category.BackgroundColor : Palette.Honey,
                TextColor = CardInfo.Readable(s.Category != null ? s.Category.TextColor : Palette.Cocoa),
                Badge = s.Badge,
                Info = s.Info,
                Seed = s.Card.GUID,
                OnClick = () => Play(cs), Tag = "card",
            };
        }

        private static Slice Back(System.Action onClick)
        {
            return new Slice
            {
                Label = "Back",
                Background = Palette.Cream,
                TextColor = Palette.SoftCocoa,
                IsBack = true, Tag = "back",
                OnClick = () => { onClick(); PlaySubmenuSound(); },
            };
        }

        private static List<Slice> InteractionSlices(UIInteractionsList list)
        {
            var result = new List<Slice>();
            foreach (var item in list.UIListInteractionItems.CurrentItems)
            {
                var li = item as UIInteractionsListItem;
                if (li == null || !li.gameObject.activeSelf || li.LabelInteractionUnitName == null) continue;
                string label = li.LabelInteractionUnitName.Text != null ? li.LabelInteractionUnitName.Text.text : "";
                if (string.IsNullOrEmpty(label)) continue;
                bool greyed = !li.Interactable;
                string info = null;
                if (greyed && li.Tooltip != null) info = li.Tooltip.TextToShow;
                else
                {
                    var parts = new List<string>();
                    if (li.CostPanel != null && li.CostPanel.activeSelf && li.CostLabel != null) parts.Add(li.CostLabel.text);
                    if (li.CooldownPanel != null && li.CooldownPanel.activeSelf && li.CooldownLabel != null) parts.Add(li.CooldownLabel.text);
                    if (parts.Count > 0) info = CardInfo.JoinInfo(parts);
                }
                var captured = li;
                result.Add(new Slice
                {
                    Label = label,
                    Background = Palette.Sand,
                    TextColor = Palette.Cocoa,
                    Icon = li.ImageIcon != null && li.ImageIcon.gameObject.activeSelf ? li.ImageIcon.sprite : null,
                    Greyed = greyed,
                    Info = info,
                    HasChildren = li.ImageHasChildInteractionsIndicator != null && li.ImageHasChildInteractionsIndicator.gameObject.activeSelf,
                    Seed = (ulong)label.GetHashCode(),
                    OnClick = () => ListItemClick.Invoke(captured, null), Tag = "interaction",
                    OnHover = h => { if (captured != null) captured.IsMouseHovered = h; },
                });
            }
            return result;
        }

        private static void PopInteractionList()
        {
            var lists = _uii.UIInteractionLists;
            int depth = lists.CurrentItems.Count - 1;
            if (depth <= 0) return;
            var parent = lists.CurrentItems[depth - 1] as UIInteractionsList;
            lists.DeactivateAndPoolAllItemsAtIndexAndAfter(depth);
            if (parent != null)
                foreach (var it in parent.UIListInteractionItems.CurrentItems) it.IsOn = false;
        }

        private static void PlaySubmenuSound()
        {
            try { Settings.Get<Audio>().LiveMode.AudioInteractionMenuSubmenuOpen.Play(); } catch { }
        }

        // ------------------------------------------------------------------ playing a social

        private static void Play(CardSlice s)
        {
            var group = SocialGroupManager.Instance.GetSocialGroupByGUID(_groupGUID);
            var ui = UI.GetOrNull<UICharacters>(_uii.PlayerOwnerIndex);
            if (group == null || ui == null) return;

            ulong user = _user, target = _target;
            Plugin.Log.LogInfo("ParaPie: playing social '" + s.Label + "' (" + s.Card.DisplayName + ")");
            _active = false;
            PieMenu.SetHidden(_uii.UIInteractionLists.gameObject, false, false);
            _uii.Hide();

            // Deal exactly this card to our Para (and empty hands to any other household members in
            // the group, which the card UI expects), then let the game run its normal card flow.
            var household = HouseholdManager.Instance.CurrentHousehold.Data.Characters;
            var hands = new Dictionary<ulong, List<TogetherCardChoice>>();
            foreach (var ch in group.CharactersInGroup)
                if (household.Contains(ch)) hands[ch] = new List<TogetherCardChoice>();
            hands[user] = new List<TogetherCardChoice> { s.Choice };
            group.NPCInitiative = (false, 0uL, default(TogetherCardChoice));
            group.CharacterChoices = hands;
            group.StorytellerChoices = new List<TogetherCardChoice>();

            AutoPlaying = true;
            _pendingCard = s.Card.GUID;
            _pendingUser = user;
            _pendingTarget = target;
            _pendingSince = Time.unscaledTime;
            ui.SetTogetherCardsMode(group);
            TickPending(ui);
        }

        /// <summary>Clicks the freshly dealt (hidden) card as soon as the game has created it.</summary>
        public static void TickPending(UICharacters ui)
        {
            if (_pendingSince < 0f)
            {
                if (AutoPlaying && !ui.IsTogetherCardsMode && !ui.IsShowingOutcomes) AutoPlaying = false;
                return;
            }
            if (Time.unscaledTime - _pendingSince > 2f)
            {
                Plugin.Log.LogWarning("ParaPie: the social could not be started (card never appeared).");
                _pendingSince = -1f;
                AutoPlaying = false;
                ui.CloseTogetherResults();
                return;
            }
            UITogetherCard found = null;
            foreach (var item in ui.List.CurrentItems)
            {
                var ci = item as UICharacterItem;
                if (ci == null || ci.CharacterGUID != _pendingUser || ci.OfferedTogetherCards == null) continue;
                foreach (var li in ci.OfferedTogetherCards.TogetherCardsList.CurrentItems)
                {
                    var card = li as UITogetherCard;
                    if (card != null && card.gameObject.activeSelf && card.Interactable && card.CardGUID == _pendingCard) { found = card; break; }
                }
            }
            if (found == null) return;

            _pendingSince = -1f;
            PieModel.Click(found);
            Plugin.Log.LogInfo("ParaPie: played card " + _pendingCard + (ui.IsInTargetCharacterSelectionMode ? " -> auto-targeting " + _pendingTarget : ""));
            if (ui.IsInTargetCharacterSelectionMode)
            {
                ui.OnCharacterSelection(_pendingTarget);
                if (ui.IsInTargetCharacterSelectionMode)
                {
                    // The game refused the target: never leave the player stuck in "pick a Para".
                    Plugin.Log.LogWarning("ParaPie: auto-target was refused, cancelling the social.");
                    ui.OnCancelCharacterSelection();
                    ui.CloseTogetherResults();
                    AutoPlaying = false;
                }
            }
        }

        public static bool IsPending { get { return _pendingSince >= 0f; } }

        /// <summary>Close the (hidden) interaction window if the pie has nothing to show for it.</summary>
        public static void Abort(string why)
        {
            Plugin.Log.LogWarning("ParaPie: closing the social pie (" + why + ").");
            _active = false;
            try
            {
                if (_uii != null)
                {
                    PieMenu.SetHidden(_uii.UIInteractionLists.gameObject, false, false);
                    if (_uii.IsVisible) _uii.Back();
                }
            }
            catch { }
        }

        public static void Reset()
        {
            _active = false;
            _pendingSince = -1f;
            AutoPlaying = false;
        }
    }
}
