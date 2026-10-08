using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Arale.Scripts.Boss;

// 开场拥有 20 格挡，仅显示召唤意图。首次结束回合只召唤；第二次才纵火与颓废。
// 后续敌方行动：剪辑→整备→强化→纵火；美术暂沿用女王。

/// <summary>薇欧拉（第三幕 BOSS）。</summary>
[RegisterMonster]
public sealed class Viola : MonsterModel
{
    /// <summary>剪辑意图的正常与低血量分支。</summary>
    private MoveState _edit = null!, _editLow = null!;

    // ---- 数值（高进阶 / 低进阶）----

    private static int PrepBlock => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 20, 15);

    private static int EditDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 20);

    private static int ArsonDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    /// <summary>纵火的基础段数（再减去阿拉蕾持有的团魂数）。</summary>
    private const int ArsonBaseHits = 4;


    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 535, 512);

    public override int MaxInitialHp => MinInitialHp;


    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Stone;

    /// <summary>★占位美术★：借用原版 BOSS「女王」的怪物场景。</summary>
    protected override string VisualsPath => "res://arale_mod/scenes/viola_combat.tscn";
    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(ModelDb.Monster<CharmedFujiko>().AssetPaths);

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        // 魅惑属于开场设置，不占用第一次敌方行动；两个进阶均为 20 格挡。
        await CreatureCmd.GainBlock(Creature, 20, ValueProp.Move, null);
    }

    private bool IsLowHp => Creature != null && Creature.MaxHp > 0 && Creature.CurrentHp * 2 < Creature.MaxHp;

    /// <summary>每名玩家独立计算；非阿拉蕾玩家没有团魂减段。</summary>
    public static int ArsonHitsFor(Player? player)
    {
        int souls = player?.Character is Arale ? player.Relics.Count(r =>
            r is AraleYunoSoulRelic or AraleRitsuSoulRelic or AraleNonoSoulRelic or AraleFujikoSoulRelic) : 0;
        return System.Math.Max(0, ArsonBaseHits - souls);
    }

    // 本机视角仅用于意图显示，绝不参与实际伤害结算。
    private int ArsonPreviewHits => ArsonHitsFor(MegaCrit.Sts2.Core.Context.LocalContext.GetMe(CombatState));
    internal MegaCrit.Sts2.Core.Commands.Builders.AttackCommand? CurrentArsonAttack { get; private set; }
    internal Creature? CurrentArsonTarget { get; private set; }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<MonsterState> states = new List<MonsterState>();

        MoveState summon = new MoveState("CHARM_MOVE", SummonMove, new SummonIntent());
        MoveState arsonFirst = new MoveState("ARSON_FIRST_MOVE", ArsonMove, new MultiAttackIntent(ArsonDamage, () => ArsonPreviewHits), new DebuffIntent());
        MoveState arson = new MoveState("ARSON_MOVE", ArsonMove, new MultiAttackIntent(ArsonDamage, () => ArsonPreviewHits), new DebuffIntent());
        MoveState edit = _edit = new MoveState("EDIT_MOVE", EditMove, new SingleAttackIntent(EditDamage), new StatusIntent(2));
        MoveState editLow = _editLow = new MoveState("EDIT_LOW_MOVE", EditMove, new SingleAttackIntent(EditDamage), new StatusIntent(1));
        MoveState prep = new MoveState("PREP_MOVE", _ => ResolvePendingStrengthen(), new BuffIntent());

        // 剪辑分「血量正常 / 血量低于 50%」两个状态，这样意图栏显示的塞牌数量与实际一致
        ConditionalBranchState editBranch = new ConditionalBranchState("EDIT_BRANCH");
        editBranch.AddState(editLow, () => IsLowHp);
        editBranch.AddState(edit, () => true);

        summon.FollowUpState = arsonFirst;
        arsonFirst.FollowUpState = editBranch;
        edit.FollowUpState = prep;
        editLow.FollowUpState = prep;
        prep.FollowUpState = arson;
        arson.FollowUpState = editBranch;

        states.Add(summon);
        states.Add(arsonFirst);
        states.Add(arson);
        states.Add(edit);
        states.Add(editLow);
        states.Add(editBranch);
        states.Add(prep);
        return new MonsterMoveStateMachine(states, summon);
    }

    // ---------------------------------------------------------------------------------
    //  行动
    // ---------------------------------------------------------------------------------

    /// <summary>首次敌方回合只召唤，双方从下一回合开始攻击。</summary>
    private async Task SummonMove(IReadOnlyList<Creature> targets)
    {
        await SummonCharmedFujiko();
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Creature && !IsPerformingMove && NextMove != null &&
            (NextMove.Id == "EDIT_MOVE" || NextMove.Id == "EDIT_LOW_MOVE"))
        {
            var desired = IsLowHp ? _editLow : _edit;
            if (NextMove != desired) SetMoveImmediate(desired);
        }
        return Task.CompletedTask;
    }

    /// <summary>纵火：造成多段伤害和脆弱，强化由独立行动结算。</summary>
    private async Task ArsonMove(IReadOnlyList<Creature> targets)
    {
        if (!CombatState.IsLiveCombat() || Creature.IsDead) return;

        // 固定同步玩家顺序并在行动开始时采样，队友不能共享他人的团魂减段。
        var attacks = targets.Where(t => t.IsAlive).Select(t => (Target: t, Hits: ArsonHitsFor(t.Player))).ToList();
        foreach (var (target, hits) in attacks)
        {
            if (!CombatState.IsLiveCombat() || Creature.IsDead) return;
            if (hits <= 0 || !target.IsAlive) continue;
            try
            {
                CurrentArsonTarget = target;
                CurrentArsonAttack = DamageCmd.Attack(ArsonDamage).FromMonster(this).WithHitCount(hits)
                    .WithAttackerAnim("Attack", 0.3f).WithHitFx("vfx/vfx_attack_slash").OnlyPlayAnimOnce();
                await CurrentArsonAttack.Execute(null);
            }
            finally
            {
                CurrentArsonAttack = null;
                CurrentArsonTarget = null;
            }
        }
        if (!CombatState.IsLiveCombat() || Creature.IsDead) return;
        foreach (Creature target in targets.Where(t => t.IsAlive))
        {
            await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), target, 1m, Creature, null);
        }
    }

    /// <summary>剪辑：单体伤害 + 塞状态牌（低血量时改塞 1 张「黯淡」到抽牌堆）。</summary>
    private async Task EditMove(IReadOnlyList<Creature> targets)
    {
        bool bleak = IsLowHp;
        await DamageCmd.Attack(EditDamage).FromMonster(this).WithAttackerAnim("Attack", 0.4f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        if (!CombatState.IsLiveCombat() || Creature.IsDead) return;
        targets = targets.Where(t => t.IsAlive).ToList();
        if (bleak)
        {
            await CardPileCmd.AddToCombatAndPreview<Bleak>(targets, PileType.Draw, 1, null);
            return;
        }

        // 流言始终进入弃牌堆，不随剪辑次数交替。
        await CardPileCmd.AddToCombatAndPreview<Rumor>(targets, PileType.Discard, 2, null);
    }

    /// <summary>进入整备回合时立即获得格挡，显示强化意图；该敌方回合只强化。</summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState != CombatState || Creature.IsDead ||
            NextMove?.Id != "PREP_MOVE" || Creature.GetPower<ViolaStrengthenPendingPower>() != null) return;
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.4f);
        await CreatureCmd.GainBlock(Creature, PrepBlock, ValueProp.Move, null);
        await PowerCmd.Apply<ViolaStrengthenPendingPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
    }

    // ---------------------------------------------------------------------------------
    //  召唤与强化
    // ---------------------------------------------------------------------------------

    /// <summary>召唤藤都子（魅惑版）：挂 99 层陶醉（静默，不造成伤害）+ 白月光 + 爪牙，当回合不攻击。</summary>
    private async Task<CharmedFujiko?> SummonCharmedFujiko()
    {
        var context = new ThrowingPlayerChoiceContext();
        SfxCmd.Play("event:/sfx/enemy/enemy_attacks/fogmog/fogmog_summon");
        // 女王占位场景只有 Cast，没有 Summon 动画触发器。
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.5f);
        if (!CombatState.IsLiveCombat())
        {
            return null;
        }

        Creature? summoned = await CreatureCmd.Add<CharmedFujiko>(CombatState, CharmedFujiko.SlotName);
        if (summoned?.Monster is not CharmedFujiko fujiko)
        {
            return null;
        }

        // CreatureCmd.Add 已调用 AfterAddedToRoom，不能重复初始化。
        return fujiko;
    }

    /// <summary>结算「整备」挂下的强化：薇欧拉 +2 荆棘、双方 +2 力量；血量低于 50% 时荆棘额外 +1。</summary>
    private async Task ResolvePendingStrengthen()
    {
        var pending = Creature.GetPower<ViolaStrengthenPendingPower>();
        if (pending == null)
        {
            return;
        }

        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Remove(pending);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.4f);

        int thorns = IsLowHp ? 3 : 2;
        await PowerCmd.Apply<ThornsPower>(context, Creature, thorns, Creature, null);
        await PowerCmd.Apply<StrengthPower>(context, Creature, 2m, Creature, null);

        var fujiko = FindFujiko();
        if (fujiko != null)
        {
            await PowerCmd.Apply<StrengthPower>(context, fujiko, 2m, Creature, null);
        }
    }

    /// <summary>场上还活着的藤都子（魅惑版）。</summary>
    private Creature? FindFujiko()
    {
        return CombatState?.Creatures?.FirstOrDefault(c => c != null && c.IsAlive && c.Monster is CharmedFujiko);
    }
}

