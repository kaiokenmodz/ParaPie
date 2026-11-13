#if PARAPIE_DEV
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Setting;
using UnityEngine;

namespace ParaPie
{
    /// <summary>
    /// Developer self-test. Only runs when BepInEx/config/ParaPie.selftest exists: waits for the
    /// main menu, checks the game data the mod reads, draws both pie styles on the game's UI
    /// canvas, saves screenshots next to the flag file and quits the game.
    /// </summary>
    internal class SelfTest : MonoBehaviour
    {
        public static string FlagPath { get { return Path.Combine(Paths.ConfigPath, "ParaPie.selftest"); } }

        private PieView _view;
        private float _start = -1f;
        private int _shots;
        private PieContent _social;

        private void Update()
        {
            if (_shots >= 2) return;
            if (Time.realtimeSinceStartup < 40f) return;
            if (_view == null)
            {
                Canvas best = null;
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode == RenderMode.WorldSpace) continue;
                    if (best == null || c.sortingOrder > best.sortingOrder) best = c;
                }
                if (best == null) return;
                Plugin.Log.LogInfo("SelfTest: canvas '" + best.name + "' mode=" + best.renderMode + " order=" + best.sortingOrder);
                _view = new PieView(best, PieMenu.FindFont(best, null));
                _start = Time.realtimeSinceStartup;
                _social = BuildSocialMock();
            }

            float t = Time.realtimeSinceStartup - _start;
            if (t < 3f) _view.Tick(PieModel.BuildContent(PieModel.PreviewPages(null), true));
            else _view.Tick(_social);

            if (_shots == 0 && t > 2.5f) Shot("ParaPie_selftest_together.png");
            if (_shots == 1 && t > 6f) { Shot("ParaPie_selftest_social.png"); Invoke("Quit", 3f); }
        }

        private void Shot(string name)
        {
            string path = Path.Combine(Paths.ConfigPath, name);
            ScreenCapture.CaptureScreenshot(path);
            Plugin.Log.LogInfo("SelfTest: screenshot saved to " + path);
            _shots++;
        }

        private static PieContent BuildSocialMock()
        {
            var c = new PieContent { Key = "mock-social", HubName = "Preview", Hint = string.Format(Plugin.SocialHintText.Value, "Robin", "Preview") };
            try
            {
                var together = CardInfo.Setting;
                Plugin.Log.LogInfo("SelfTest: " + together.AllTogetherCards.Length + " together cards, " + together.AllCategories.Length + " categories");
                var cats = new List<TogetherCardCategory>(together.AllCategories);
                cats.Sort((a, b) => CardInfo.CategoryOrder(a).CompareTo(CardInfo.CategoryOrder(b)));
                foreach (var cat in cats)
                {
                    int count = 0;
                    foreach (var card in together.AllTogetherCards)
                        if (card.CardType == TogetherCardType.Character && CardInfo.Category(card) == cat) count++;
                    Plugin.Log.LogInfo("SelfTest: category " + cat.DisplayName + " -> '" + CardInfo.CategoryName(cat) + "' cards=" + count + " bg=" + cat.BackgroundColor);
                    if (count == 0 || c.Slices.Count >= 7) continue;
                    c.Slices.Add(new Slice
                    {
                        Label = CardInfo.CategoryName(cat), Background = cat.BackgroundColor, TextColor = CardInfo.Readable(cat.TextColor),
                        HasChildren = true, Seed = cat.GUID, Info = count + " socials",
                    });
                }
                int shown = 0;
                foreach (var card in together.AllTogetherCards)
                {
                    if (card.CardType != TogetherCardType.Character) continue;
                    if (shown++ >= 8) break;
                    Plugin.Log.LogInfo("SelfTest: card " + card.DisplayName + " -> '" + CardInfo.Label(card, 0, 0) + "' trait=" + CardInfo.Trait(card) + " targets=" + card.TargetsAnotherCharacter);
                }
            }
            catch (System.Exception e) { Plugin.Log.LogError("SelfTest data check failed: " + e); }
            c.Slices.Add(new Slice { Label = "Hang Out", Background = Palette.Sand, TextColor = Palette.Cocoa, Seed = 11 });
            c.Slices.Add(new Slice { Label = "Invite Over", Background = Palette.Sand, TextColor = Palette.Cocoa, HasChildren = true, Seed = 12 });
            c.Slices.Add(new Slice { Label = "Ask to Move In", Background = Palette.Sand, TextColor = Palette.Cocoa, Greyed = true, Info = "Needs a higher relationship", Seed = 13 });
            return c;
        }

        private void Quit() { Application.Quit(); }
    }
}

#endif
