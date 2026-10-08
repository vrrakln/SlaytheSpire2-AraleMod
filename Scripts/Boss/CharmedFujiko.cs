using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Arale.Scripts.Boss;

// =====================================================================================
//  薇欧拉的召唤物「藤都子（魅惑版）」（设计文档：docs\薇欧拉boss设计.md）
//
//  行动：颓废（20/18 点伤害 + 1 层虚弱）→ 怀疑（8/7 点伤害 3 次）→ 循环
//  被召唤时：99 层陶醉（★静默施加，不造成伤害★）+ 白月光 + 爪牙，当回合不攻击，从下一敌方回合开始行动
//
//  ★美术占位★：立绘同样先借用原版 BOSS「女王」的场景。
// =====================================================================================

/// <summary>藤都子（魅惑版）：薇欧拉召唤出来的爪牙。</summary>
[RegisterMonster]
public sealed class CharmedFujiko : MonsterModel
{
    /// <summary>召唤用的站位（与 <see cref="ViolaBoss" /> 的 Slots 一致）。</summary>
    public const string SlotName = "fujiko";

    private static int DecayDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 20, 18);

    private static int DoubtDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    private const int DoubtRepeat = 3;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 196, 188);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Stone;

    /// <summary>★占位美术★。</summary>
    protected override string VisualsPath => "res://arale_mod/scenes/fujiko_combat.tscn";

    /// <summary>爪牙：不参与「斩杀」结算（与被击杀奖励无关），并在 UI 上按次要点显示。</summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
        // 初始持有陶醉，不走造成生命损失的施加入口。
        await PowerCmd.Apply<Mechanics.AraleIntoxicatedPower>(new ThrowingPlayerChoiceContext(), Creature, 99, Creature, null);
        await PowerCmd.Apply<WhiteMoonlightPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<MonsterState> states = new List<MonsterState>();
        MoveState decay = new MoveState("DECAY_MOVE", DecayMove, new SingleAttackIntent(DecayDamage), new DebuffIntent());
        MoveState doubt = new MoveState("DOUBT_MOVE", DoubtMove, new MultiAttackIntent(DoubtDamage, DoubtRepeat));
        decay.FollowUpState = doubt;
        doubt.FollowUpState = decay;
        states.Add(decay);
        states.Add(doubt);
        return new MonsterMoveStateMachine(states, decay);
    }

    private async Task DecayMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DecayDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        foreach (Creature target in targets.Where(t => t.IsAlive))
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 1m, Creature, null);
        }
    }

    private async Task DoubtMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DoubtDamage).FromMonster(this).WithHitCount(DoubtRepeat)
            .WithAttackerAnim("Attack", 0.25f)
            .OnlyPlayAnimOnce()
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

}

/// <summary>
/// 白月光（藤都子·魅惑版 的能力）：玩家受到来自藤都子的**未被格挡**伤害时，薇欧拉回复等量生命；
/// 藤都子死亡时，给予薇欧拉 3 层陶醉，阿拉蕾回复 10 点生命。
/// </summary>
[RegisterPower]
public sealed class WhiteMoonlightPower : PowerModel, STS2RitsuLib.Scaffolding.Content.Patches.IModPowerAssetOverrides
{
    // Load the PNG directly: the exported AtlasTexture can retain its 64x64 region
    // while losing the backing atlas, leaving an invisible power icon.
    public string CustomIconPath => "res://arale_mod/images/powers/small/WhiteMoonlightPower.png";
    public string CustomBigIconPath => "res://images/powers/arale_mod_power_white_moonlight_power.png";
    public STS2RitsuLib.Scaffolding.Content.PowerAssetProfile AssetProfile => new(
        IconPath: CustomIconPath, BigIconPath: CustomBigIconPath);
    private bool _deathResolved;
    /// <summary>藤都子死亡时给予薇欧拉的陶醉层数。</summary>
    public const int DeathIntoxication = 3;

    /// <summary>藤都子死亡时阿拉蕾回复的生命。</summary>
    public const int DeathHeal = 10;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>玩家受到来自藤都子的伤害时（未被格挡的部分用于治疗薇欧拉）。</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner || result == null || result.UnblockedDamage <= 0)
        {
            return;
        }
        if (target?.Player == null || target.Side == Owner.Side)
        {
            return;
        }

        Creature? viola = FindViola();
        if (viola == null || viola.IsDead)
        {
            return;
        }

        await CreatureCmd.Heal(viola, result.UnblockedDamage, playAnim: true);
    }

    /// <summary>藤都子死亡：给予薇欧拉 3 层陶醉（正常结算，含每层生命损失），阿拉蕾回复 10 点生命。</summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Owner || wasRemovalPrevented || !creature.IsDead || _deathResolved)
        {
            return;
        }

        _deathResolved = true;
        var arales = CombatState.PlayerCreatures.Where(c => c.IsAlive && c.Player?.Character is Arale).ToList();
        Creature? viola = FindViola();
        if (viola != null && !viola.IsDead)
        {
            await PowerCmd.Apply<Mechanics.AraleIntoxicatedPower>(choiceContext, viola, DeathIntoxication, Owner, null);
            // 死亡来源已不可行动；固定生命损失不能用死去的藤都子作伤害执行者。
            await CreatureCmd.Damage(choiceContext, viola, DeathIntoxication * Mechanics.AraleCmd.IntoxicatedHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }

        foreach (Creature player in arales)
        {
            if (player != null && player.IsAlive)
            {
                await CreatureCmd.Heal(player, DeathHeal);
            }
        }
    }

    private Creature? FindViola()
    {
        return CombatState?.Creatures?.FirstOrDefault(c => c != null && c.IsAlive && c.Monster is Viola);
    }
}

/// <summary>
/// 「整备」挂下的强化待办（隐藏能力，只在下一回合开始时结算一次）。
/// 用能力而不是普通字段是为了让它随战斗状态一起存档，中途存档读档不会丢掉这次强化。
/// </summary>
[RegisterPower]
public sealed class ViolaStrengthenPendingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;
}
