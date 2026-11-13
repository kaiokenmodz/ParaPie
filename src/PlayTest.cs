#if PARAPIE_DEV
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using Setting;
using UnityEngine;

namespace ParaPie
{
    /// <summary>
    /// Developer playtest. Only runs when BepInEx/config/ParaPie.playtest exists. Loads the latest
    /// save, opens the social pie on another Para exactly like a mouse click does, starts a
    /// hangout, then plays a series of socials while logging the game state, and screenshots
    /// anything that gets stuck. Quits the game at the end.
    /// </summary>
    internal class PlayTest : MonoBehaviour
    {
        public static string FlagPath { get { return Path.Combine(Paths.ConfigPath, "ParaPie.playtest"); } }

        private const int Rounds = 4;
        private int _shot;
        private UICharacters _ui;
        private readonly System.Random _rng = new System.Random(1234);

        private void Start()
        {
            Application.logMessageReceived += OnUnityLog;
            StartCoroutine(Run());
        }

        private static void OnUnityLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (msg.StartsWith("Could not resolve") || msg.Contains("Addressables") || msg.Contains("TextDataProvider")) return;
            Plugin.Log.LogWarning("[unity " + type + "] " + msg + (type == LogType.Exception ? "\n" + stack : ""));
        }

        private static void L(string s) { Plugin.Log.LogInfo("PLAYTEST " + Time.realtimeSinceStartup.ToString("0.0") + "s: " + s); }

        private void Shot(string name)
        {
            string path = Path.Combine(Paths.ConfigPath, "pt_" + (++_shot).ToString("00") + "_" + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            L("screenshot " + Path.GetFileName(path));
        }

        private static WaitForSecondsRealtime Wait(float s) { return new WaitForSecondsRealtime(s); }

        private IEnumerator Run()
        {
            yield return Wait(35f);
            L("pressing Continue");
            bool pressed = false;
            foreach (int idx in new[] { 0, -1 })
            {
                try
                {
                    var menu = UI.Get<UIMainMenu>(idx);
                    if (menu != null) { menu.ClickedContinue(); menu.ButtonContinueComponent.ButtonClick.Invoke(); pressed = true; break; }
                }
                catch (System.Exception e) { L("main menu idx " + idx + ": " + e.Message); }
            }
            if (!pressed) { L("FAILED: could not press Continue"); yield break; }

            float t0 = Time.realtimeSinceStartup;
            while (GameLoadingManager.State != State.Game && Time.realtimeSinceStartup - t0 < 300f) yield return Wait(1f);
            if (GameLoadingManager.State != State.Game) { L("FAILED: save never finished loading"); Quit(); yield break; }
            L("save loaded");
            yield return Wait(15f);
            Shot("loaded");

            _ui = UI.Get<UICharacters>(0);
            PieMenu.WatchdogEnabled = false;   // we want to see stuck states, not have them silently fixed
            var player = PlayerManager.Instance.Players[0];
            var household = HouseholdManager.Instance.CurrentHousehold.Data.Characters;
            ulong user = player.GetSelectedCharacterGUID();
            if (user == 0 && household.Count > 0) { user = household[0]; CharacterManager.Instance.SelectCharacter(user, 0); }
            L("household=" + household.Count + " user=" + Name(user));

            int played = 0, stuck = 0, hangoutTries = 0;
            for (int round = 0; round < Rounds; round++)
            {
                ParaTime.IsPausedByPlayer = false;
                if (player.GetSelectedCharacterGUID() != user) CharacterManager.Instance.SelectCharacter(user, 0);
                yield return Wait(0.5f);

                ulong target = PickTarget(user);
                if (target == 0) { L("FAILED: nobody else on the lot to talk to"); break; }
                L("round " + round + ": clicking " + Name(target) + " | " + StateString());
                OpenInteractions(player, target);
                yield return Wait(1.2f);
                var c = PieMenu.LastContent;
                if (c == null) { L("pie did NOT open. " + StateString()); CloseInteractions(); continue; }
                L("pie: " + string.Join(", ", c.Slices.Select(s => s.Tag + ":" + s.Label).ToArray()));
                if (round == 0) Shot("pie");

                bool socials = c.Slices.Any(s => s.Tag == "card" || s.Tag == "category");
                if (!socials)
                {
                    if (++hangoutTries > 3) { L("FAILED: could not start a hangout"); break; }
                    var hang = c.Slices.FirstOrDefault(s => s.Tag == "interaction" && !s.Greyed && Regex.IsMatch(s.Label, "hang|chat|talk|greet|social|convers", RegexOptions.IgnoreCase));
                    if (hang != null) { L("starting hangout via '" + hang.Label + "'"); hang.OnClick(); }
                    else
                    {
                        L("no hangout slice found, queueing the default hangout interaction");
                        CloseInteractions();
                        QueueHangout(player, target);
                    }
                    float h0 = Time.realtimeSinceStartup;
                    while (!InGroup(user, target) && Time.realtimeSinceStartup - h0 < 90f) yield return Wait(1f);
                    L(InGroup(user, target) ? "hanging out now" : "still not hanging out after 90s");
                    if (hangoutTries == 1) Shot("hangout");
                    continue;
                }

                if (!_didScenarios)
                {
                    _didScenarios = true;
                    yield return StartCoroutine(KissScenarios(player, user, target));
                    continue;
                }

                Slice pick;
                var cats = c.Slices.Where(s => s.Tag == "category").ToList();
                if (cats.Count > 0)
                {
                    var cat = cats[_rng.Next(cats.Count)];
                    L("opening category " + cat.Label);
                    cat.OnClick();
                    yield return Wait(0.6f);
                    c = PieMenu.LastContent;
                    if (c == null) { L("pie closed after opening a category! " + StateString()); continue; }
                    if (played == 0) Shot("category");
                }
                var cards = c.Slices.Where(s => s.Tag == "card").ToList();
                if (cards.Count == 0) { L("no socials in that ring"); CloseInteractions(); continue; }
                pick = cards[_rng.Next(cards.Count)];
                L("PLAY '" + pick.Label + "' (" + pick.Info + ")");
                pick.OnClick();
                played++;

                float s0 = Time.realtimeSinceStartup;
                string last = null;
                bool isStuck = false, midShot = false;
                while (true)
                {
                    yield return Wait(0.25f);
                    string st = StateString();
                    if (st != last) { L("  " + st); last = st; }
                    float el = Time.realtimeSinceStartup - s0;
                    if (!midShot && played <= 2 && el > 2f) { Shot("social" + played); midShot = true; }
                    if (el > 1.5f && !_ui.IsTogetherCardsMode && !_ui.IsShowingOutcomes && !SocialPie.AutoPlaying && !_ui.IsInTargetCharacterSelectionMode) break;
                    if (el > 30f) { isStuck = true; break; }
                }
                if (isStuck)
                {
                    stuck++;
                    L("STUCK after '" + pick.Label + "': " + StateString());
                    Shot("STUCK");
                    PieMenu.Unstick(_ui, "playtest");
                    yield return Wait(2f);
                }
                yield return Wait(2f);
            }

            L("DONE: played=" + played + " stuck=" + stuck);
            Shot("end");
            yield return Wait(2f);
            Quit();
        }

        private bool _didScenarios;

        private static readonly string[] RealNames =
        {
            "ground", "other-para", "self-para", "hud-portraits", "right-click", "quick", "category-then-ground", "reopen-by-click",
        };

        private IEnumerator RealClick(Vector2 pos, bool right)
        {
            RealMouse.MoveTo(pos);
            yield return Wait(0.15f);
            RealMouse.Down(right);
            yield return Wait(0.08f);
            RealMouse.Up(right);
            yield return Wait(0.1f);
        }

        private static Vector2 ScreenOf(ulong guid)
        {
            var v = Visual(guid);
            var cam = PlayerManager.Instance.GetHybridPlayer(0).HybridCamera.Camera;
            if (v == null || cam == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);
            var sp = cam.WorldToScreenPoint(v.transform.position + Vector3.up * 0.9f);
            return new Vector2(sp.x, sp.y);
        }

        /// <summary>Opens the pie with a real click on the Para; returns via LastContent.</summary>
        private IEnumerator RealOpen(ulong target)
        {
            yield return StartCoroutine(Focus(target));
            yield return StartCoroutine(RealClick(ScreenOf(target), false));
            yield return Wait(1.0f);
        }

        /// <summary>Repeatedly opens the pie on the Para and clicks out, with health checks, for a while.</summary>
        private IEnumerator OpenCloseDuring(Player player, ulong target, string label, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            int n = 0;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                n++;
                int f0 = Time.frameCount;
                yield return StartCoroutine(RealOpen(target));
                bool opened = PieMenu.LastContent != null;
                yield return StartCoroutine(RealClick(Ground, false));
                yield return Wait(0.8f);
                L("  [" + label + " #" + n + "] opened=" + opened + " frames+" + (Time.frameCount - f0) + " | " + Diag(player));
                if (n == 2) Shot(label);
            }
        }

        private IEnumerator WaitCardFlow(float max)
        {
            float s0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - s0 < max)
            {
                yield return Wait(0.25f);
                if (Time.realtimeSinceStartup - s0 > 1.5f && !_ui.IsTogetherCardsMode && !_ui.IsShowingOutcomes && !SocialPie.AutoPlaying) yield break;
            }
            L("  card flow still running after " + max + "s: " + StateString());
        }

        /// <summary>Passionate kiss (social and Romance interaction), opening/closing the pie while it plays.</summary>
        private IEnumerator KissScenarios(Player player, ulong user, ulong target)
        {
            CloseInteractions();
            RealMouse.Init();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                // 1) The social card.
                yield return StartCoroutine(RealOpen(target));
                var c = PieMenu.LastContent;
                var romantic = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "category" && s.Label == "Romantic") : null;
                if (romantic != null) { romantic.OnClick(); yield return Wait(0.6f); c = PieMenu.LastContent; }
                var kiss = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "card" && s.Label.ToLower().Contains("kiss")) : null;
                L("KISS social attempt " + attempt + ": " + (kiss != null ? kiss.Label : "no kiss social offered (cooldown?)") + " | ring: " + Labels(c));
                if (kiss != null)
                {
                    kiss.OnClick();
                    yield return StartCoroutine(WaitCardFlow(25f));
                    L("KISS social outcomes done, now open/close the pie while they kiss");
                    yield return StartCoroutine(OpenCloseDuring(player, target, "kiss_social_" + attempt, 20f));
                }
                else CloseInteractions();

                // 2) The Romance... interaction submenu kiss.
                yield return Wait(1f);
                yield return StartCoroutine(RealOpen(target));
                c = PieMenu.LastContent;
                var romance = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "interaction" && s.HasChildren && s.Label.ToLower().Contains("roman")) : null;
                if (romance == null) { L("KISS no Romance submenu: " + Labels(c)); CloseInteractions(); continue; }
                romance.OnClick();
                yield return Wait(0.8f);
                c = PieMenu.LastContent;
                L("KISS Romance submenu: " + Labels(c));
                var kissI = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "interaction" && !s.Greyed && s.Label.ToLower().Contains("kiss")) : null;
                if (kissI == null) { L("KISS no kiss interaction available"); CloseInteractions(); continue; }
                L("KISS interaction: " + kissI.Label);
                kissI.OnClick();
                yield return Wait(0.5f);
                yield return StartCoroutine(OpenCloseDuring(player, target, "kiss_interaction_" + attempt, 30f));
            }
            CloseInteractions();
        }

        private static Vector2 Ground { get { return new Vector2(Screen.width * 0.18f, Screen.height * 0.80f); } }

        /// <summary>Points the camera at a Para so a real click can reach them.</summary>
        private IEnumerator Focus(ulong guid)
        {
            var hp = PlayerManager.Instance.GetHybridPlayer(0);
            var v = Visual(guid);
            if (v != null)
            {
                hp.Player.CameraCurrentCharacterFollowTarget = 0uL;
                hp.HybridCamera.FreeCamera.LookTarget.position = v.transform.position;
            }
            yield return Wait(1.5f);
        }

        /// <summary>Real OS mouse clicks: open the pie by clicking the Para, then click out in different places.</summary>
        private IEnumerator RealMouseScenarios(Player player, ulong user, ulong target)
        {
            CloseInteractions();
            RealMouse.Init();
            L("real mouse: window=" + RealMouse.Window + " foreground=" + RealMouse.IsForeground + " screen=" + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreenMode);
            ulong other = 0;
            foreach (var v in FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None))
                if (v.CharacterGUID != 0 && v.CharacterGUID != user && v.CharacterGUID != target && v.gameObject.activeInHierarchy) { other = v.CharacterGUID; break; }

            for (int i = 0; i < RealNames.Length; i++)
            {
                string n = RealNames[i];
                CloseInteractions();
                yield return Wait(1f);
                if (player.GetSelectedCharacterGUID() != user) CharacterManager.Instance.SelectCharacter(user, 0);
                if (!RealMouse.IsForeground) L("  WARNING game window is not in front, real clicks may miss");
                L("REAL " + n + " start | " + Diag(player));

                yield return StartCoroutine(Focus(target));
                RealMouse.MoveTo(ScreenOf(target));
                yield return Wait(0.3f);
                var under = player.CurrentRaycastCharacterVisual;
                L("  cursor at " + ScreenOf(target) + " over " + (under != null ? Name(under.CharacterGUID) : "no Para"));
                yield return StartCoroutine(RealClick(ScreenOf(target), false));
                yield return Wait(i == 5 ? 0.1f : 1.2f);
                L("  after clicking " + Name(target) + ": pie=" + (PieMenu.LastContent != null) + " | " + Labels(PieMenu.LastContent));
                if (PieMenu.LastContent == null) { Shot("REAL_noopen_" + n); continue; }

                if (i == 6)
                {
                    var cat = PieMenu.LastContent.Slices.FirstOrDefault(s => s.Tag == "category");
                    if (cat != null) { cat.OnClick(); yield return Wait(0.6f); }
                }

                Vector2 outAt = Ground;
                bool right = false;
                if (i == 1) { if (other != 0) outAt = ScreenOf(other); else L("  no third Para on the lot, using ground"); }
                if (i == 2) outAt = ScreenOf(user);
                if (i == 3) outAt = new Vector2(Screen.width * 0.12f, Screen.height * 0.05f);
                if (i == 4) right = true;
                L("  clicking out at " + outAt + (right ? " (right)" : ""));
                yield return StartCoroutine(RealClick(outAt, right));

                if (i == 7)
                {
                    yield return StartCoroutine(RealClick(ScreenOf(target), false));
                    yield return Wait(1.2f);
                    L("  reopened by clicking again: pie=" + (PieMenu.LastContent != null));
                    yield return StartCoroutine(RealClick(Ground, false));
                }

                int f0 = Time.frameCount;
                double m0 = ParaTime.TotalMinutes;
                yield return Wait(2.5f);
                L("REAL " + n + " after 2.5s: frames+" + (Time.frameCount - f0) + " gameMinutes+" + (ParaTime.TotalMinutes - m0).ToString("0.0") + " | " + Diag(player));
                Shot("real_" + n);

                yield return StartCoroutine(Focus(target));
                yield return StartCoroutine(RealClick(ScreenOf(target), false));
                yield return Wait(1.2f);
                bool ok = PieMenu.LastContent != null;
                L("REAL " + n + (ok ? " OK - pie reopens" : " PROBLEM - pie does not reopen") + " | " + Diag(player));
                if (!ok) Shot("REAL_PROBLEM_" + n);
                yield return StartCoroutine(RealClick(Ground, false));
                yield return Wait(0.8f);
            }
            CloseInteractions();
        }

        private static readonly string[] ScenarioNames =
        {
            "root-leftclick", "category-leftclick", "submenu-leftclick", "root-rightclick", "escape",
            "double-click", "click-during-open", "click-during-category", "clickout-then-reopen",
        };

        /// <summary>Every way of "clicking out" of the pie, each followed by a health check.</summary>
        private IEnumerator ClickOutScenarios(Player player, ulong user, ulong target)
        {
            var L1 = PointerEventDataButton(0);
            var R1 = PointerEventDataButton(1);
            for (int i = 0; i < ScenarioNames.Length; i++)
            {
                string n = ScenarioNames[i];
                CloseInteractions();
                yield return Wait(0.8f);
                if (player.GetSelectedCharacterGUID() != user) CharacterManager.Instance.SelectCharacter(user, 0);
                L("SCENARIO " + n + " start | " + Diag(player));
                OpenInteractions(player, target);
                yield return Wait(i == 6 ? 0.05f : 1.0f);
                var c = PieMenu.LastContent;
                L("  pie: " + Labels(c));

                if (i == 1 || i == 7)
                {
                    var cat = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "category") : null;
                    if (cat != null) { L("  opening category " + cat.Label); cat.OnClick(); yield return Wait(i == 7 ? 0.05f : 0.6f); }
                }
                if (i == 2)
                {
                    var sub = c != null ? c.Slices.FirstOrDefault(s => s.Tag == "interaction" && s.HasChildren) : null;
                    if (sub != null) { L("  opening submenu " + sub.Label); sub.OnClick(); yield return Wait(0.6f); L("  pie now: " + Labels(PieMenu.LastContent)); }
                    else L("  no interaction submenu available");
                }

                if (i == 3) PieMenu.SimulateOutsideClick(R1);
                else if (i == 4) { var u = UI.GetOrNull<UIInteractions>(0); if (u != null) u.Back(); }
                else if (i == 5) { PieMenu.SimulateOutsideClick(L1); PieMenu.SimulateOutsideClick(L1); }
                else PieMenu.SimulateOutsideClick(L1);

                if (i == 8)
                {
                    yield return Wait(0.1f);
                    OpenInteractions(player, target);
                    yield return Wait(1f);
                    L("  reopened right away: pie=" + (PieMenu.LastContent != null));
                    PieMenu.SimulateOutsideClick(L1);
                }

                int f0 = Time.frameCount;
                double m0 = ParaTime.TotalMinutes;
                yield return Wait(2.5f);
                L("SCENARIO " + n + " after 2.5s: frames+" + (Time.frameCount - f0) + " gameMinutes+" + (ParaTime.TotalMinutes - m0).ToString("0.0") + " | " + Diag(player));
                if (i == 0) Shot("after_clickout");

                OpenInteractions(player, target);
                yield return Wait(1f);
                bool ok = PieMenu.LastContent != null;
                L("SCENARIO " + n + (ok ? " OK - pie reopens" : " PROBLEM - pie does not reopen") + " | " + Diag(player));
                if (!ok) Shot("PROBLEM_" + n);
                PieMenu.SimulateOutsideClick(L1);
                yield return Wait(0.8f);
            }
        }

        private static UnityEngine.EventSystems.PointerEventData.InputButton PointerEventDataButton(int i)
        {
            return i == 0 ? UnityEngine.EventSystems.PointerEventData.InputButton.Left : UnityEngine.EventSystems.PointerEventData.InputButton.Right;
        }

        private static string Labels(PieContent c)
        {
            return c == null ? "(none)" : string.Join(", ", c.Slices.Select(s => s.Tag + ":" + s.Label).ToArray());
        }

        private static readonly System.Reflection.FieldInfo VisibleWindowsField = HarmonyLib.AccessTools.Field(typeof(UIManager), "_visibleWindows");

        private string Diag(Player player)
        {
            string windows = "?";
            bool back = false;
            try
            {
                var mgr = PlayerManager.Instance.GetHybridPlayer(0).UIManager;
                back = mgr.HasVisibleWindowRegisteredForBackButton;
                var list = VisibleWindowsField.GetValue(mgr) as List<UIWindow>;
                if (list != null) windows = string.Join("/", list.Where(w => w != null).Select(w => w.GetType().Name).ToArray());
            }
            catch { }
            int markers = Resources.FindObjectsOfTypeAll<PieHiddenMarker>().Length;
            return StateString() + " blocked=" + player.PlayerActionBlockedByUIWindow + " backWin=" + back + " windows=[" + windows + "] markers=" + markers
                + " overUI=" + InputManager.Instance.IsPointerOverUI(0) + " selected=" + player.SelectedCharactersGUID.Count;
        }

        private string StateString()
        {
            bool wheel = false;
            try { var w = UI.GetOrNull<UISuccessChanceWheel>(0); wheel = w != null && w.IsVisible; } catch { }
            bool uii = false;
            try { var u = UI.GetOrNull<UIInteractions>(0); uii = u != null && u.IsVisible; } catch { }
            return "cardMode=" + _ui.IsTogetherCardsMode + " outcomes=" + _ui.IsShowingOutcomes + " picking=" + _ui.IsInTargetCharacterSelectionMode
                + " wheel=" + wheel + " auto=" + SocialPie.AutoPlaying + " pending=" + SocialPie.IsPending + " pie=" + (PieMenu.LastContent != null)
                + " menu=" + uii + " paused=" + ParaTime.IsPaused + " ok=" + (_ui.TogetherCardsOKButton != null && _ui.TogetherCardsOKButton.activeInHierarchy);
        }

        private static string Name(ulong guid)
        {
            var c = guid != 0 ? AssetManager.Instance.GetCharacter(guid) : null;
            return c != null ? c.Data.ShortName + "#" + (guid % 10000) : "none";
        }

        private static bool InGroup(ulong a, ulong b)
        {
            var g = SocialGroupManager.Instance.GetCharacterCurrentSocialGroup(a);
            return g != null && g.CharactersInGroup.Contains(b);
        }

        private static ulong PickTarget(ulong user)
        {
            var g = SocialGroupManager.Instance.GetCharacterCurrentSocialGroup(user);
            if (g != null)
                foreach (var ch in g.CharactersInGroup)
                    if (ch != user && Visual(ch) != null) return ch;
            var mine = Visual(user);
            ulong best = 0;
            float bestD = float.MaxValue;
            foreach (var v in FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None))
            {
                if (v.CharacterGUID == 0 || v.CharacterGUID == user || !v.gameObject.activeInHierarchy) continue;
                var c = AssetManager.Instance.GetCharacter(v.CharacterGUID);
                if (c == null || c.Data.IsDead) continue;
                float d = mine != null ? Vector3.Distance(mine.transform.position, v.transform.position) : 0f;
                if (d < bestD) { bestD = d; best = v.CharacterGUID; }
            }
            return best;
        }

        private static CharacterVisual Visual(ulong guid)
        {
            foreach (var v in FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None))
                if (v.CharacterGUID == guid && v.gameObject.activeInHierarchy) return v;
            return null;
        }

        /// <summary>Same call the game makes when you click a Para in the world.</summary>
        private static void OpenInteractions(Player player, ulong target)
        {
            var interactions = Settings.Get<Interactions>();
            ulong groupGUID = InteractionManager.Instance.GetInteractionGroupGUIDFor(interactions, player.SelectedCharactersGUID, target);
            var group = interactions.GetInteractionGroupByGUID(groupGUID);
            var v = Visual(target);
            Vector3 pos = v != null ? v.transform.position : Vector3.zero;
            ulong lot = LotManager.Instance.GetLotFromPosition(pos);
            UI.Get<UIInteractions>(0).Show(group, pos, -1, target, null, lot);
        }

        private static void CloseInteractions()
        {
            var u = UI.GetOrNull<UIInteractions>(0);
            if (u != null && u.IsVisible) u.Back();
        }

        private static void QueueHangout(Player player, ulong target)
        {
            var v = Visual(target);
            var t = new InteractionTarget { CharacterGUID = target, WorldPosition = v != null ? v.transform.position : Vector3.zero, ItemInstanceID = -1 };
            ulong lot = LotManager.Instance.GetLotFromPosition(t.WorldPosition);
            InteractionManager.Instance.AddToInteractionQueueOfSelectedCharactersFromPlayerInput(0, Settings.Get<Interactions>().DefaultHangoutInteraction, t, lot, 0);
        }

        private void Quit() { L("quitting"); Application.Quit(); }
    }
}

#endif
