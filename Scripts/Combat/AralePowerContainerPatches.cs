using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Arale.Scripts.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Arale.Scripts.Combat;

internal static class AralePowerDisplay
{
    private static readonly ConditionalWeakTable<NPowerContainer, PowerModel> ZeroDreams = new();
    internal static readonly AccessTools.FieldRef<NPowerContainer, Creature?> CreatureField =
        AccessTools.FieldRefAccess<NPowerContainer, Creature?>("_creature");
    internal static readonly AccessTools.FieldRef<NPowerContainer, List<NPower>> Nodes =
        AccessTools.FieldRefAccess<NPowerContainer, List<NPower>>("_powerNodes");
    internal static readonly MethodInfo Layout = AccessTools.Method(typeof(NPowerContainer), "UpdatePositions");
    private static readonly MethodInfo SetOwner = AccessTools.PropertySetter(typeof(PowerModel), nameof(PowerModel.Owner));

    internal static bool Applies(NPowerContainer container) => CreatureField(container)?.Player?.Character is Arale;

    internal static PowerModel ZeroDream(NPowerContainer container) => ZeroDreams.GetValue(container, c => {
        var model = ModelDb.Power<AraleDreamPower>().ToMutable();
        // UI-only model: bind for native hover/death handling, but never add it to Creature.Powers.
        // This must not fire PowerApplied or participate in gameplay hooks/save data.
        SetOwner.Invoke(model, new object?[] { CreatureField(c) });
        return model;
    });

    internal static void EnsureAndSort(NPowerContainer container)
    {
        var nodes = Nodes(container);
        if (!nodes.Any(n => n.Model is AraleDreamPower)) {
            var node = NPower.Create(ZeroDream(container));
            node.Container = container;
            nodes.Add(node);
            container.AddChild(node);
        }
        AralePowerOrder.Prioritize(nodes, n => n.Model switch {
            AraleDreamPower => 0, AraleTrainingPower => 1, _ => 2
        });
    }
}

[HarmonyPatch(typeof(NPowerContainer), "UpdatePositions")]
internal static class AralePowerLayoutPatch
{
    [HarmonyPrefix]
    private static void Prefix(NPowerContainer __instance)
    {
        if (AralePowerDisplay.Applies(__instance)) AralePowerDisplay.EnsureAndSort(__instance);
    }
}

[HarmonyPatch(typeof(NPowerContainer), nameof(NPowerContainer.SetCreature))]
internal static class AralePowerInitialLayoutPatch
{
    [HarmonyPostfix]
    private static void Postfix(NPowerContainer __instance)
    {
        // SetCreature normally never lays out a creature with no visible powers.
        if (AralePowerDisplay.Applies(__instance)) AralePowerDisplay.Layout.Invoke(__instance, null);
    }
}

[HarmonyPatch(typeof(NPowerContainer), "Add")]
internal static class AraleDreamDisplayAddedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NPowerContainer __instance, PowerModel power)
    {
        if (!AralePowerDisplay.Applies(__instance) || power is not AraleDreamPower) return true;
        var existing = AralePowerDisplay.Nodes(__instance).FirstOrDefault(n => n.Model is AraleDreamPower);
        if (existing == null) return true;
        // Reuse the same node: gaining dream after zero does not replay icon entry or shift the row.
        existing.Model = power;
        AralePowerDisplay.Layout.Invoke(__instance, null);
        return false;
    }
}

[HarmonyPatch(typeof(NPowerContainer), "Remove")]
internal static class AraleDreamDisplayRemovedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NPowerContainer __instance, PowerModel power)
    {
        if (!AralePowerDisplay.Applies(__instance) || power is not AraleDreamPower ||
            !CombatManager.Instance.IsInProgress) return true;
        var existing = AralePowerDisplay.Nodes(__instance).FirstOrDefault(n => n.Model == power);
        if (existing != null) existing.Model = AralePowerDisplay.ZeroDream(__instance);
        AralePowerDisplay.Layout.Invoke(__instance, null);
        return false;
    }
}
