using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Arale.Scripts.Boss;

// 原生 FromMonster 固定选择所有对手，且没有公开的单目标重设接口。
// 仅收窄薇欧拉正在执行的纵火命令，保留原生伤害、反击和攻击前后钩子。
[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class ViolaArsonTargeting
{
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.Attacker?.Monster is Viola viola &&
            ReferenceEquals(viola.CurrentArsonAttack, __instance) && viola.CurrentArsonTarget is { } target)
            __result = new[] { target };
    }
}
