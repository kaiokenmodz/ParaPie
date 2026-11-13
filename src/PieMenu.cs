using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParaPie
{
    /// <summary>Pointer target for pie elements (uses the game's own EventSystem).</summary>
    internal class PieHit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public bool Hovered;
        public Action<PointerEventData.InputButton> Clicked;

        public void OnPointerEnter(PointerEventData e) { Hovered = true; }
        public void OnPointerExit(PointerEventData e) { Hovered = false; }
        public void OnPointerClick(PointerEventData e) { if (Clicked != null) Clicked(e.button); }
        private void OnDisable() { Hovered = false; }
    }

    /// <summary>Marks a game object we hid, so we can put it back exactly.</summary>
    internal class PieHiddenMarker : MonoBehaviour
    {
        public Canvas AddedCanvas;
        public CanvasGroup Group;
        public float OldAlpha;
        public bool OldBlocks;
        public LayoutElement AddedLayout;
        public LayoutElement ExistingLayout;
        public bool OldIgnore;
    }

    /// <summary>Controller: decides which pie is shown and talks to the game.</summary>
    internal static class PieMenu
    {
        private static PieView _view;
        private static bool _failed;
        private static bool _committed;
        private static bool _preview;
        private static List<PageData> _previewPages;
        private static UICharacters _ui;

        /// <summary>Character page shown in the together-mode pie. Storyteller = 0.</summary>
        internal static ulong? SelectedPage;

        /// <summary>What the pie showed last frame (read by the playtest).</summary>
        internal static PieContent LastContent;
        internal static bool WatchdogEnabled = true;

        public static bool IsReplacingCards { get { return !_failed && Plugin.Enabled != null && Plugin.Enabled.Value; } }
        public static bool HidesTogetherBar { get { return IsReplacingCards && Plugin.HideTogetherBar.Value; } }

        public static void Fail(Exception e)
        {
            if (_failed) return;
            _failed = true;
            Plugin.Log.LogError("ParaPie hit an error and switched back to the original game UI:\n" + e);
            try
            {
                SocialPie.Reset();
                if (_ui != null) HideCards(_ui, false);
                foreach (var m in Resources.FindObjectsOfTypeAll<PieHiddenMarker>())
                    if (m != null) SetHidden(m.gameObject, false, false);
                if (_view != null) _view.Destroy();
            }
            catch { }
            _view = null;
        }

        public static void Tick(UICharacters ui)
        {
            if (_failed || ui == null) return;
            _ui = ui;

            if (Plugin.ToggleKey.Value.IsDown())
            {
                Plugin.Enabled.Value = !Plugin.Enabled.Value;
                Plugin.Log.LogInfo("Pie menu " + (Plugin.Enabled.Value ? "on" : "off (original game UI)"));
            }
            if (Plugin.PreviewKey.Value.IsDown())
            {
                _preview = !_preview;
                _previewPages = null;
            }

            bool enabled = IsReplacingCards;
            HideCards(ui, enabled);
            SocialPie.TickPending(ui);

            PieContent content = null;
            bool together = ui.IsVisible && ui.IsTogetherCardsMode;
            if (together) _preview = false;

            if (Plugin.UnstickKey.Value.IsDown()) Unstick(ui, "unstick key pressed");

            if (enabled) content = SocialPie.Build();
            if (content != null && content.Slices.Count == 0)
            {
                // Never leave the (hidden) interaction window open with nothing on screen.
                SocialPie.Abort("nothing to show");
                content = null;
            }

            if (content == null && enabled && together && !ui.IsShowingOutcomes && !ui.IsInTargetCharacterSelectionMode
                && !_committed && !SocialPie.AutoPlaying)
            {
                var pages = PieModel.Gather(ui);
                if (pages.Count > 0) content = PieModel.BuildContent(pages, false);
            }
            else if (content == null && _preview && ui.IsVisible)
            {
                if (_previewPages == null) _previewPages = PieModel.PreviewPages(ui);
                content = PieModel.BuildContent(_previewPages, true);
            }

            if (content != null && (_view == null || !_view.IsAlive))
            {
                var canvas = PieView.CanvasFor(ui);
                if (canvas == null) return;
                _view = new PieView(canvas, FindFont(ui, null));
            }
            if (_view != null && _view.IsAlive) _view.Tick(content);
            LastContent = content;
            if (enabled && WatchdogEnabled) Watchdog(ui, content != null);
        }

        private static float _stuckSince = -1f;
        private static float _outcomesSince = -1f;

        /// <summary>
        /// Safety net: if the game sits in card mode (time paused, screen dimmed) with nothing the
        /// player can click (no pie, no success wheel, no results), put everything back to normal.
        /// </summary>
        private static void Watchdog(UICharacters ui, bool pieShowing)
        {
            bool wheel = false;
            try
            {
                var w = UI.GetOrNull<UISuccessChanceWheel>(ui.PlayerOwnerIndex);
                wheel = w != null && w.IsVisible;
            }
            catch { }
            bool okButton = ui.TogetherCardsOKButton != null && ui.TogetherCardsOKButton.activeInHierarchy;
            bool stuck = ui.IsTogetherCardsMode && !ui.IsShowingOutcomes && !wheel && !pieShowing && !okButton
                         && !SocialPie.IsPending
                         && (!ui.IsInTargetCharacterSelectionMode || SocialPie.AutoPlaying);
            // Results that never clear (normally they fade after a few seconds) also count as stuck.
            if (ui.IsShowingOutcomes && !ui.IsTogetherCardsMode && !okButton)
            {
                if (_outcomesSince < 0f) _outcomesSince = Time.unscaledTime;
                else if (Time.unscaledTime - _outcomesSince > 30f) { _outcomesSince = -1f; Unstick(ui, "results stayed on screen for 30 seconds"); }
            }
            else _outcomesSince = -1f;

            if (!stuck) { _stuckSince = -1f; return; }
            if (_stuckSince < 0f) { _stuckSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - _stuckSince > 4f)
            {
                _stuckSince = -1f;
                Unstick(ui, "card mode had nothing to click for 4 seconds");
            }
        }

        /// <summary>Gets the player out of any half-finished card state.</summary>
        public static void Unstick(UICharacters ui, string why)
        {
            Plugin.Log.LogWarning("ParaPie: recovering (" + why + "). State: cardMode=" + ui.IsTogetherCardsMode
                + " outcomes=" + ui.IsShowingOutcomes + " picking=" + ui.IsInTargetCharacterSelectionMode
                + " autoPlay=" + SocialPie.AutoPlaying + " committed=" + _committed);
            try { if (ui.IsInTargetCharacterSelectionMode) ui.OnCancelCharacterSelection(); } catch { }
            try { if (ui.IsTogetherCardsMode || ui.IsShowingOutcomes) ui.CloseTogetherResults(); } catch { }
            try { var uii = UI.GetOrNull<UIInteractions>(ui.PlayerOwnerIndex); if (uii != null && uii.IsVisible) uii.Back(); } catch { }
            SocialPie.Reset();
            _committed = false;
            _preview = false;
        }

        public static void SimulateOutsideClick(PointerEventData.InputButton button)
        {
            if (_view != null && _view.IsAlive) _view.SimulateOutsideClick(button);
        }

        public static void ReopenView()
        {
            if (_view != null && _view.IsAlive) _view.ForceReopen();
        }

        public static void ClosePreview() { _preview = false; }

        public static void OnTogetherModeStarted(UICharacters ui)
        {
            Plugin.Log.LogInfo("ParaPie: card mode started" + (SocialPie.AutoPlaying ? " (social from pie)" : ""));
            _committed = false;
            _preview = false;
            SelectedPage = null;
            ReopenView();
        }

        public static void OnCardCommitted() { _committed = true; }

        public static void OnTogetherClosed()
        {
            Plugin.Log.LogInfo("ParaPie: card mode closed");
            _committed = false;
            SocialPie.AutoPlaying = false;
        }

        /// <summary>Called when an option of the together-mode pie is clicked.</summary>
        public static void Pick(OptionData o, bool preview)
        {
            if (preview) { _preview = false; return; }
            if (o.Card == null || !o.Card.Interactable || _ui == null) return;
            PieModel.SetHover(_ui, o.Card, false);
            bool needsTarget = PieModel.TargetsAnotherCharacter(o.CardGUID);
            if (!needsTarget) _committed = true;
            // Cards that need a target use the game's own "pick a Para" step; the pie hides while it
            // is active and comes back if the player cancels.
            PieModel.Click(o.Card);
        }

        public static void SwitchVariant(OptionData o)
        {
            if (o.Card == null) return;
            if (_ui != null) PieModel.SetHover(_ui, o.Card, false);
            o.Card.SwitchVariant();
        }

        public static void Hover(OptionData o, bool hovered)
        {
            if (o == null || o.Card == null || _ui == null) return;
            if (hovered)
            {
                try { o.Card.HoveredAudio.PlayAudioContainer(); } catch { }
            }
            PieModel.SetHover(_ui, o.Card, hovered);
        }

        public static void KeepHover(OptionData o)
        {
            if (o == null || o.Card == null || _ui == null) return;
            PieModel.SetHover(_ui, o.Card, true);
        }

        public static ulong DefaultPage(List<PageData> pages)
        {
            try
            {
                ulong selected = _ui.PlayerOwner.Player.GetSelectedCharacterGUID();
                foreach (var p in pages) if (!p.IsStoryteller && p.CharacterGUID == selected) return p.CharacterGUID;
            }
            catch { }
            return pages[0].CharacterGUID;
        }

        internal static TMP_FontAsset FindFont(Component ui, List<PageData> pages)
        {
            if (pages != null)
                foreach (var p in pages)
                    foreach (var o in p.Options)
                        if (o.Card != null && o.Card.Label != null && o.Card.Label.Text != null && o.Card.Label.Text.font != null)
                            return o.Card.Label.Text.font;
            // Prefer the font the game uses for its Together Card labels (prefab may be inactive).
            foreach (var card in Resources.FindObjectsOfTypeAll<UITogetherCard>())
                if (card.Label != null && card.Label.Text != null && card.Label.Text.font != null) return card.Label.Text.font;
            if (ui != null)
                foreach (var t in ui.GetComponentsInChildren<TMP_Text>(true))
                    if (t.font != null) return t.font;
            return TMP_Settings.defaultFontAsset;
        }

        /// <summary>Hides the original card fans while keeping them alive (state, positions, logic).</summary>
        internal static void HideCards(UICharacters ui, bool hide)
        {
            if (ui.List != null)
            {
                foreach (var item in ui.List.CurrentItems)
                {
                    var ci = item as UICharacterItem;
                    if (ci != null && ci.OfferedTogetherCards != null) SetHidden(ci.OfferedTogetherCards.gameObject, hide, false);
                }
            }
            // Only the storyteller's cards, never the whole panel, and never anything that holds the
            // game's OK button (hiding that could leave the player with no way out of card mode).
            if (ui.StorytellerOfferedCards != null)
            {
                var go = ui.StorytellerOfferedCards.gameObject;
                bool holdsOk = ui.TogetherCardsOKButton != null && ui.TogetherCardsOKButton.transform.IsChildOf(go.transform);
                SetHidden(go, hide && !holdsOk, false);
            }
            if (ui.TogetherCardStorytellerPanel != null && ui.TogetherCardStorytellerPanel.GetComponent<PieHiddenMarker>() != null)
                SetHidden(ui.TogetherCardStorytellerPanel, false, false);   // undo what version 1/2.0 hid
        }

        /// <summary>
        /// Hides a UI object without disabling it: a disabled nested Canvas stops it drawing and
        /// receiving clicks. With collapse, it also stops taking space in its layout group.
        /// </summary>
        internal static void SetHidden(GameObject go, bool hide, bool collapse)
        {
            var marker = go.GetComponent<PieHiddenMarker>();
            if (hide)
            {
                if (marker == null)
                {
                    marker = go.AddComponent<PieHiddenMarker>();
                    if (go.GetComponent<Canvas>() == null)
                    {
                        marker.AddedCanvas = go.AddComponent<Canvas>();
                    }
                    else
                    {
                        marker.Group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                        marker.OldAlpha = marker.Group.alpha;
                        marker.OldBlocks = marker.Group.blocksRaycasts;
                    }
                    if (collapse)
                    {
                        marker.ExistingLayout = go.GetComponent<LayoutElement>();
                        if (marker.ExistingLayout != null) marker.OldIgnore = marker.ExistingLayout.ignoreLayout;
                        else marker.AddedLayout = go.AddComponent<LayoutElement>();
                    }
                }
                if (marker.AddedCanvas != null) marker.AddedCanvas.enabled = false;
                if (marker.Group != null) { marker.Group.alpha = 0f; marker.Group.blocksRaycasts = false; }
                if (marker.AddedLayout != null) marker.AddedLayout.ignoreLayout = true;
                if (marker.ExistingLayout != null) marker.ExistingLayout.ignoreLayout = true;
            }
            else if (marker != null)
            {
                if (marker.AddedCanvas != null) UnityEngine.Object.Destroy(marker.AddedCanvas);
                if (marker.Group != null) { marker.Group.alpha = marker.OldAlpha; marker.Group.blocksRaycasts = marker.OldBlocks; }
                if (marker.AddedLayout != null) UnityEngine.Object.Destroy(marker.AddedLayout);
                if (marker.ExistingLayout != null) marker.ExistingLayout.ignoreLayout = marker.OldIgnore;
                UnityEngine.Object.Destroy(marker);
            }
        }
    }
}
