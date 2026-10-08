using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

// =====================================================================================
//  多人游戏牌（序号 93–97）
//  实现方式参考原版多人牌：EnergySurge（全体队友获得能量）、HuddleUp（为队友抽牌）、
//  BelieveInYou（选择 1 名友方）、Flanking（挂目标状态的多人牌）等。
//  这些牌都设置了 CardMultiplayerConstraint.MultiplayerOnly，因此只会在多人模式出现。
// =====================================================================================

/// <summary>序号 93「群愿」：所有玩家获得等同于你当前梦想层数的力量，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class GroupWish : AraleCardModel
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    public GroupWish() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal strength = AraleCmd.Dream(Owner.Creature);
        if (strength <= 0m)
        {
            return;
        }
        var players = CombatState?.PlayerCreatures?.Where(c => c != null && c.IsAlive).ToList();
        if (players == null)
        {
            return;
        }
        foreach (var player in players)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, player, strength, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 94「梦蝶」：战斗结束时，为随机 1 名玩家和自己回复生命。</summary>
/// <remarks>9/15 削弱：回复量 12/15 → **8/10**（文本同时补上「战斗结束时」的时机说明）。</remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DreamButterfly : AraleCardModel
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public DreamButterfly() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new HealVar(8m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 回复量作为层数记在能力上，实际回复在战斗结束时结算（见 AraleDreamButterflyPower）
        await PowerCmd.Apply<AraleDreamButterflyPower>(choiceContext, Owner.Creature, DynamicVars.Heal.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Heal.UpgradeValueBy(2m);
}

/// <summary>序号 95「呕心沥血」：选择 1 名玩家，其失去生命，你获得梦想。</summary>
/// <remarks>9/14 第二次平衡调整：梦想 4/5 → 3/4（1 费换 3 点梦想太划算）。</remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class BloodAndSweat : AraleCardModel
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public BloodAndSweat() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HpLossVar(1m),
        new DynamicVar("Dream", 3m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, cardPlay);
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade() => DynamicVars["Dream"].UpgradeValueBy(1m);
}

/// <summary>
/// 序号 96「异口同声」：给予选定敌人 1 层歌声，本回合其他玩家每打出 1 张攻击牌就再额外给予 1 层。
/// 卡面用「动态变量」实时显示总层数，队友出牌越多数字越大且变绿。
/// 结构与原版多人牌「群起攻之」（GangUp）一致：不挂任何持续状态，打出瞬间按战斗历史一次性结算。
/// 同类写法的原版参考还有「终结技」Finisher、「月华爆」LunarBlast（都用 CalculatedVar 读 History）。
///
/// <para>9/14 第二次平衡调整：升级不再提高每张牌的额外层数（太强），改成**移除「消耗」**——
/// 未升级打出一次就没了，升级后可以反复配合队友。</para>
/// </summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Unison : AraleCardModel
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public Unison() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(1m),
        new CalculationExtraVar(1m),
        new CalculatedVar("Song").WithMultiplier((CardModel card, Creature? _) =>
            CombatManager.Instance.History.CardPlaysFinished.Count((CardPlayFinishedEntry e) =>
                e.HappenedThisTurn(card.CombatState)
                && e.CardPlay.Card.Type == CardType.Attack
                && e.CardPlay.Player != null
                && e.CardPlay.Player != card.Owner))
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal song = ((CalculatedVar)DynamicVars["Song"]).Calculate(cardPlay.Target);
        if (song > 0m)
        {
            await AraleCmd.ApplySong(choiceContext, cardPlay.Target, song, Owner, this);
        }
    }

    /// <summary>升级：移除「消耗」（数值不变）。</summary>
    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

/// <summary>序号 97「传感器」：每当你进入魔法露露状态时，其他玩家获得能量并抽牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Sensor : AraleCardModel
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public Sensor() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new EnergyVar(2),
        new CardsVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<AraleSensorPower>(choiceContext, Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
        if (power != null)
        {
            power.DynamicVars.Cards.BaseValue += DynamicVars.Cards.BaseValue;
            power.DynamicVars.Energy.BaseValue += DynamicVars.Energy.BaseValue;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
