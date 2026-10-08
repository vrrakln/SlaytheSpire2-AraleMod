using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace Arale.Scripts;

/// <summary>阿拉蕾专属卡池。</summary>
public sealed class AraleCardPool : TypeListCardPoolModel
{
    // 彩色成品卡框使用恒等 HSV 材质，避免默认无色材质将它去色。
    private static readonly Material FrameColors = MaterialUtils.CreateUnmodulatedHsvShaderMaterial();
    public override Material? PoolFrameMaterial => FrameColors;

    /// <summary>卡池 ID（须唯一，避免与其他 mod 撞车）。</summary>
    public override string Title => "arale";

    public override string EnergyColorName => AraleEnergy.ColorName;
    public override string? BigEnergyIconPath => AraleEnergy.CardIcon;
    public override string? TextEnergyIconPath => AraleEnergy.TextIcon;

    /// <summary>多人牌组与历史记录中小卡牌的亮金黄色底色。</summary>
    public override Color DeckEntryCardColor => new Color("FFD85A");

    /// <summary>能量表盘文字描边颜色。</summary>
    public override Color EnergyOutlineColor => new Color("594016FF");

    public override bool IsColorless => false;
}

/// <summary>阿拉蕾专属遗物池。</summary>
public sealed class AraleRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => AraleEnergy.ColorName;
    public override string? BigEnergyIconPath => AraleEnergy.CardIcon;
    public override string? TextEnergyIconPath => AraleEnergy.TextIcon;
}

/// <summary>阿拉蕾专属药水池。</summary>
public sealed class AralePotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => AraleEnergy.ColorName;
    public override string? BigEnergyIconPath => AraleEnergy.CardIcon;
    public override string? TextEnergyIconPath => AraleEnergy.TextIcon;
}
