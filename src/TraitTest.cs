#if PARAPIE_DEV
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Setting;
using UnityEngine;
using UnityEngine.UI;

namespace ParaPie
{
    /// <summary>
    /// Developer test for the ParaTraits content mod. Runs only when BepInEx/config/ParaPie.traittest
    /// exists: checks the mod's data was merged, loads the latest save, gives the selected Para some of
    /// the new traits, frees her from any conversation, runs time at the fastest speed while logging
    /// every trait moodlet / perk / quirk, and takes screenshots for the Workshop page. Removes the
    /// traits again and quits.
    /// </summary>
    internal class TraitTest : MonoBehaviour
    {
        public static string FlagPath { get { return Path.Combine(Paths.ConfigPath, "ParaPie.traittest"); } }
        private int _shot;

        private static readonly string[] TestTraits = { "PT_Foodie", "PT_MusicLover", "PT_Romantic", "PT_Stargazer", "PT_SocialButterfly", "PT_Glutton", "PT_HotHeaded" };

        private void Start() { StartCoroutine(Run()); }

        private static void L(string s) { Plugin.Log.LogInfo("TRAITTEST " + Time.realtimeSinceStartup.ToString("0") + "s: " + s); }
        private static WaitForSecondsRealtime Wait(float s) { return new WaitForSecondsRealtime(s); }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Paths.ConfigPath, "tt_" + (++_shot).ToString("00") + "_" + name + ".png"));
            L("screenshot " + name);
            yield return Wait(0.5f);
        }

        private IEnumerator Run()
        {
            yield return Wait(35f);
            var pers = Settings.Get<Personalities>();
            var mine = pers.PersonalityTraits.Where(t => t.DisplayName != null && t.DisplayName.StartsWith("PT_")).ToList();
            var se = Settings.Get<StatusEffects>();
            var auto = Settings.Get<Autonomy>();
            L("traits merged: " + mine.Count + ", moodlets: " + se.AllStatusEffects.Count(s => s.DisplayName != null && s.DisplayName.StartsWith("PT_"))
              + ", brain rules: " + Settings.Get<BrainLogic>().BrainTriggersAndLogics.Count(b => b.DisplayName != null && b.DisplayName.StartsWith("PT_"))
              + ", quirks: " + auto.ScoreBasedIdleAutonomyRules.Count(r => r.DisplayName != null && r.DisplayName.StartsWith("PT_")));
            if (mine.Count == 0) { L("FAILED mod data not loaded"); Quit(); yield break; }

            foreach (int idx in new[] { 0, -1 })
            {
                try { var menu = UI.Get<UIMainMenu>(idx); if (menu != null) { menu.ClickedContinue(); menu.ButtonContinueComponent.ButtonClick.Invoke(); break; } }
                catch { }
            }
            float t0 = Time.realtimeSinceStartup;
            while (GameLoadingManager.State != State.Game && Time.realtimeSinceStartup - t0 < 300f) yield return Wait(1f);
            yield return Wait(15f);
            RealMouse.Init();
            var player = PlayerManager.Instance.Players[0];
            var ch = AssetManager.Instance.GetCharacter(player.GetSelectedCharacterGUID());
            L("loaded; testing on " + ch.Data.ShortName);

            foreach (var n in TestTraits)
            {
                var t = mine.First(x => x.DisplayName == n);
                if (!PersonalityManager.Instance.CharacterHasTrait(ch, t.GUID)) PersonalityManager.Instance.ToggleTraitOnCharacter(ch, t.GUID);
            }
            L("traits on: " + string.Join(", ", mine.Where(t => PersonalityManager.Instance.CharacterHasTrait(ch, t.GUID)).Select(t => t.DisplayName).ToArray()));

            // Free her from any conversation so she has idle time for quirks.
            var group = SocialGroupManager.Instance.GetCharacterCurrentSocialGroup(ch.GUID);
            if (group != null) { SocialGroupManager.Instance.RemoveCharacterFromSocialGroup(group, ch.GUID); L("left her social group"); }

            ParaTime.SetTimeSpeed(2);
            double m0 = ParaTime.TotalMinutes;
            var seen = new Dictionary<string, int>();
            var did = new List<string>();
            string lastDoing = null;
            bool shotWorld = false, shotTooltip = false;
            float s0 = Time.realtimeSinceStartup;
            int tick = 0;
            while (Time.realtimeSinceStartup - s0 < 150f)
            {
                yield return Wait(3f);
                foreach (var sd in ch.Data.StatusEffectSaveData)
                {
                    var def = se.GetStatusEffectByGUID(sd.StatusEffectGUID);
                    if (def == null || def.DisplayName == null || !def.DisplayName.StartsWith("PT_")) continue;
                    if (!seen.ContainsKey(def.DisplayName))
                    {
                        seen[def.DisplayName] = 0;
                        L("MOODLET " + def.DisplayName + " '" + TranslationManager.Get("StatusEffect_" + def.DisplayName) + "' remaining=" + sd.RemainingTimeInMinutes.ToString("0") + "min, game time +" + (ParaTime.TotalMinutes - m0).ToString("0") + "min");
                    }
                    seen[def.DisplayName]++;
                }
                if (++tick % 5 == 0)
                {
                    var eligible = auto.ScoreBasedIdleAutonomyRules.Where(r => r.DisplayName != null && r.DisplayName.StartsWith("PT_")
                        && r.ContextRequirements.All(q => ContextEvaluationManager.Instance.Evaluate(q, new ContextData { CharacterGUID = ch.GUID }))).Select(r => r.DisplayName).ToArray();
                    var mineQuirks = auto.ScoreBasedIdleAutonomyRules.Count(r => r.DisplayName != null && r.DisplayName.StartsWith("PT_") && TestTraits.Any(tn => r.DisplayName.StartsWith(tn + "_")));
                    L("  quirks eligible now: " + eligible.Length + "/" + mineQuirks + " " + string.Join(", ", eligible));
                }
                var current = ch.Data.CurrentInteractionsInQueue.FirstOrDefault();
                string doing = current != null ? InteractionName(current.InteractionSettingGUID) : "(idle)";
                if (doing != lastDoing) { L("  doing: " + doing); lastDoing = doing; did.Add(doing); }

                bool hasReactionMoodlet = seen.Keys.Any(k => !k.EndsWith("FastLearner") && !k.Contains("AmongFriends"));
                if (!shotWorld && hasReactionMoodlet)
                {
                    shotWorld = true;
                    ParaTime.SetTimeSpeed(0);
                    yield return Wait(1f);
                    yield return StartCoroutine(Shot("world"));
                    yield return StartCoroutine(EmotionTooltip());
                    shotTooltip = true;
                    ParaTime.SetTimeSpeed(2);
                }
            }
            ParaTime.SetTimeSpeed(0);
            L("game minutes passed: " + (ParaTime.TotalMinutes - m0).ToString("0"));
            L("moodlets/perks seen: " + string.Join(", ", seen.Keys.ToArray()));
            L("activities: " + string.Join(" > ", did.ToArray()));
            if (!shotTooltip) { yield return StartCoroutine(Shot("world_end")); yield return StartCoroutine(EmotionTooltip()); }

            yield return StartCoroutine(ProfileShot());

            foreach (var n in TestTraits)
            {
                var t = mine.First(x => x.DisplayName == n);
                if (PersonalityManager.Instance.CharacterHasTrait(ch, t.GUID)) PersonalityManager.Instance.ToggleTraitOnCharacter(ch, t.GUID);
            }
            L("traits removed again; remaining PT traits: " + mine.Count(t => PersonalityManager.Instance.CharacterHasTrait(ch, t.GUID)));
            ParaTime.SetTimeSpeed(0);
            L("DONE");
            yield return Wait(3f);
            Quit();
        }

        /// <summary>Hovers the HUD emotion card with the real mouse so its reasons tooltip shows.</summary>
        private IEnumerator EmotionTooltip()
        {
            var card = FindObjectsByType<UIEmotionsItem2>(FindObjectsSortMode.None).FirstOrDefault(c => c.isActiveAndEnabled);
            if (card == null) { L("no emotion card found"); yield break; }
            var rt = card.GetComponent<RectTransform>();
            var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            for (int i = 0; i < 6; i++) { RealMouse.MoveTo(p + new Vector2(i % 2, i)); yield return Wait(0.3f); }
            yield return Wait(1.5f);
            yield return StartCoroutine(Shot("emotion_tooltip"));
            RealMouse.MoveTo(new Vector2(Screen.width * 0.6f, Screen.height * 0.5f));
        }

        /// <summary>Opens the profile and scrolls it to the Lifestyle &amp; Traits section.</summary>
        private IEnumerator ProfileShot()
        {
            UICharacterProfile prof = null;
            try { prof = UI.Get<UICharacterProfile>(0); prof.Show(); } catch (System.Exception e) { L("profile: " + e.Message); yield break; }
            yield return Wait(1.5f);
            var scroll = prof.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault();
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Wait(0.8f); }
            else L("no scroll view in profile");
            yield return StartCoroutine(Shot("profile_traits"));
            prof.Hide();
        }

        private static string InteractionName(ulong guid)
        {
            var i = Settings.Get<Setting.Interactions>().GetInteractionByGUID(guid);
            return i != null ? i.DisplayName : guid.ToString();
        }

        private void Quit() { L("quitting"); Application.Quit(); }
    }
}
#endif
