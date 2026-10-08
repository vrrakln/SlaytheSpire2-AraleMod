using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Arale.Scripts.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
namespace Arale.Scripts.Combat;

// Extend native character tips without interfering with hitboxes or targeting.
[HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
internal static class AraleLuluHoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!CombatManager.Instance.IsInProgress || !__instance.IsAlive ||
            __instance.Player?.Character is not Arale) return;
        var power = __instance.GetPower<AraleMagicLuluPower>();
        if (power != null) __result = __result.Append(power.DumbHoverTip);
    }
}
