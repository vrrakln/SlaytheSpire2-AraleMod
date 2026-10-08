using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Unlocks;
using Arale.Scripts.Quests;

namespace Arale.Scripts.Cards;

internal static class AraleCrossCharacterPools
{
    public static IEnumerable<CardPoolModel> ForPlayer(Player player) =>
        Filter(player.Character, player.UnlockState.CharacterCardPools, AraleQuestRunData.IncludeAraleCardsFor(player));

    internal static IEnumerable<CardPoolModel> Filter(CharacterModel character, IEnumerable<CardPoolModel> pools, bool includeArale)
    {
        // Keep Arale, other mod characters, colorless cards and explicitly granted cards intact.
        return !includeArale && character.GetType().Assembly == typeof(Ironclad).Assembly
            ? pools.Where(pool => pool is not AraleCardPool)
            : pools;
    }
}

/// <summary>
/// Replace the player-specific cross-character pool lookup before any RNG is consumed.
/// Patching the async state machines directly avoids global/current-player context leaking across awaits.
/// </summary>
[HarmonyPatch]
internal static class AraleCrossCharacterPoolsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AsyncBody(AccessTools.Method(typeof(Splash), "OnPlay"));
        yield return AsyncBody(AccessTools.Method(typeof(Kaleidoscope), nameof(Kaleidoscope.AfterObtained)));
        yield return AccessTools.Method(typeof(PrismaticGem), nameof(PrismaticGem.ModifyCardRewardCreationOptions));
    }

    private static MethodInfo AsyncBody(MethodInfo method) =>
        AccessTools.Method(method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        var unlocks = AccessTools.PropertyGetter(typeof(Player), nameof(Player.UnlockState));
        var pools = AccessTools.PropertyGetter(typeof(UnlockState), nameof(UnlockState.CharacterCardPools));
        int replaced = 0;
        for (int i = 0; i + 1 < codes.Count; i++)
        {
            if (!codes[i].Calls(unlocks) || !codes[i + 1].Calls(pools)) continue;
            codes[i].opcode = OpCodes.Call;
            codes[i].operand = AccessTools.Method(typeof(AraleCrossCharacterPools), nameof(AraleCrossCharacterPools.ForPlayer));
            // Preserve labels/exception boundaries, including those attached to the second call.
            codes[i + 1].opcode = OpCodes.Nop;
            codes[i + 1].operand = null;
            replaced++;
        }
        if (replaced != 1)
            throw new InvalidOperationException($"Expected one cross-character pool lookup in {__originalMethod}, found {replaced}.");
        return codes;
    }
}
