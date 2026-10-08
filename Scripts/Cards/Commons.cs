using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

// =====================================================================================
//  普通卡（序号 3–22）· 文本与数值以 docs\角色卡池.md 为准
//  说明：v1 简化实现的地方都在注释里标注了「v1」。
// =====================================================================================

/// <summary>序号 3：0 费，造成 3 点伤害 2 次。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Assault : AraleCardModel
{
    public Assault() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}

/// <summary>序号 4：0 费，保留，获得 3 点格挡，抽 1 张牌，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Disguise : AraleCardModel
{
    public Disguise() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(4m, ValueProp.Move), new CardsVar(1) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

/// <summary>序号 5：0 费，获得 4 点格挡，锻炼 2。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Unstoppable : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public Unstoppable() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(4m, ValueProp.Move),
        new DynamicVar("Train", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Train"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 6：1 费，随机对敌人造成 6 点伤害 2 次。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LuckyAudience : AraleCardModel
{
    public LuckyAudience() : base(1, CardType.Attack, CardRarity.Common, TargetType.RandomEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay)
            .TargetingRandomOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 7：1 费，造成 8 点伤害，给予 1 层歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class BackupVocals : AraleCardModel
{
    public BackupVocals() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Song", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, DynamicVars["Song"].BaseValue, Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Song"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 8：1 费，造成 7 点伤害，将 1 张肌肉棒棒放入手牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class VigorPunch : AraleCardModel
{
    public VigorPunch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move),
        new CardsVar(1)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        await AraleCmd.CreateMuscleBars(choiceContext, Owner, CombatState, DynamicVars.Cards.IntValue, overflowToDiscard: true);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 9：1 费，给予 2 层易伤，获得 1 点梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Cheer : AraleCardModel
{
    public Cheer() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<VulnerablePower>(2m),
        new DynamicVar("Dream", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Vulnerable.UpgradeValueBy(1m);
}

/// <summary>序号 10：1 费，失去 1 点梦想，给予所有敌人 2 层虚弱。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LingeringDread : AraleCardModel
{
    public LingeringDread() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("DreamCost", 1m),
        new PowerVar<WeakPower>(2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.LoseDream(choiceContext, Owner, DynamicVars["DreamCost"].BaseValue, this);
        await PowerCmd.Apply<WeakPower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1m);
}

/// <summary>序号 11：1 费，获得 7 点格挡，获得 1 点梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class MarySue : AraleCardModel
{
    public MarySue() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("Dream", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

/// <summary>序号 12：1 费，给予 2 层歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SoftSing : AraleCardModel
{
    public SoftSing() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Song", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, DynamicVars["Song"].BaseValue, Owner, this);
    }

    protected override void OnUpgrade() => DynamicVars["Song"].UpgradeValueBy(2m);
}

/// <summary>序号 13：1 费，将 2 张肌肉棒棒放入手牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Transformation : AraleCardModel
{
    public Transformation() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.CreateMuscleBars(choiceContext, Owner, CombatState, DynamicVars.Cards.IntValue, overflowToDiscard: true);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>序号 14：1 费，锻炼 2，下回合获得 2 点能量（升级后各 +1）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class CalorieRefill : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public CalorieRefill() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Train", 2m),
        new EnergyVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
        // 原版「充能电池」「放松」同款：下回合开始时的能量用 EnergyNextTurnPower 记账
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Train"].UpgradeValueBy(1m);
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}

/// <summary>序号 15：1 费，获得 6 点格挡，为一张手牌添加保留（v1：随机一张，未升级时同原版「真勇气」做法）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Relief : AraleCardModel
{
    public Relief() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(6m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var hand = Owner.PlayerCombatState?.Hand?.Cards;
        if (hand == null)
        {
            return;
        }
        var picked = (await CardSelectCmd.FromHand(
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
            context: choiceContext,
            player: Owner,
            filter: (CardModel c) => !c.Keywords.Contains(CardKeyword.Retain),
            source: this)).FirstOrDefault();
        if (picked != null)
        {
            AraleCmd.AddRetain(picked);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>序号 16：1 费，保留，造成 4 点伤害，给予 1 层易伤。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class TightHug : AraleCardModel
{
    public TightHug() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}

/// <summary>序号 17：1 费，锻炼 4，下回合抽 1 张牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LimitedSticker : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public LimitedSticker() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Train", 4m),
        new CardsVar(1)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Train"].UpgradeValueBy(2m);
}

/// <summary>序号 18：1 费，给予 1 层歌声，抽 2 张牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Harmony : AraleCardModel
{
    public Harmony() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Song", 1m),
        new CardsVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, DynamicVars["Song"].BaseValue, Owner, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Song"].UpgradeValueBy(1m);
}

/// <summary>序号 19：2 费，虚无，造成 7 点伤害 3 次。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class RapidAttack : AraleCardModel
{
    public RapidAttack() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Ethereal };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(7m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(3).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 20：2 费，造成 12 点伤害，如果你本回合获得过梦想，则获得 2 点能量。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SelfDoubt : AraleCardModel
{
    public SelfDoubt() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    /// <summary>金边：本回合获得过梦想（打出后才会额外给能量）时亮起。</summary>
    protected override bool ShouldGlowGoldInternal => Owner != null && AraleCmd.GainedDreamThisTurn(Owner.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(12m, ValueProp.Move),
        new EnergyVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        if (AraleCmd.GainedDreamThisTurn(Owner.Creature))
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>序号 21：2 费，获得 10 点格挡，给予 2 层歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Declaration : AraleCardModel
{
    public Declaration() : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(11m, ValueProp.Move),
        new DynamicVar("Song", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, DynamicVars["Song"].BaseValue, Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Song"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 22：3 费，保留，对所有敌人造成 9 点伤害 2 次。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class PublicMoment : AraleCardModel
{
    public PublicMoment() : base(3, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}