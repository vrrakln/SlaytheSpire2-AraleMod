using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Arale.Scripts.Boss;

// Map icons use fixed boss slots, whereas vanilla room selection uses a visit counter.
// Debug travel/revisiting an act can separate those two. Select the clicked slot without
// changing room history, RNG, generated boss order, or explicitly requested encounters.
[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class ViolaBossMapEncounterPatch
{
    private static readonly MethodInfo StateGetter = AccessTools.PropertyGetter(typeof(RunManager), "State");

    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, RoomType roomType, MapPointType mapPointType, ref AbstractModel? model)
    {
        if (model != null || roomType != RoomType.Boss || mapPointType != MapPointType.Boss)
            return;
        if (StateGetter.Invoke(__instance, null) is not RunState state || !state.Act.HasSecondBoss)
            return;
        if (state.Act.BossEncounter is not ViolaBoss && state.Act.SecondBossEncounter is not ViolaBoss)
            return;
        var point = state.CurrentMapPoint;
        if (point == state.Map.BossMapPoint)
            model = state.Act.BossEncounter.ToMutable();
        else if (point != null && point == state.Map.SecondBossMapPoint)
            model = state.Act.SecondBossEncounter!.ToMutable();
    }
}

// Keep the native travel/hover tint. Only Viola's colored portrait needs
// conversion into the two map ink/paper colors; native Spine icons already do this.
[HarmonyPatch(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready))]
internal static class ViolaBossMapPalettePatch
{
    [HarmonyPostfix]
    private static void Postfix(NBossMapPoint __instance, IRunState ____runState)
    {
        var encounter = __instance.Point == ____runState.Map.SecondBossMapPoint
            ? ____runState.Act.SecondBossEncounter : ____runState.Act.BossEncounter;
        if (encounter is not ViolaBoss)
            return;
        var image = __instance.GetNode<TextureRect>("%PlaceholderImage");
        // The cream-colored silhouette multiplies with map paper into a yellow rim.
        // The portrait already has a complete transparent contour; no backing is needed.
        __instance.GetNode<TextureRect>("%PlaceholderOutline").Visible = false;
        var material = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://arale_mod/shaders/viola_map_palette.gdshader")
        };
        material.SetShaderParameter("paper_color", ____runState.Act.MapBgColor);
        image.Material = material;
    }
}
