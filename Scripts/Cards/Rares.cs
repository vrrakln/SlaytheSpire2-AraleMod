using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

// =====================================================================================
//  稀有卡（序号 64–89）· 文本与数值以 docs\角色卡池.md 为准
// =====================================================================================

/// <summary>序号 64：0 费，每有 1 点梦想抽 1 张牌，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class SelfAffirmation : AraleCardModel
{
    public SelfAffirmation() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("CardsPerDream", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal draw = AraleCmd.Dream(Owner.Creature) * DynamicVars["CardsPerDream"].BaseValue;
        if (draw > 0m)
        {
            await CardPileCmd.Draw(choiceContext, draw, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars["CardsPerDream"].UpgradeValueBy(1m);
}

/// <summary>序号 65：0 费，获得 2 点梦想，给予 2 层易伤，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class FantasyGuess : AraleCardModel
{
    public FantasyGuess() : base(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Dream", 2m),
        new PowerVar<VulnerablePower>(2m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
        if (cardPlay.Target != null)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dream"].UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}

/// <summary>序号 66：0 费，失去 1 点梦想，获得 2 点能量，抽 4 张牌，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ZoneOut : AraleCardModel
{
    public ZoneOut() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("DreamCost", 1m),
        new EnergyVar(2),
        new CardsVar(4)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.LoseDream(choiceContext, Owner, DynamicVars["DreamCost"].BaseValue, this);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1m);
}

/// <summary>序号 67：0 费，只有锻炼层数 ≥ 5 时才能打出，造成 18 点伤害。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Effortless : AraleCardModel
{
    public Effortless() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override bool IsPlayable => Owner != null && AraleCmd.Training(Owner.Creature) >= 5m;

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(18m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(7m);
}

/// <summary>序号 68 · 合影：0 费，获得 1 点梦想，给予所有敌人 2 层歌声，锻炼 4，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class GroupPhoto : AraleCardModel
{
    /// <summary>会锻炼（打出「组间休息」后，本牌会被标红提醒）。</summary>
    protected override bool TrainsThisTurn => true;
    public GroupPhoto() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Dream", 1m),
        new DynamicVar("Song", 2m),
        new DynamicVar("Train", 4m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
        await AraleCmd.ApplySongToAll(choiceContext, Owner, DynamicVars["Song"].BaseValue, this);
        await AraleCmd.Train(choiceContext, Owner, DynamicVars["Train"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dream"].UpgradeValueBy(1m);
        DynamicVars["Song"].UpgradeValueBy(1m);
        DynamicVars["Train"].UpgradeValueBy(1m);
    }
}

/// <summary>序号 69：1 费，将敌人的歌声层数翻倍（升级后变为 3 倍），消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Amplify : AraleCardModel
{
    public Amplify() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal song = AraleCmd.Song(cardPlay.Target);
        if (song <= 0m)
        {
            return;
        }
        decimal add = IsUpgraded ? song * 2m : song;
        await AraleCmd.ApplySong(choiceContext, cardPlay.Target, add, Owner, this);
    }
}

/// <summary>序号 70：1 费，若拥有 2 点或以上梦想，失去所有梦想并用肌肉棒棒填满手牌（升级后获得保留）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Handstand : AraleCardModel
{
    public Handstand() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (AraleCmd.Dream(Owner.Creature) < 3m)
        {
            return;
        }
        await AraleCmd.LoseAllDream(choiceContext, Owner, this);
        int count = AraleCmd.FreeHandSlots(Owner);
        if (count > 0)
        {
            await AraleCmd.CreateMuscleBars(choiceContext, Owner, CombatState, count);
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

/// <summary>序号 71：1 费能力，拥有歌声或陶醉的敌人受到更多伤害。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class InMyWords : AraleCardModel
{
    public InMyWords() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DamageIncrease", 1.5m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleSongBonusDamagePower>(choiceContext, Owner.Creature, (DynamicVars["DamageIncrease"].BaseValue - 1m) * 100m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["DamageIncrease"].UpgradeValueBy(0.25m);
}

/// <summary>序号 72：1 费能力，战斗结束时每有 1 点梦想回复 3 点生命（最多 10 点），升级后费用降为 0。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DreamHealing : AraleCardModel
{
    public DreamHealing() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    /// <summary>每点梦想回复的生命（同时传给能力，能力里的结算与卡面显示共用这一个数）。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("HealPerDream", 3m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamHealPower>(choiceContext, Owner.Creature, DynamicVars["HealPerDream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 73「梦之刃」：保留，造成伤害；斩杀时本局游戏永久增加伤害（实现参考亡灵契约师「巨镰」，
/// 用 [SavedProperty] 保存成长值，并同步到牌组中的那张卡，保证跨战斗生效），消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DreamBlade : AraleCardModel
{
    private const int BaseDamage = 10;

    /// <summary>金边：有**非爪牙**敌人可能被这次伤害斩杀（才会永久加伤）时亮起。</summary>
    /// <remarks>爪牙被击杀不会触发斩杀加伤，所以金边不亮（见 <see cref="AraleCardModel.CanFatalAnEnemy" />）。</remarks>
    protected override bool ShouldGlowGoldInternal => CanFatalAnEnemy;

    private int _currentDamage = BaseDamage;

    private int _increasedDamage;

    [SavedProperty]
    public int CurrentDamage
    {
        get => _currentDamage;
        set
        {
            AssertMutable();
            _currentDamage = value;
            DynamicVars.Damage.BaseValue = _currentDamage;
        }
    }

    [SavedProperty]
    public int IncreasedDamage
    {
        get => _increasedDamage;
        set
        {
            AssertMutable();
            _increasedDamage = value;
        }
    }

    public DreamBlade() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(CurrentDamage, ValueProp.Move),
        new IntVar("Growth", 4m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // 斩杀判定与原版「狂宴」一致：目标身上的能力允许把这次死亡视作斩杀时才算
        // （爪牙等 ShouldOwnerDeathTriggerFatal() 为 false 的敌人不算，与词条说明「非爪牙敌人」相符）
        bool fatalAllowed = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (fatalAllowed && attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            int growth = DynamicVars["Growth"].IntValue;
            BuffFromKill(growth);
            (DeckVersion as DreamBlade)?.BuffFromKill(growth);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Growth"].UpgradeValueBy(1m);

    protected override void AfterDowngraded() => UpdateDamage();

    private void BuffFromKill(int extraDamage)
    {
        IncreasedDamage += extraDamage;
        UpdateDamage();
    }

    private void UpdateDamage()
    {
        CurrentDamage = BaseDamage + IncreasedDamage;
    }
}

/// <summary>序号 74：1 费，移除所有敌人的格挡与覆甲，对所有敌人造成 11 点伤害，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LetMeSee : AraleCardModel
{
    public LetMeSee() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(11m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = CombatState?.HittableEnemies?.ToList();
        if (enemies != null && enemies.Count > 0)
        {
            foreach (var enemy in enemies)
            {
                await PowerCmd.Remove<PlatingPower>(enemy);
                if (enemy.Block > 0m)
                {
                    await CreatureCmd.LoseBlock(choiceContext, enemy, enemy.Block, Owner.Creature);
                }
            }
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_rock_shatter")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>序号 75：1 费，保留，造成 9 点伤害；若目标处于陶醉状态则使其晕眩，消耗。
/// v1：晕眩回合数固定为 1（升级提高回合数待实现）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Intoxicate : AraleCardModel
{
    public Intoxicate() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    /// <summary>金边：有敌人处于陶醉（打出后才会施加晕眩）时亮起。</summary>
    protected override bool ShouldGlowGoldInternal =>
        CombatState?.HittableEnemies?.Any(e => e != null && e.HasPower<AraleIntoxicatedPower>()) ?? false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        if (cardPlay.Target.GetPower<AraleIntoxicatedPower>() != null && cardPlay.Target.Monster != null)
        {
            await CreatureCmd.Stun(cardPlay.Target);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>序号 76：1 费能力，每回合开始时为一张随机手牌添加保留（v1：随机选择），升级后获得固有。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class PlayItSafe : AraleCardModel
{
    public PlayItSafe() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleRetainPerTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

/// <summary>序号 77：1 费，从消耗牌堆选择 1 张牌放入手牌，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Reminisce : AraleCardModel
{
    public Reminisce() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = DynamicVars.Cards.IntValue;
        var pile = PileType.Exhaust.GetPile(Owner);
        if (pile.Cards.Count == 0)
        {
            return;
        }
        var picked = await CardSelectCmd.FromCombatPile(choiceContext, pile, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, Math.Min(count, pile.Cards.Count)));
        if (picked.Any())
        {
            await CardPileCmd.Add(picked, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>序号 78：1 费，选择抽牌堆的 1 张牌使其耗能变为 0，消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DeepThought : AraleCardModel
{
    public DeepThought() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = DynamicVars.Cards.IntValue;
        var pile = PileType.Draw.GetPile(Owner);
        if (pile.Cards.Count == 0)
        {
            return;
        }
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, pile, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, Math.Min(count, pile.Cards.Count)))).ToList();
        foreach (var card in picked)
        {
            card.EnergyCost.SetUntilPlayed(0);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>序号 79 · 巨羊羹：1 费能力，获得 4 层再生与 5 点活力。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class GiantYokan : AraleCardModel
{
    public GiantYokan() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<RegenPower>(4m),
        new PowerVar<VigorPower>(5m)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, DynamicVars["RegenPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["VigorPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["RegenPower"].UpgradeValueBy(1m);
}

/// <summary>落幕：基础1伤害，每点梦想额外增加12/15伤害；单次攻击后清空梦想。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class CurtainCall : AraleCardModel
{
    public CurtainCall() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(1m, ValueProp.Move),
        new DynamicVar("DamagePerDream", 12m),
        new CurtainCallDamagePreviewVar().WithMultiplier((CardModel card, Creature? _) => AraleCmd.Dream(card.Owner.Creature))
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal dream = AraleCmd.Dream(Owner.Creature);
        // 梦想自身、梦想即力量、力量等加成由原生攻击钩子统一结算一次。
        decimal damage = DynamicVars.Damage.BaseValue + DynamicVars["DamagePerDream"].BaseValue * dream;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);
        await AraleCmd.LoseAllDream(choiceContext, Owner, this);
    }

    protected override void OnUpgrade() => DynamicVars["DamagePerDream"].UpgradeValueBy(3m);
}

/// <summary>序号 81：1 费，对所有敌人造成 10 点伤害；处于魔法露露状态时额外造成 1 次并抽牌。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class NiceBulk : AraleCardModel
{
    public NiceBulk() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    /// <summary>金边：处于魔法露露状态（打出后会额外造成 1 次并抽牌）时亮起。</summary>
    protected override bool ShouldGlowGoldInternal => Owner != null && AraleCmd.IsMagicLulu(Owner.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(10m, ValueProp.Move),
        new CardsVar(2)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int hits = AraleCmd.IsMagicLulu(Owner.Creature) ? 2 : 1;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
        if (hits > 1)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

/// <summary>序号 82 · 余音缭绕：2 费，造成 4 点伤害 3 次，本场战斗每施加过 1 层歌声额外造成伤害。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LingeringEcho : AraleCardModel
{
    public LingeringEcho() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    // 动态伤害（参考战士「灰烬打击」）：基础值 + 本场累计施加的歌声层数 x 额外值
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(4m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) =>
            card.Owner.Creature.GetPower<AraleSongGivenTotalPower>()?.Amount ?? 0)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).WithHitCount(3).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.ExtraDamage.UpgradeValueBy(1m);
}

/// <summary>序号 83 · 梦想即力量：2 费能力，梦想的加成效果翻倍（升级后费用降为 1）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DreamIsPower : AraleCardModel
{
    public DreamIsPower() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamForcePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 84：2 费能力，进入魔法露露状态时额外获得梦想（升级后数值 +1）。</summary>
/// <remarks>
/// 9/15 修 bug：卡面写「额外获得{BonusDream}点梦想」而 `BonusDream` 是 1，实际却给了 2 点
/// （能力层数 1 + 代码里写死的 +1）。现在**卡面数值与能力层数共用同一个数**：
/// `BonusDream` = 2（升级 3），能力层数直接就是额外获得的梦想数。
/// </remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ComboMove : AraleCardModel
{
    public ComboMove() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("BonusDream", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleBonusDreamPower>(choiceContext, Owner.Creature, DynamicVars["BonusDream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["BonusDream"].UpgradeValueBy(1m);
}

/// <summary>序号 85：2 费能力，进入魔法露露状态不会清空溢出的锻炼层数（升级后费用降为 1）。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class Overload : AraleCardModel
{
    public Overload() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleOverflowTrainingPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>序号 86：2 费能力，所有保留牌耗能减 1，升级后获得固有。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class YiyiKara : AraleCardModel
{
    public YiyiKara() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleRetainCostPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

/// <summary>序号 87：3 费，造成 26 点伤害，其他敌人失去等量生命。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class NnkImpact : AraleCardModel
{
    public NnkImpact() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(26m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_rock_shatter")
            .Execute(choiceContext);
        decimal dealt = attack.Results.SelectMany(r => r).Sum(r => r.TotalDamage);
        var others = CombatState?.HittableEnemies?.Where(e => e != cardPlay.Target).ToList();
        if (dealt > 0m && others != null && others.Count > 0)
        {
            await CreatureCmd.Damage(choiceContext, others, dealt, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, cardPlay);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6m);
}

/// <summary>序号 88：3 费能力，每回合开始时获得 2 点梦想，升级后获得保留。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class LiveTime : AraleCardModel
{
    public LiveTime() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AraleDreamPerTurnPower>(choiceContext, Owner.Creature, DynamicVars["Dream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

/// <summary>序号 89：4 费，保留，获得 32 点格挡；每被保留 1 个回合，耗能 -1（最低 0），消耗。</summary>
[RegisterCard(typeof(AraleCardPool))]
public sealed class ShutIn : AraleCardModel
{
    public ShutIn() : base(4, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(32m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    /// <summary>
    /// 「每当这张牌被保留时，将其耗能减少 1」。
    ///
    /// <para>★注意：这里**不能**用 <c>HasTurnEndInHandEffect</c> / <c>OnTurnEndInHand</c>★——
    /// 那套机制（原版给「疑惧」「灼伤」这类状态牌用的）会在回合结束时先把牌从手牌移到出牌堆、
    /// 结算完再丢进弃牌堆，于是带「保留」的牌反而**不会**留在手里（旧版就是这样，表现成「不是保留牌」）。
    /// </para>
    ///
    /// <para>正确做法照抄原版附魔「沉眠精华」SlumberingEssence：用 <see cref="BeforeFlush" />，
    /// 它在「清手牌之前」触发，此时牌还在手上 = 正好就是「要被保留」，于是给一个「本场战斗一直有效」的折扣
    /// （<c>AddThisCombat(-1)</c>，战斗内跨回合累积：4 → 3 → 2 → 1 → 0，到 0 后不再变化）。
    /// 费用在降低时自动显示为绿色，由原版 <c>CardCostHelper</c> 判断（有本地费用修正且更低 → Decreased），
    /// 不需要自己染色。</para>
    /// </summary>
    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsMutable)
        {
            return Task.CompletedTask;
        }
        if (Pile?.Type != PileType.Hand)
        {
            return Task.CompletedTask;
        }
        // 已经 0 费就不用再挂了（否则每回合白堆一层修正）
        if (EnergyCost.GetWithModifiers(CostModifiers.Local) <= 0)
        {
            return Task.CompletedTask;
        }
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(10m);
}
