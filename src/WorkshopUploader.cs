#if PARAPIE_DEV
using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace ParaPie
{
    /// <summary>
    /// Developer tool: when BepInEx/config/ParaPie.upload exists (containing a mod GUID), waits for
    /// the main menu and uploads that mod with the game's own Workshop uploader, exactly like the
    /// Upload button in the Mods menu. The flag file is deleted first so it can only run once.
    /// </summary>
    internal class WorkshopUploader : MonoBehaviour
    {
        public static string FlagPath { get { return Path.Combine(Paths.ConfigPath, "ParaPie.upload"); } }

        private bool _started;
        private float _doneAt = -1f;

        private static void L(string s) { Plugin.Log.LogInfo("UPLOAD " + s); }

        private void Update()
        {
            if (_doneAt > 0f)
            {
                if (Time.realtimeSinceStartup - _doneAt > 12f) { L("quitting"); Application.Quit(); _doneAt = -1f; }
                return;
            }
            if (_started || Time.realtimeSinceStartup < 40f || GameLoadingManager.State != State.MainMenu) return;
            _started = true;

            ulong guid;
            string text = File.ReadAllText(FlagPath).Trim();
            File.Delete(FlagPath);   // one shot, never uploads twice
            string changeNotes = null;
            if (text.StartsWith("update:"))
            {
                // update:<guid>:<change notes>
                var parts = text.Split(new[] { ':' }, 3);
                text = parts[1];
                changeNotes = parts.Length > 2 ? parts[2] : "";
            }
            if (!ulong.TryParse(text, out guid)) { L("FAILED bad GUID '" + text + "'"); Finish(); return; }
            if (!Steamworks.SteamClient.IsLoggedOn) { L("FAILED Steam is not logged on"); Finish(); return; }
            var mod = ModManager.Instance.GetAssetModByGUID(guid);
            if (mod == null) { L("FAILED mod " + guid + " not found"); Finish(); return; }
            L((changeNotes != null ? "updating item " + mod.PublishedFileId + " '" : "uploading '") + mod.ModName + "' from " + mod.FilePath + " as " + Steamworks.SteamClient.Name);
            try
            {
                System.Threading.Tasks.Task task;
                if (changeNotes != null)
                {
                    if (mod.PublishedFileId == 0) { L("FAILED mod has no Workshop id"); Finish(); return; }
                    mod.TargetVisibility = (int)SteamworksService.VisibilityStatus.Public;
                    L("description now starts: " + (mod.WorkshopDescription ?? "").Substring(0, Math.Min(80, (mod.WorkshopDescription ?? "").Length)).Replace("\n", " / "));
                    task = SteamworksService.Instance.UpdateWorkshopItem(mod, changeNotes, SteamworksService.ModWorkshopStatus.WorkshopLocal);
                }
                else task = SteamworksService.Instance.UploadNewWorkshopItem(mod);
                task.GetAwaiter().OnCompleted(() =>
                {
                    if (task.Exception != null) L("FAILED " + task.Exception);
                    L("finished: PublishedFileId=" + mod.PublishedFileId + " visibility=" + mod.WorkshopVisibility);
                    L(mod.PublishedFileId != 0 && mod.WorkshopVisibility == 1 ? "SUCCESS https://steamcommunity.com/sharedfiles/filedetails/?id=" + mod.PublishedFileId : "NOT PUBLISHED (see game prompt)");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Paths.ConfigPath, "upload_result.png"));
                    Finish();
                });
            }
            catch (Exception e) { L("FAILED " + e); Finish(); }
        }

        private void Finish() { _doneAt = Time.realtimeSinceStartup; }
    }
}
#endif
