using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace Arale.Scripts;

// The selection screen uses this localization key; other UI uses CharacterModel.Title.
[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CharacterSelectTitle), MethodType.Getter)]
internal static class AraleCharacterSelectTitlePatch
{
    [HarmonyPostfix]
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is Arale)
            __result = "ARALE_MOD_CHARACTER_ARALE.characterSelectTitle";
    }
}
