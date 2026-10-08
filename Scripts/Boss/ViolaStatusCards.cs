using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Cards;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Boss;

// =====================================================================================
//  第三幕 BOSS「薇欧拉」塞给玩家的两张状态牌（设计文档：docs\薇欧拉boss设计.md）
//  - 流言：不能被打出；回合结束时若在手牌中 → 获得 1 层易伤
//  - 黯淡：1 费可打出；打出时薇欧拉失去 20 点生命；回合结束时若在手牌中 → 获得 1 层陶醉
//
//  两张都注册进原版的「状态牌池」（StatusCardPool），因此不会进入阿拉蕾的卡池与奖励。
//  「回合结束时若在手牌中」用的就是原版状态牌的写法（HasTurnEndInHandEffect，见疑惧/灼伤）。
// =====================================================================================

/// <summary>流言：不能被打出，回合结束时若在手牌中则获得 1 层易伤。</summary>
[RegisterCard(typeof(MegaCrit.Sts2.Core.Models.CardPools.StatusCardPool))]
public sealed class Rumor : AraleCardModel
{
    public override STS2RitsuLib.Scaffolding.Content.CardAssetProfile AssetProfile => base.AssetProfile with
    {
        FrameMaterialPath = "res://arale_mod/materials/viola_status_frame.tres"
    };
    public override bool HasBuiltInOverlay => true;

    public override int MaxUpgradeLevel => 0;

    public Rumor() : base(-1, CardType.Status, CardRarity.Status, TargetType.None) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Unplayable, CardKeyword.Ethereal };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<VulnerablePower>(1m) };

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }
}

/// <summary>黯淡：1 费可打出，打出时薇欧拉失去 20 点生命；回合结束时若在手牌中则获得 1 层陶醉。</summary>
[RegisterCard(typeof(MegaCrit.Sts2.Core.Models.CardPools.StatusCardPool))]
public sealed class Bleak : AraleCardModel
{
    public override STS2RitsuLib.Scaffolding.Content.CardAssetProfile AssetProfile => base.AssetProfile with
    {
        FrameMaterialPath = "res://arale_mod/materials/viola_status_frame.tres",
        EnergyIconPath = ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool>().EnergyIconPath
    };
    public override bool HasBuiltInOverlay => true;

    /// <summary>打出时薇欧拉失去的生命。</summary>
    public const int ViolaHpLoss = 10;

    public override int MaxUpgradeLevel => 0;

    public Bleak() : base(1, CardType.Status, CardRarity.Status, TargetType.None) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HpLossVar(ViolaHpLoss),
        new DynamicVar("Intoxicated", 1m)
    };

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? viola = FindViola();
        if (viola == null || viola.IsDead)
        {
            return;
        }

        // 「失去生命」：无视格挡、不吃力量加成
        await CreatureCmd.Damage(choiceContext, viola, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, cardPlay);
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        // 走统一的陶醉施加入口（含每层固定生命损失，与设计一致）
        await AraleCmd.ApplyIntoxicated(choiceContext, Owner.Creature, DynamicVars["Intoxicated"].BaseValue, Owner, this);
    }

    private Creature? FindViola()
    {
        return CombatState?.HittableEnemies?.FirstOrDefault(c => c != null && c.IsAlive && c.Monster is Viola);
    }
}
