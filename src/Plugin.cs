using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ParaPie
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.parapie.paralives.piemenu";
        public const string Name = "ParaPie - Pie Menu for Together Cards";
        public const string Version = "2.0.2";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> SocialPie;
        internal static ConfigEntry<bool> HideTogetherBar;
        internal static ConfigEntry<bool> ShowAllSocials;
        internal static ConfigEntry<int> FlatSocialLimit;
        internal static ConfigEntry<string> SocialHintText;
        internal static ConfigEntry<string> NotTogetherHintText;
        internal static ConfigEntry<float> MenuScale;
        internal static ConfigEntry<bool> PlayfulTilt;
        internal static ConfigEntry<bool> FollowMouse;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<KeyboardShortcut> PreviewKey;
        internal static ConfigEntry<KeyboardShortcut> UnstickKey;
        internal static ConfigEntry<string> HintText;
        internal static ConfigEntry<string> StorytellerText;

        private void Awake()
        {
            Log = Logger;
            try { BepInEx.Logging.Logger.Listeners.Add(new FlushLog(Logger)); } catch { }
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false the game's original cards, bar and menus are used.");
            SocialPie = Config.Bind("Socializing", "ClickParaForSocialPie", true,
                "Clicking another Para opens a Sims-style pie with every social you can do with them right now, plus their normal interactions.");
            HideTogetherBar = Config.Bind("Socializing", "HideTogetherBar", true,
                "Remove the together bar (no more waiting for it to fill). Set false to keep it as an extra way to get a card hand.");
            ShowAllSocials = Config.Bind("Socializing", "ShowAllSocials", true,
                "List every social the Para qualifies for (cooldowns and trait requirements still apply). False = a random hand like the cards.");
            FlatSocialLimit = Config.Bind("Socializing", "GroupIntoCategoriesAbove", 6,
                "If there are more socials than this, they are grouped into categories (Friendly, Funny, Romantic...).");
            SocialHintText = Config.Bind("Text", "SocialHint", "What should {0} do with {1}?",
                "Shown under the social pie. {0} = your Para, {1} = the Para you clicked.");
            NotTogetherHintText = Config.Bind("Text", "NotTogetherHint", "Hang out with {1} to unlock socials",
                "Shown when the two Paras are not hanging out yet. {0} = your Para, {1} = the Para you clicked.");
            MenuScale = Config.Bind("Look", "MenuScale", 1f,
                new ConfigDescription("Overall size of the pie menu.", new AcceptableValueRange<float>(0.6f, 1.8f)));
            PlayfulTilt = Config.Bind("Look", "PlayfulTilt", true,
                "Give each option a tiny tilt, like the hand-placed cards in the original UI.");
            FollowMouse = Config.Bind("Look", "OpenAtMouse", false,
                "Open the pie where the mouse is (Sims style) instead of at the centre of the screen.");
            ToggleKey = Config.Bind("Keys", "ToggleCardsAndPie", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftControl),
                "Switch between the pie menu and the original cards at any time.");
            UnstickKey = Config.Bind("Keys", "Unstick", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Emergency button: if the game ever seems stuck paused after a social, this closes card mode and any open menu.");
            PreviewKey = Config.Bind("Keys", "PreviewPie", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Open a sample pie with dummy options so you can check the look (clicking an option does nothing).");
            HintText = Config.Bind("Text", "Hint", "Choose what to do together",
                "Shown under the character's name while nothing is hovered.");
            StorytellerText = Config.Bind("Text", "Storyteller", "Storyteller",
                "Name shown in the centre for storyteller cards.");

            try
            {
                new Harmony(Guid).PatchAll(typeof(Patches));
                Log.LogInfo(Name + " " + Version + " loaded. Ctrl+F7 toggles pie/cards, Ctrl+F8 shows a preview.");
#if PARAPIE_DEV
                if (System.IO.File.Exists(SelfTest.FlagPath))
                {
                    var go = new GameObject("ParaPieSelfTest");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    go.AddComponent<SelfTest>();
                    Log.LogInfo("Self-test mode active.");
                }
                if (System.IO.File.Exists(PlayTest.FlagPath))
                {
                    var go = new GameObject("ParaPiePlayTest");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    go.AddComponent<PlayTest>();
                    Log.LogInfo("Playtest mode active.");
                }
                if (System.IO.File.Exists(TraitTest.FlagPath))
                {
                    var go = new GameObject("ParaPieTraitTest");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    go.AddComponent<TraitTest>();
                    Log.LogInfo("Trait test mode active.");
                }
                if (System.IO.File.Exists(WorkshopUploader.FlagPath))
                {
                    var go = new GameObject("ParaPieUploader");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    go.AddComponent<WorkshopUploader>();
                    Log.LogInfo("Workshop upload mode active.");
                }
#endif
            }
            catch (System.Exception e)
            {
                Log.LogError("Failed to patch Paralives, the pie menu is disabled. The game may have updated.\n" + e);
            }
        }
    }
}
