using System;
using HarmonyLib;
using Setting;

namespace ParaPie
{
    internal static class Patches
    {
        // Drive the pie from the character bar's own Update so it lives and dies with the game UI.
        [HarmonyPatch(typeof(UICharacters), "Update")]
        [HarmonyPostfix]
        private static void UICharactersUpdate(UICharacters __instance)
        {
            try { PieMenu.Tick(__instance); }
            catch (Exception e) { PieMenu.Fail(e); }
        }

        // The original card hover logic resets the success-chance highlight every frame for
        // cards that are not under the mouse. The cards are invisible while the pie is up,
        // so the pie owns hovering instead.
        [HarmonyPatch(typeof(UITogetherCard), "Update")]
        [HarmonyPrefix]
        private static bool UITogetherCardUpdate()
        {
            return !PieMenu.IsReplacingCards;
        }

        // Clicking another Para opens the social pie instead of the interaction list.
        [HarmonyPatch(typeof(UIInteractions), nameof(UIInteractions.Show), new[] { typeof(InteractionGroup), typeof(UnityEngine.Vector3), typeof(int), typeof(ulong), typeof(ItemObjectRoot), typeof(ulong) })]
        [HarmonyPostfix]
        private static void InteractionsShown(UIInteractions __instance)
        {
            try { SocialPie.OnInteractionsShown(__instance); }
            catch (Exception e) { PieMenu.Fail(e); }
        }

        // No more together bar: hide it (and skip its "bar full" chime).
        [HarmonyPatch(typeof(UICharactersTogetherBar), "Update")]
        [HarmonyPrefix]
        private static bool TogetherBarUpdate(UICharactersTogetherBar __instance)
        {
            try
            {
                bool hide = PieMenu.HidesTogetherBar;
                PieMenu.SetHidden(__instance.gameObject, hide, true);
                return !hide;
            }
            catch (Exception e) { PieMenu.Fail(e); return true; }
        }

        [HarmonyPatch(typeof(UICharacters), nameof(UICharacters.SetTogetherCardsMode))]
        [HarmonyPostfix]
        private static void TogetherModeStarted(UICharacters __instance)
        {
            try { PieMenu.OnTogetherModeStarted(__instance); }
            catch (Exception e) { PieMenu.Fail(e); }
        }

        [HarmonyPatch(typeof(UICharacters), nameof(UICharacters.OnTogetherCardTargetClicked))]
        [HarmonyPostfix]
        private static void TargetChosen()
        {
            PieMenu.OnCardCommitted();
        }

        [HarmonyPatch(typeof(UICharacters), nameof(UICharacters.CloseTogetherResults))]
        [HarmonyPostfix]
        private static void TogetherClosed()
        {
            PieMenu.OnTogetherClosed();
        }
    }
}
