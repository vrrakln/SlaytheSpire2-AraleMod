using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
//  罕见卡（序号 23–63）· 文本与数值以 docs\角色卡池.md 为准
//  v1 简化处均在注释中标注。
// =====================================================================================

/// <summary>序号 23：0 费，每有 1 名敌人获得 1 点梦想，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class CheerOn : AraleCardModel
{
    public CheerOn() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DreamPerEnemy", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int enemies = CombatState?.HittableEnemies?.Count() ?? 0;
        if (enemies > 0)
        {
            await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["DreamPerEnemy"].BaseValue * enemies, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars["DreamPerEnemy"].UpgradeValueBy(1m);
}

/// <summary>序号 24：1 费，给予 2 层歌声，抽 3 张牌，本回合不能再打出攻击牌。</summary>
/// <remarks>9/15 削弱：费用 0 → **1**。</remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class HoarseVoice : AraleCardModel
{
    public HoarseVoice() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Song", 2m),
        new CardsVar(3)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, DynamicVars["Song"].BaseValue, Owner, this);
        await PowerCmd.Apply<AraleNoAttackThisTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>序号 25：0 费，锻炼 3，抽 1 张牌，本回合无法再锻炼。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SetBreak : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public SetBreak() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Train", 4m),
        new CardsVar(1)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
        await PowerCmd.Apply<AraleNoTrainPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Train"].UpgradeValueBy(2m);
}

/// <summary>序号 26：0 费，保留，对所有敌人造成 4 点伤害 2 次，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Stagger : AraleCardModel
{
    public Stagger() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 27：0 费，造成 3 点伤害；每当你锻炼时，这张牌会从弃牌堆放回你的手牌（见 AraleCmd.Train）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Metronome : AraleCardModel
{
    public Metronome() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}

/// <summary>序号 28：0 费，将手牌中的保留牌全部变为肌肉棒棒，每变化 1 张抽 1 张牌，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class RaringToGo : AraleCardModel
{
    public RaringToGo() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState?.Hand?.Cards?.ToList();
        if (hand == null)
        {
            return;
        }
        var targets = hand.Where(c => c != this && c.Keywords.Contains(CardKeyword.Retain)).ToList();
        int changed = 0;
        foreach (var card in targets)
        {
            await CardCmd.Exhaust(choiceContext, card);
            changed++;
        }
        if (changed > 0)
        {
            var bars = await AraleCmd.CreateMuscleBars(choiceContext, Owner, CombatState, changed);
            if (IsUpgraded)
            {
                foreach (var bar in bars)
                {
                    CardCmd.Upgrade(bar);
                }
            }
            await CardPileCmd.Draw(choiceContext, changed, Owner);
        }
    }
}

/// <summary>序号 29：0 费，获得敌人歌声总层数 2 倍的格挡（升级为 3 倍）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ShapeOfVoice : AraleCardModel
{
    public ShapeOfVoice() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(0m),
        new CalculationExtraVar(2m),
        new SongBlockPreviewVar().WithMultiplier((CardModel card, Creature? _) => AraleCmd.TotalSong(card.CombatState))
    };

    protected override void OnUpgrade() => DynamicVars.CalculationExtra.UpgradeValueBy(1m);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal total = AraleCmd.TotalSong(CombatState) * (IsUpgraded ? 3m : 2m);
        if (total > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, total, ValueProp.Move, cardPlay);
        }
    }
}

/// <summary>序号 30：0 费，抽 2 张牌；处于魔法露露状态时再抽 1 张（升级后为再抽 2 张）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SharedVision : AraleCardModel
{
    public SharedVision() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    /// <summary>金边：处于魔法露露状态（打出后会额外抽牌）时亮起。</summary>
    protected override bool ShouldGlowGoldInternal => Owner != null && AraleCmd.IsMagicLulu(Owner.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(2),
        new DynamicVar("BonusCards", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        if (AraleCmd.IsMagicLulu(Owner.Creature))
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars["BonusCards"].BaseValue, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars["BonusCards"].UpgradeValueBy(1m);
}

/// <summary>序号 31：1 费，造成 3 点伤害，给予造成伤害等量的歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SoundWave : AraleCardModel
{
    public SoundWave() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        decimal dealt = attack.Results.SelectMany(r => r).Sum(r => r.TotalDamage);
        if (dealt > 0m)
        {
            await AraleCmd.ApplySong(choiceContext, cardPlay.Target, dealt, Owner, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 32：1 费，获得 6 点格挡，下 1 张攻击牌不会消耗梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Vigilance : AraleCardModel
{
    public Vigilance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("FreeAttacks", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<AraleFreeAttackDreamPower>(choiceContext, Owner.Creature, DynamicVars["FreeAttacks"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["FreeAttacks"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 33：0 费，随机消耗一张牌，获得等同于其费用的梦想（升级后由玩家选择）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class WishSiphon : AraleCardModel
{
    public WishSiphon() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? picked = null;
        if (IsUpgraded)
        {
            picked = (await CardSelectCmd.FromHand(
                prefs: new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
                context: choiceContext,
                player: Owner,
                filter: null,
                source: this)).FirstOrDefault();
        }
        else
        {
            var hand = Owner.PlayerCombatState?.Hand?.Cards?.ToList();
            if (hand != null && hand.Count > 0)
            {
                picked = Owner.RunState.Rng.CombatCardSelection.NextItem(hand);
            }
        }
        if (picked == null)
        {
            return;
        }
        // X 费牌没有固定费用，GetResolved() 会返回它的 CapturedXValue（未打出过 = 0）→ 拿不到梦想。
        // 按设计：消耗 X 费牌应当获得 X 点梦想，X = 打出「心愿汲取」后的实时能量数。
        bool isXCost = picked.EnergyCost.CostsX;
        int cost = isXCost ? 0 : Math.Max(0, picked.EnergyCost.GetResolved());
        await CardCmd.Exhaust(choiceContext, picked);
        if (isXCost)
        {
            cost = Owner.PlayerCombatState?.Energy ?? 0;
        }
        if (cost > 0)
        {
            await AraleCmd.GainDream(choiceContext, Owner, cost, this);
        }
    }
}

/// <summary>序号 34：1 费，获得等同于目标敌人歌声层数的能量（升级后费用降为 0）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Resonance : AraleCardModel
{
    public Resonance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("CurrentEnergy").WithMultiplier((CardModel _, Creature? target) => target == null ? 0m : AraleCmd.Song(target))
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal song = AraleCmd.Song(cardPlay.Target);
        if (song > 0m)
        {
            await PlayerCmd.GainEnergy(song, Owner);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 35：1 费，消耗一张牌，锻炼 4，保留。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class FatBurn : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public FatBurn() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Train", 3m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var picked = (await CardSelectCmd.FromHand(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            context: choiceContext,
            player: Owner,
            filter: null,
            source: this)).FirstOrDefault();
        if (picked != null)
        {
            await CardCmd.Exhaust(choiceContext, picked);
        }
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
    }

    protected override void OnUpgrade() => DynamicVars["Train"].UpgradeValueBy(2m);
}

/// <summary>序号 36：1 费能力，每失去 1 点梦想获得临时敏捷。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Impressionist : AraleCardModel
{
    public Impressionist() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DexPerDream", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamLoseDexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexPerDream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["DexPerDream"].UpgradeValueBy(1m);
}

/// <summary>序号 37：1 费能力，回合开始时给予随机敌人歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Echo : AraleCardModel
{
    public Echo() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Song", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleSongPerTurnPower>(choiceContext, Owner.Creature, DynamicVars["Song"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Song"].UpgradeValueBy(1m);
}

/// <summary>序号 38：1 费能力，每当你给予歌声时抽牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class KindredSpirit : AraleCardModel
{
    public KindredSpirit() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleSongDrawPower>(choiceContext, Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 39：1 费能力，肌肉棒棒额外锻炼 1（升级后费用降为 0）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class EfficientTraining : AraleCardModel
{
    public EfficientTraining() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleMuscleBarTrainingPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 40：1 费能力，每消耗 1 层梦想对所有敌人造成伤害。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ResidualDream : AraleCardModel
{
    public ResidualDream() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DamagePerDream", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamConsumeDamagePower>(choiceContext, Owner.Creature, DynamicVars["DamagePerDream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["DamagePerDream"].UpgradeValueBy(1m);
}

/// <summary>序号 41：1 费，造成 7 点伤害，每有 1 张保留牌额外造成伤害。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class HandheldStrike : AraleCardModel
{
    public HandheldStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    /// <summary>
    /// 名字里带「打击」的牌按原版惯例一律带 <c>CardTag.Strike</c>（原版 剑柄打击 PommelStrike=Common、
    /// 明耀打击 ShiningStrike=Uncommon、完美打击 PerfectedStrike、TwinStrike、UltimateStrike… 全部如此），
    /// 所以这里与它们保持**同一个标签**。
    ///
    /// 与「初始牌打击」的区别**不在标签、而在稀有度**：原版 <c>CardModel.IsBasicStrikeOrDefend</c>
    /// 的判定是「<c>Rarity == CardRarity.Basic</c> 且带打击/防御标签」，因此：
    /// <list type="bullet">
    ///   <item>打击 / 防御（Basic）→ 属于「初始牌」，会被树叶药膏、潘多拉魔盒、虚无种子等只作用于初始牌的效果处理；</item>
    ///   <item>掌机打击（罕见 Uncommon）→ 与明耀打击同档，<b>不会</b>被上述效果处理，但会被只看标签的效果认作打击牌（打击假人 +3 伤害、士兵炖菜 +1 重放、螺旋附魔可附魔等）。</item>
    /// </list>
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

    // 动态伤害（参考战士「灰烬打击」）：基础值 + 每张保留牌 x 额外值，卡面实时显示
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(6m),
        new ExtraDamageVar(3m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) =>
            card.Owner.PlayerCombatState?.Hand?.Cards?.Count(c => c.Keywords.Contains(CardKeyword.Retain)) ?? 0)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3m);
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }
}

/// <summary>序号 42：1 费，造成 7 点伤害，本回合每打出过 1 张技能牌就施加歌声。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class FastChanting : AraleCardModel
{
    public FastChanting() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move),
        new DynamicVar("SongPerSkill", 1m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        // 挂在目标身上：之后你每打出 1 张技能牌，就对它施加歌声（机制同静默猎手「紧勒」）
        await PowerCmd.Apply<AraleSongOnSkillPower>(choiceContext, cardPlay.Target, DynamicVars["SongPerSkill"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["SongPerSkill"].UpgradeValueBy(1m);
}

/// <summary>序号 43：1 费，造成 9 点伤害，下 2 次锻炼层数翻倍。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class WarmUp : AraleCardModel
{
    public WarmUp() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<AraleTrainingDoublePower>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>序号 44：1 费，使目标敌人失去力量，获得梦想，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Deadline : AraleCardModel
{
    public Deadline() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<StrengthPower>(2m),
        new DynamicVar("Dream", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await PowerCmd.Apply<DarkShacklesPower>(choiceContext, cardPlay.Target, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthPower"].UpgradeValueBy(1m);
        DynamicVars["Dream"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 45：1 费能力，保留，获得等同于当前梦想层数的荆棘（永久），升级后费用降为 0。</summary>
/// <remarks>
/// 9/14 第二次平衡调整：旧版是「本回合获得梦想层数 2 倍的临时荆棘、消耗」，太弱且和梦想引擎冲突；
/// 现在改成**能力牌 + 永久荆棘**，梦想越多荆棘越多，升级只降费不涨数值。
/// </remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class GuardedMind : AraleCardModel
{
    public GuardedMind() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal thorns = AraleCmd.Dream(Owner.Creature);
        if (thorns > 0m)
        {
            // 永久荆棘：直接给原版「荆棘」，本场战斗一直有效（与临时荆棘 AraleTemporaryThornsPower 无关）
            await PowerCmd.Apply<ThornsPower>(choiceContext, Owner.Creature, thorns, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 46：1 费，保留，获得 8 点格挡，将 1 张随机本职业保留牌加入手牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Satisfaction : AraleCardModel
{
    public Satisfaction() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(8m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (CombatState == null)
        {
            return;
        }
        // Derive the pool from current keywords, rather than an obsolete list of names.
        // Canonical keywords exclude cards that only gain Retain after upgrading.
        var candidates = Pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanonicalKeywords.Contains(CardKeyword.Retain));
        var card = MegaCrit.Sts2.Core.Factories.CardFactory.GetDistinctForCombat(
            Owner, candidates, 1, Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (card is null) return;
        if (IsUpgraded)
        {
            CardCmd.Upgrade(card);
        }
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

/// <summary>序号 47：1 费能力，每消耗若干点梦想获得 1 点能量。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DeterminedGaze : AraleCardModel
{
    public DeterminedGaze() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    // 卡面用 {Energy:energyIcons()} 显示能量图标（与原版「肾上腺素」等一致），因此需要 EnergyVar
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("DreamPerEnergy", 3m),
        new EnergyVar(1)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamConsumeEnergyPower>(choiceContext, Owner.Creature, DynamicVars["DreamPerEnergy"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["DreamPerEnergy"].UpgradeValueBy(-1m);
}

/// <summary>序号 48：1 费能力，每打出 3 张攻击牌锻炼。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class MuscleCoordination : AraleCardModel
{
    public MuscleCoordination() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Train", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleAttackTrainingPower>(choiceContext, Owner.Creature, DynamicVars["Train"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Train"].UpgradeValueBy(1m);
}

/// <summary>序号 49：1 费能力，每回合你打出的第 3 张攻击牌不消耗梦想（升级后获得固有）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Flattered : AraleCardModel
{
    public Flattered() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleThirdAttackFreePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 50：2 费，对所有敌人造成 14 点伤害，击杀（含爪牙）时获得 2 点梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Shatter : AraleCardModel
{
    public Shatter() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    /// <summary>金边：有敌人可能被这次伤害击杀（击杀才会给梦想）时亮起。</summary>
    protected override bool ShouldGlowGoldInternal => CanKillAnEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("Dream", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
        // 注意：这里是「击杀」，与原版静默猎手「回响斩击」EchoingSlash 的判定一致——
        // 只看 WasTargetKilled，**不做** ShouldOwnerDeathTriggerFatal 前置判定，因此击杀爪牙也算。
        // （需要「斩杀」语义的牌见梦之刃 DreamBlade，那里才要求非爪牙敌人。）
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>序号 51：2 费，保留，获得 13 点格挡，下回合获得 2 点梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ChangeSubject : AraleCardModel
{
    public ChangeSubject() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(13m, ValueProp.Move),
        new DynamicVar("Dream", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<AraleNextTurnDreamPower>(choiceContext, Owner.Creature, DynamicVars["Dream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>序号 52：2 费，给予所有敌人 3 层歌声、2 层虚弱，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Intimidate : AraleCardModel
{
    public Intimidate() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Song", 3m),
        new PowerVar<WeakPower>(2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.ApplySongToAll(choiceContext, Owner, DynamicVars["Song"].BaseValue, this);
        await PowerCmd.Apply<WeakPower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Song"].UpgradeValueBy(2m);
}

/// <summary>序号 53：2 费能力，每回合开始时将 2 张肌肉棒棒加入手牌（升级后费用降为 1）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class PracticeSchedule : AraleCardModel
{
    public PracticeSchedule() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleMuscleBarPerTurnPower>(choiceContext, Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 54：2 费，造成 14 点伤害，你的下一张保留牌耗能变为 0。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SuperHeadbutt : AraleCardModel
{
    public SuperHeadbutt() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(14m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        await PowerCmd.Apply<AraleFreeRetainPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>序号 55：2 费，造成 7 点伤害 2 次，抽 2 张牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Beckon : AraleCardModel
{
    public Beckon() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move),
        new CardsVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>序号 56：2 费，造成 15 点伤害，锻炼 2，下回合再锻炼 2。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class MerchFrenzy : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public MerchFrenzy() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(15m, ValueProp.Move),
        new DynamicVar("Train", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
        await PowerCmd.Apply<AraleNextTurnTrainingPower>(choiceContext, Owner.Creature, DynamicVars["Train"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
        DynamicVars["Train"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 57：2 费，对所有敌人造成 6 点伤害 2 次并给予 2 层易伤。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class FallIn : AraleCardModel
{
    public FallIn() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new PowerVar<VulnerablePower>(2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}

/// <summary>序号 58：2 费，失去 10 点生命，锻炼 5，获得 2 层无实体，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class StillRememberYou : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public StillRememberYou() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HpLossVar(10m),
        new DynamicVar("Train", 5m),
        new DynamicVar("Intangible", 2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this, cardPlay);
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
        await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature, DynamicVars["Intangible"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.HpLoss.UpgradeValueBy(-3m);
}

/// <summary>序号 59：2 费，给予所有敌人 2 层歌声、2 层易伤，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Infectiousness : AraleCardModel
{
    public Infectiousness() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Song", 2m),
        new PowerVar<VulnerablePower>(2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.ApplySongToAll(choiceContext, Owner, DynamicVars["Song"].BaseValue, this);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Song"].UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}

/// <summary>序号 60：2 费，随机给予敌人 2 点歌声 3 次（升级后获得保留）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ImpromptuSong : AraleCardModel
{
    public ImpromptuSong() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Song", 2m),
        new DynamicVar("Repeats", 3m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = CombatState?.HittableEnemies?.ToList();
        if (enemies == null || enemies.Count == 0)
        {
            return;
        }
        int repeats = (int)DynamicVars["Repeats"].BaseValue;
        for (int i = 0; i < repeats; i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(enemies);
            await AraleCmd.ApplySong(choiceContext, target!, DynamicVars["Song"].BaseValue, Owner, this);
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

/// <summary>禁止剧透：单体伤害 17/23，所有存活敌人的歌声总层数降低费用；不依赖本机悬停目标。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class NoSpoilers : AraleCardModel
{
    private const int BaseCost = 3;

    public NoSpoilers() : base(BaseCost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(17m, ValueProp.Move)
    };

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card != this || CombatState == null) return false;
        // Cost depends only on synchronized combat state, never the local hover target.
        decimal song = CombatState.Enemies.Where(e => e.IsAlive).Sum(AraleCmd.Song);
        modifiedCost = Math.Min(originalCost, Math.Max(0m, BaseCost - song));
        return modifiedCost != originalCost;
    }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6m);

}

/// <summary>序号 62：X 费，对所有敌人造成 3 点伤害（X + 当前梦想点数）次。</summary>
/// <remarks>9/15 削弱：伤害 4 → **3**（升级 4），并**移除升级额外 +1 次攻击**（升级只提升伤害）。</remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class RadiantBurst : AraleCardModel
{
    public RadiantBurst() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int hits = ResolveEnergyXValue() + (int)AraleCmd.Dream(Owner.Creature);
        if (hits <= 0)
        {
            return;
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}

/// <summary>序号 63：X 费，锻炼 X，并在接下来 X 个回合开始时获得梦想，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class BuildUp : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public BuildUp() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override bool HasEnergyCostX => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DreamPerTurn", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // X 取 ResolveEnergyXValue()（= 出牌时支付的能量，再经 Hook.ModifyXValue 修正），与原版 X 费牌一致。
        // 注意：遗物「化学物X」正是通过 ModifyXValue 给 X 费牌 +2（全游戏唯一的 X 修正器，中文词条：
        // 「耗能为X的牌的效果数值增加2点」），所以持有它时 3 点能量会打出「锻炼 5 / 接下来 5 回合」——
        // 这是化学物X 的正常效果，不是「X 吃掉了魔法露露给的 2 点能量」
        // （能量支付发生在 OnPlay 之前：PlayCardAction 先 SpendResources() 再 OnPlayWrapper）。
        int x = ResolveEnergyXValue();
        if (x <= 0)
        {
            return;
        }
        await AraleCmd.Train(choiceContext, Owner, x, this);
        var power = await PowerCmd.Apply<AraleDreamOverTimePower>(choiceContext, Owner.Creature, x, Owner.Creature, this);
        if (power != null)
        {
            power.DynamicVars["DreamPerTurn"].BaseValue = DynamicVars["DreamPerTurn"].BaseValue;
        }
    }

    protected override void OnUpgrade() => DynamicVars["DreamPerTurn"].UpgradeValueBy(1m);
}
