using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Arale.Scripts.Mechanics;

// =====================================================================================
//  阿拉蕾核心机制（梦想 / 歌声 / 陶醉 / 锻炼 / 魔法露露）以及配套的联动能力
//  设计文档：docs\角色卡池.md
//  说明：机制数值与规则以卡池文档为准；本文件同时提供 AraleCmd 供卡牌代码调用。
// =====================================================================================

/// <summary>梦想：攻击牌按当前梦想点数获得额外伤害；每打出一张非无色攻击牌消耗 1 点。</summary>
[RegisterPower]
public sealed class AraleDreamPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public static decimal DreamMultiplier(Creature creature) => 1m + (creature.GetPower<AraleDreamForcePower>()?.Amount ?? 0);

    private decimal Multiplier => DreamMultiplier(Owner);

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (base.Owner != dealer)
        {
            return 0m;
        }
        if (!props.IsPoweredAttack())
        {
            return 0m;
        }
        return base.Amount * Multiplier;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 多人模式：只消耗「梦想持有者自己」打出的攻击牌，队友的攻击不受影响
        if (cardPlay.Player == null || cardPlay.Player.Creature != base.Owner)
        {
            return;
        }
        await AraleCmd.ConsumeDreamForAttack(choiceContext, cardPlay);
    }
}

/// <summary>梦想即力量：每层令梦想额外加成攻击牌一次。</summary>
[RegisterPower]
public sealed class AraleDreamForcePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>歌声：层数只保留「5 的余数」（0~4）；转化与扣血在 <see cref="AraleCmd.ApplySong" /> 里统一处理。</summary>
[RegisterPower]
public sealed class AraleSongPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>陶醉：造成的攻击伤害减半，持续层数回合（机制同虚弱）。</summary>
[RegisterPower]
public sealed class AraleIntoxicatedPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DamageDecrease", 0.5m) };

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != base.Owner)
        {
            return 1m;
        }
        if (!props.IsPoweredAttack())
        {
            return 1m;
        }
        return base.DynamicVars["DamageDecrease"].BaseValue;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(this);
        }
    }
}

/// <summary>锻炼：累计到 10 时清空并进入魔法露露状态（溢出不保留，除非拥有对应能力）。</summary>
[RegisterPower]
public sealed class AraleTrainingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>魔法露露：进入时 +2 能量 +2 梦想，攻击牌不再消耗梦想，回合结束时退出。</summary>
[RegisterPower]
public sealed class AraleMagicLuluPower : PowerModel
{
    // Presented by the character's transformation overlay, without a stack counter.
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        var ctx = new ThrowingPlayerChoiceContext();
        Player? player = base.Owner.Player;
        if (player != null)
        {
            await PlayerCmd.GainEnergy(2m, player);
            await AraleCmd.GainDream(ctx, player, 2m, cardSource);
            var bonus = base.Owner.GetPower<AraleBonusDreamPower>();
            if (bonus != null)
            {
                // 能力层数**就是**额外获得的梦想数（卡面 {BonusDream} 共用同一个数）
                await AraleCmd.GainDream(ctx, player, bonus.Amount, cardSource);
            }
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}

/// <summary>魔法露露强化（稀有 #84）：进入魔法露露时额外获得梦想。</summary>
[RegisterPower]
public sealed class AraleBonusDreamPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>锻炼溢出保留（稀有 #85）：进入魔法露露时不清空溢出的锻炼层数。</summary>
[RegisterPower]
public sealed class AraleOverflowTrainingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>共鸣（罕见 #38）：每当你给予歌声时抽牌。</summary>
[RegisterPower]
public sealed class AraleSongDrawPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task HandleSongApplied(PlayerChoiceContext choiceContext, Player owner, CardModel? source)
    {
        await CardPileCmd.Draw(choiceContext, base.Amount, owner);
    }
}

/// <summary>喧嚣（稀有 #71）：拥有歌声或陶醉的敌人受到的伤害提高。</summary>
[RegisterPower]
public sealed class AraleSongBonusDamagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != base.Owner || target == null)
        {
            return 1m;
        }
        if (target.GetPower<AraleSongPower>() == null && target.GetPower<AraleIntoxicatedPower>() == null)
        {
            return 1m;
        }
        return 1m + Amount / 100m;
    }
}

/// <summary>回响冲击（罕见 #40）：每消耗 1 层梦想，对所有敌人造成伤害。</summary>
[RegisterPower]
public sealed class AraleDreamConsumeDamagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task HandleDreamConsumed(PlayerChoiceContext choiceContext, Player owner, decimal consumed, CardModel? source)
    {
        var state = owner.Creature.CombatState;
        var enemies = state?.HittableEnemies;
        if (enemies == null || !enemies.Any())
        {
            return;
        }
        await CreatureCmd.Damage(choiceContext, enemies, consumed * base.Amount, ValueProp.Unpowered, owner.Creature, source, null);
    }
}

/// <summary>坚定眼神：每张牌独立累计梦想消耗，图标显示距下次触发所需的梦想。</summary>
[RegisterPower]
public sealed class AraleDreamConsumeEnergyPower : PowerModel
{
    private sealed class Data
    {
        public decimal Accumulated;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override int DisplayAmount => Math.Max(0, Amount - (int)GetInternalData<Data>().Accumulated);

    protected override object InitInternalData()
    {
        return new Data();
    }

    public async Task HandleDreamConsumed(PlayerChoiceContext choiceContext, Player owner, decimal consumed, CardModel? source)
    {
        if (base.Amount <= 0m)
        {
            return;
        }
        var data = GetInternalData<Data>();
        data.Accumulated += consumed;
        decimal gained = Math.Floor(data.Accumulated / base.Amount);
        if (gained > 0m)
        {
            data.Accumulated -= gained * base.Amount;
            await PlayerCmd.GainEnergy(gained, owner);
        }
        InvokeDisplayAmountChanged();
    }
}

/// <summary>印象派（罕见 #36）：每失去 1 点梦想获得临时敏捷。</summary>
[RegisterPower]
public sealed class AraleDreamLoseDexterityPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task HandleDreamConsumed(PlayerChoiceContext choiceContext, Player owner, decimal consumed, CardModel? source)
    {
        await AraleCmd.GainTemporaryDexterity(choiceContext, owner, consumed * base.Amount, source);
    }
}

/// <summary>协调肌群：回合内每打出 3 张攻击牌，锻炼 Amount；回合开始清空进度。</summary>
[RegisterPower]
public sealed class AraleAttackTrainingPower : PowerModel
{
    private sealed class Data
    {
        public decimal Attacks;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner) GetInternalData<Data>().Attacks = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack || cardPlay.Player.Creature != base.Owner)
        {
            return;
        }
        decimal training = Amount;
        var data = GetInternalData<Data>();
        data.Attacks += 1m;
        if (data.Attacks >= 3m)
        {
            data.Attacks = 0m;
            await AraleCmd.Train(choiceContext, cardPlay.Player, training, cardPlay.Card);
        }
    }
}

/// <summary>节流（罕见 #32）：接下来的 N 张攻击牌不会消耗梦想。</summary>
[RegisterPower]
public sealed class AraleFreeAttackDreamPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>过奖（罕见 #49）：每回合打出的第 2 张攻击牌不消耗梦想。</summary>
[RegisterPower]
public sealed class AraleThirdAttackFreePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;


}

/// <summary>梦之疗愈（稀有 #72）：战斗结束时每有 1 点梦想回复 Amount 点生命，最多 10 点。</summary>
[RegisterPower]
public sealed class AraleDreamHealPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        Player? player = base.Owner.Player;
        if (player == null)
        {
            return;
        }
        decimal dream = AraleCmd.Dream(base.Owner);
        decimal heal = Math.Min(10m, dream * base.Amount);
        if (heal > 0m)
        {
            await CreatureCmd.Heal(base.Owner, heal);
        }
    }
}

/// <summary>日有所思（稀有 #88）：每回合开始时获得梦想。</summary>
[RegisterPower]
public sealed class AraleDreamPerTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        await AraleCmd.GainDream(choiceContext, player, base.Amount, null);
    }
}

/// <summary>回音（罕见 #37）：回合开始时给予随机敌人歌声。</summary>
[RegisterPower]
public sealed class AraleSongPerTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        var enemies = player.Creature.CombatState?.HittableEnemies?.ToList();
        if (enemies == null || enemies.Count == 0)
        {
            return;
        }
        // 使用对局同步的选敌随机流，确保联机各端对同一个敌人施加歌声。
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        await AraleCmd.ApplySong(choiceContext, target, base.Amount, player, null);
    }
}

/// <summary>轻装上阵（稀有 #86）：所有保留牌耗能减 1。</summary>
/// <remarks>与「超级头槌」同样改用 <see cref="CardModel.ShouldRetainThisTurn" />，
/// 让附魔/效果赋予的保留也算「保留牌」（9/14 与 54 一并修正）。</remarks>
[RegisterPower]
public sealed class AraleRetainCostPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card == null || card.Owner?.Creature != base.Owner)
        {
            return false;
        }
        if (!card.ShouldRetainThisTurn)
        {
            return false;
        }
        modifiedCost = Math.Max(0m, originalCost - base.Amount);
        return true;
    }
}

/// <summary>拉伸（罕见 #56）：下个回合开始时再进行锻炼。</summary>
[RegisterPower]
public sealed class AraleNextTurnTrainingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        await AraleCmd.Train(choiceContext, player, base.Amount, null);
        await PowerCmd.Remove(this);
    }
}

/// <summary>转移话题：下回合获得 Amount 点梦想，结算后移除。</summary>
[RegisterPower]
public sealed class AraleNextTurnDreamPower : PowerModel, STS2RitsuLib.Scaffolding.Content.Patches.IModPowerAssetOverrides
{
    public string CustomIconPath => "res://arale_mod/images/powers/small/AraleNextTurnDreamPower.png";
    public string CustomBigIconPath => "res://images/powers/arale_mod_power_arale_next_turn_dream_power.png";
    public STS2RitsuLib.Scaffolding.Content.PowerAssetProfile AssetProfile => new(
        IconPath: CustomIconPath, BigIconPath: CustomBigIconPath);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;
        await AraleCmd.GainDream(choiceContext, player, Amount, null);
        await PowerCmd.Remove(this);
    }
}

/// <summary>厚积薄发：每次施放独立记录剩余回合及每回合梦想量。</summary>
[RegisterPower]
public sealed class AraleDreamOverTimePower : PowerModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DreamPerTurn", 1m) };

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        await AraleCmd.GainDream(choiceContext, player, base.DynamicVars["DreamPerTurn"].BaseValue, null);
        await PowerCmd.TickDownDuration(this);
    }
}

/// <summary>跃动（罕见 #43）：接下来 N 次锻炼的层数翻倍。</summary>
[RegisterPower]
public sealed class AraleTrainingDoublePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>保守起见：每回合开始选择 Amount 张手牌添加保留。</summary>
[RegisterPower]
public sealed class AraleRetainPerTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>选择提示（本地化键：cards 表的 ARALE_MOD_CARD_PLAY_IT_SAFE.selectionScreenPrompt）。</summary>
    private static LocString Prompt => new LocString("cards", "ARALE_MOD_CARD_PLAY_IT_SAFE.selectionScreenPrompt");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        var hand = player.PlayerCombatState?.Hand?.Cards;
        if (hand == null || hand.Count == 0)
        {
            return;
        }
        int count = Math.Min(Amount, hand.Count(c => !c.Keywords.Contains(CardKeyword.Retain)));
        if (count <= 0) return;
        var picked = await CardSelectCmd.FromHand(
            prefs: new CardSelectorPrefs(Prompt, count),
            context: choiceContext,
            player: player,
            filter: (CardModel c) => !c.Keywords.Contains(CardKeyword.Retain),
            source: this);
        foreach (var card in picked)
        {
            AraleCmd.AddRetain(card);
        }
    }
}


// =====================================================================================
//  用于卡牌联动的辅助能力（多数为不可见的计数器）
// =====================================================================================

/// <summary>序号 39 · 肌肉棒棒额外锻炼 N。</summary>
[RegisterPower]
public sealed class AraleMuscleBarTrainingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>内部标记：本回合是否获得过梦想（序号 20「自我怀疑」使用）。
/// 注意在**玩家回合结束时**清除而不是回合开始：这样战斗开始时由初始遗物获得的梦想、
/// 以及上一回合挂上的「下回合获得梦想」效果，都能在本次回合内被正确判定。</summary>
[RegisterPower]
public sealed class AraleDreamGainedThisTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}

/// <summary>内部标记：本回合无法再锻炼（序号 25 使用）。</summary>
[RegisterPower]
public sealed class AraleNoTrainPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
            return;
        }
        await Task.CompletedTask;
    }
}

/// <summary>序号 54 · 接下来的 N 张保留牌耗能变为 0。</summary>
/// <remarks>
/// 写法照抄原版同类卡（静默猎手「猛扑」Pounce → FreeSkillPower、铁甲战士「无情猛攻」Unrelenting → FreeAttackPower、
/// 故障机器人「人工合成」Synthesis → FreePowerPower）：用 Late 费用钩子把费用改成 0，
/// 并在 <see cref="BeforeCardPlayed" /> 里扣掉一层；只对手牌/出牌堆里的牌生效。
///
/// <para><b>9/14 修 bug</b>：原来判定「保留牌」用的是 <c>CanonicalKeywords</c>，
/// 它只包含卡面印的保留，**看不到附魔或卡牌效果临时赋予的保留**
/// （阿拉蕾的书包、舒缓、玩安全…），于是那些牌不享受免费。
/// 现在改用 <see cref="CardModel.ShouldRetainThisTurn" />（= 当前的保留关键词，含附加/附魔来的），
/// 判定口径与「藤都子的团魂」「自闭」的保留结算保持一致。</para>
/// </remarks>
[RegisterPower]
public sealed class AraleFreeRetainPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (base.Amount <= 0 || card == null || card.Owner?.Creature != base.Owner)
        {
            return false;
        }
        if (!IsFreeTargetCard(card))
        {
            return false;
        }
        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay?.Card == null || cardPlay.Player?.Creature != base.Owner)
        {
            return;
        }
        if (!IsFreeTargetCard(cardPlay.Card))
        {
            return;
        }
        await PowerCmd.Decrement(this);
    }

    /// <summary>「保留牌」且位于手牌/出牌堆（原版免费牌的做法：不理会抽牌堆与弃牌堆里的牌）。</summary>
    private static bool IsFreeTargetCard(CardModel card)
    {
        if (!card.ShouldRetainThisTurn)
        {
            return false;
        }
        return card.Pile?.Type is PileType.Hand or PileType.Play;
    }
}

/// <summary>内部计数器：本场战斗累计施加过的歌声层数（序号 82「余音缭绕」使用）。</summary>
[RegisterPower]
public sealed class AraleSongGivenTotalPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}
/// <summary>序号 53 · 每回合开始时将肌肉棒棒加入手牌。</summary>
[RegisterPower]
public sealed class AraleMuscleBarPerTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player.Creature != base.Owner)
        {
            return;
        }
        // 手牌放不下的肌肉棒棒**送入弃牌堆**（不能直接丢掉）：
        // overflowToDiscard: true 时由 CreateMuscleBars 自己按手牌上限分配，超出的进弃牌堆。
        int count = (int)base.Amount;
        if (count > 0)
        {
            await AraleCmd.CreateMuscleBars(choiceContext, player, player.Creature.CombatState, count, overflowToDiscard: true);
        }
    }
}

/// <summary>先古（序号 92）· 每打出 1 张牌获得梦想。</summary>
[RegisterPower]
public sealed class AraleDreamPerCardPlayedPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != base.Owner)
        {
            return;
        }
        await AraleCmd.GainDream(choiceContext, cardPlay.Player, base.Amount, cardPlay.Card);
    }
}
/// <summary>序号 24「声嘶力竭」：本回合不能再打出攻击牌。</summary>
[RegisterPower]
public sealed class AraleNoAttackThisTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card.Owner?.Creature != base.Owner)
        {
            return true;
        }
        return card.Type != CardType.Attack;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
// =====================================================================================
//  临时属性回收（隐藏，不在 UI 上显示）
//
//  设计：临时敏捷 / 临时荆棘**不再做成自己的能力状态**——玩家看到的始终是原版的
//  「敏捷」「荆棘」（原版图标与数值），只是回合结束时把本回合临时给出去的那部分扣回来。
//  这里用两个 **隐藏** 能力做记账（原版同样有这种不可见能力，例如 AmbergrisPower 用
//  IsVisibleInternal => false），Amount = 本回合待扣回的层数。
//  原版同机制参考：静默猎手卡牌「预判」（AnticipatePower : TemporaryDexterityPower）、
//  静默猎手遗物「螺线飞镖」（HelicalDartPower : TemporaryDexterityPower）——
//  它们也是「先给真实敏捷、回合结束时再减掉」，区别只是原版会额外显示一个包装能力图标。
// =====================================================================================

/// <summary>临时敏捷的记账（隐藏）：回合结束时把自己发放的敏捷扣回去。</summary>
[RegisterPower]
public sealed class AraleTemporaryDexterityPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner))
        {
            return;
        }
        decimal amount = base.Amount;
        await PowerCmd.Remove(this);
        if (amount > 0m)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, -amount, base.Owner, null, silent: true);
        }
    }
}

/// <summary>临时荆棘的记账（隐藏）：回合结束时把自己发放的荆棘扣回去。</summary>
[RegisterPower]
public sealed class AraleTemporaryThornsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner))
        {
            return;
        }
        decimal amount = base.Amount;
        await PowerCmd.Remove(this);
        if (amount > 0m)
        {
            await PowerCmd.Apply<ThornsPower>(choiceContext, base.Owner, -amount, base.Owner, null, silent: true);
        }
    }
}

/// <summary>序号 42「高速吟唱」：你每打出 1 张技能牌，就对这名敌人施加歌声（机制参考静默猎手「紧勒」）。</summary>
[RegisterPower]
public sealed class AraleSongOnSkillPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }
        if (base.Applier?.Player == null || cardPlay.Player != base.Applier.Player)
        {
            return;
        }
        Flash();
        await AraleCmd.ApplySong(choiceContext, base.Owner, base.Amount, cardPlay.Player, cardPlay.Card);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}

/// <summary>序号 75「沉醉」升级版：下个回合再晕眩该敌人一次（合计 2 个回合）。</summary>
[RegisterPower]
public sealed class AraleStunNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner))
        {
            return;
        }
        await PowerCmd.Remove(this);
        if (base.Owner.Monster != null)
        {
            await CreatureCmd.Stun(base.Owner);
        }
    }
}

// =====================================================================================
//  多人游戏牌（序号 93–97）配套能力
// =====================================================================================

/// <summary>序号 94「梦蝶」：战斗结束时，为随机 1 名玩家和你自己回复生命（层数 = 回复量）。</summary>
[RegisterPower]
public sealed class AraleDreamButterflyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 说明：这里必须用 AfterCombatEnd 而不是 AfterCombatVictory——原版战斗结束的顺序是
    //   Hook.AfterCombatEnd → room.OnCombatEnded() → Player.AfterCombatEnd()（会清空全部能力）→ Hook.AfterCombatVictory
    // 所以「能力」在 AfterCombatVictory 时已经被移除，只有 AfterCombatEnd 还能生效
    // （该钩子只在胜利路径触发：战败走 CombatManager.ProcessPendingLoss，不会派发它）。
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        decimal heal = base.Amount;
        if (heal <= 0m || base.Owner.IsDead)
        {
            return;
        }
        var me = base.Owner.Player;
        if (me == null)
        {
            return;
        }
        Flash();

        // 随机 1 名其他玩家（RunState.Players 比 CombatState 更稳，战斗收尾阶段依然有效）
        var others = me.RunState?.Players?
            .Where(p => p != null && p != me && p.Creature != null && !p.Creature.IsDead).ToList();
        if (others != null && others.Count > 0)
        {
            var picked = me.RunState!.Rng.CombatCardSelection.NextItem(others);
            if (picked?.Creature != null)
            {
                await CreatureCmd.Heal(picked.Creature, heal);
            }
        }
        await CreatureCmd.Heal(base.Owner, heal);
    }
}

/// <summary>序号 97「传感器」：每当你进入魔法露露状态时，其他玩家获得能量并抽牌。</summary>
[RegisterPower]
public sealed class AraleSensorPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(0), new EnergyVar(0) };

    /// <summary>由 AraleCmd.EnterMagicLulu 调用：Amount = 累计奖励数，Energy / Cards = 每人获得的总能量 / 抽牌数。</summary>
    public async Task OnMagicLuluEntered(PlayerChoiceContext choiceContext, CardModel? source = null)
    {
        int cards = base.DynamicVars.Cards.IntValue;
        // GetTeammatesOf 包含自己，这里要的是「其他玩家」，所以排除自己
        var teammates = CombatState?.GetTeammatesOf(base.Owner)?
            .Where(c => c != null && c != base.Owner && c.IsAlive && c.IsPlayer).ToList();
        if (teammates == null || teammates.Count == 0)
        {
            return;
        }
        Flash();
        foreach (var teammate in teammates)
        {
            var player = teammate!.Player;
            if (player == null)
            {
                continue;
            }
            if (DynamicVars.Energy.BaseValue > 0)
            {
                await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
            }
            if (cards > 0)
            {
                // 传 null 源牌是安全的（BranchingPlayerChoiceContext 只把 Source 用于日志描述）
                await CardPileCmd.DrawWithoutBlockingOnOtherPlayers(choiceContext, cards, player, source!);
            }
        }
    }
}

