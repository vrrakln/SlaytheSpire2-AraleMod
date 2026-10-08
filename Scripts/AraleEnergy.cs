using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Arale.Scripts;

internal static class AraleEnergy
{
    public const string ColorName = "arale";
    public const string CardIcon = "res://arale_mod/images/energy/card_energy.png";
    public const string TextIcon = "res://images/packed/sprite_fonts/arale_energy_icon.png";
    public const string CounterIcon = "res://arale_mod/images/energy/counter_energy.png";
}

// 保留铁甲战士表盘的数字刷新、悬浮提示、进出动画与零能量变暗逻辑。
[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter.Create))]
internal static class AraleEnergyCounterArt
{
    [HarmonyPostfix]
    private static void Postfix(Player player, NEnergyCounter? __result)
    {
        if (__result is null || player.Character is not Arale)
            return;

        var texture = ResourceLoader.Load<Texture2D>(AraleEnergy.CounterIcon);
        if (texture is null)
            return;

        __result.GetNode<TextureRect>("Layers/Layer1").Texture = texture;
        // 猫耳轮廓保持竖直；只隐藏原表盘装饰，不隐藏能量增加时 Restart 的特效容器。
        foreach (var path in new[] { "Layers/RotationLayers", "Layers/Layer4", "Layers/Layer5" })
            __result.GetNode<CanvasItem>(path).Visible = false;

        // 复用本体的恢复光晕和扩散光环，改成阿拉蕾的亮黄色；去掉铁甲战士火焰。
        var back = __result.GetNode<CanvasItem>("EnergyVfxBack");
        var front = __result.GetNode<CanvasItem>("EnergyVfxFront");
        back.Visible = front.Visible = true;
        back.GetNode<CanvasItem>("vfx_common_glow").SelfModulate = new Color("FFE475");
        foreach (var child in front.GetChildren())
        {
            if (child is not CanvasItem effect) continue;
            effect.Visible = effect.Name == "vfx_common_ring_polar_a";
            if (effect.Visible) effect.SelfModulate = new Color("FFD84A");
        }
    }
}
