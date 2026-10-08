using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts;

// =====================================================================================
//  阿拉蕾专属药水（普通 / 罕见 / 稀有 各 1 瓶）
//  设计文档：docs\阿拉蕾专属药水.md
//  图标：使用已确认的五角星梦想源泉、果汁瓶型牛奶、盒状八音盒药水。
// =====================================================================================

/// <summary>阿拉蕾专属药水的本体与轮廓图。</summary>
internal static class AralePotionArt
{
    private const string Root = "res://arale_mod/images/potions/";

    public static string Image(string id) => Root + id + ".png";

    public static string Outline(string id) => Root + "outlines/" + id + ".png";
}

/// <summary>梦想源泉（普通）：获得 3 点梦想。</summary>
[RegisterPotion(typeof(AralePotionPool))]
public sealed class AraleDreamFountainPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    public override string? CustomImagePath => AralePotionArt.Image(nameof(AraleDreamFountainPotion));

    public override string? CustomOutlinePath => AralePotionArt.Outline(nameof(AraleDreamFountainPotion));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 3m) };

    // 注意：RitsuLib 的 ModPotionTemplate.ExtraHoverTips 是 sealed（与卡牌的 ModCardTemplate 一样），
    // 扩展点是 AdditionalHoverTips。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<AraleDreamPower>() };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, null);
    }}

/// <summary>牛奶（罕见）：锻炼 10，抽 2 张牌。</summary>
[RegisterPotion(typeof(AralePotionPool))]
public sealed class AraleMilkPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    public override string? CustomImagePath => AralePotionArt.Image(nameof(AraleMilkPotion));

    public override string? CustomOutlinePath => AralePotionArt.Outline(nameof(AraleMilkPotion));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Train", 10m),
        new CardsVar(2)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<AraleTrainingPower>(),
        HoverTipFactory.FromPower<AraleMagicLuluPower>()
    };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        // 锻炼 10 正好达到阈值，会清空锻炼并进入魔法露露（与卡牌的「锻炼」结算完全一致）
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, null);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }
}

/// <summary>八音盒（稀有）：给予所有敌人 2 层陶醉。</summary>
[RegisterPotion(typeof(AralePotionPool))]
public sealed class AraleMusicBoxPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AllEnemies;

    public override string? CustomImagePath => AralePotionArt.Image(nameof(AraleMusicBoxPotion));

    public override string? CustomOutlinePath => AralePotionArt.Outline(nameof(AraleMusicBoxPotion));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<AraleIntoxicatedPower>("Intoxicated", 2m)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<AraleIntoxicatedPower>() };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var enemies = Owner.Creature.CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0)
        {
            return;
        }
        await PowerCmd.Apply<AraleIntoxicatedPower>(
            choiceContext, enemies, DynamicVars["Intoxicated"].BaseValue, Owner.Creature, null);
    }
}
