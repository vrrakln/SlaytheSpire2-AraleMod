using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

// ============================ 基础牌（打击 / 防御） ============================

/// <summary>打击：造成 6 点伤害（与文档示例一致）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class AraleStrike : AraleCardModel
{
    public AraleStrike()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    /// <summary>
    /// 打上原版「打击」标签。注意：原版区分「初始牌打击」靠的是**稀有度 + 标签**——
    /// <c>CardModel.IsBasicStrikeOrDefend</c> = <c>Rarity == CardRarity.Basic</c> 且带打击/防御标签，
    /// 所以树叶药膏/潘多拉魔盒/虚无种子这类只作用于初始牌的效果，依赖的就是这里的 Basic + Strike 组合。
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

/// <summary>防御：获得 5 点格挡。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class AraleDefend : AraleCardModel
{
    public AraleDefend()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    /// <summary>
    /// 打上原版「防御」标签（同样是「Basic 稀有度 + 标签」才算初始牌防御，见打击处的说明）。
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

// ============================ 初始牌（序号 1、2） ============================

/// <summary>序号 1 · 梦的开始：获得梦想。古老牙齿可将其转化为「梦幻开局」。</summary>
[RegisterCard(typeof(AraleCardPool))]
[RegisterArchaicToothTranscendence(typeof(DreamOpening))]
public sealed class DreamStart : AraleCardModel
{
    public DreamStart()
        : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dream"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 2 · 逐梦：对所有敌人造成 3 点伤害 3 次。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ChaseDream : AraleCardModel
{
    public ChaseDream()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AllEnemies)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(3)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}

// ============================ 衍生牌（序号 90） ============================

/// <summary>
/// 序号 90 · 肌肉棒棒：衍生牌，无色（注册到无色卡池，因此不消耗梦想）。
/// 0 费、保留、消耗，造成 3 点伤害并锻炼 1。
/// </summary>
[RegisterCard(typeof(ColorlessCardPool))]
public sealed class MuscleBar : AraleCardModel
{
    // 衍生牌仅由专属效果明确生成，不进入工具箱等战斗随机生成候选。
    public override bool CanBeGeneratedInCombat => false;
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public MuscleBar()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust, CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Train", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        decimal train = DynamicVars["Train"].BaseValue;
        var bonus = Owner.Creature.GetPower<AraleMuscleBarTrainingPower>();
        if (bonus != null && bonus.Amount > 0)
        {
            train += bonus.Amount;
        }
        await AraleCmd.Train(choiceContext, Owner, train, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
