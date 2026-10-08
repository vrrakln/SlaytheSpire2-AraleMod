using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;
using Arale.Scripts.Quests;

namespace Arale.Scripts;

// =====================================================================================
//  团魂机制：任务书 + 4 个团魂遗物
//  设计文档：docs\团魂体系说明.md（机制本体）、docs\团魂任务生成设计.md（任务生成）
//
//  规则：
//   - 「阿拉蕾的任务书」是初始遗物，开局随机写 4 条任务（主题固定：梦想/锻炼/歌声/保留）；
//   - 战斗结束时结算，达成的任务会在**该场战斗的奖励里**多出一个对应的团魂遗物；
//   - 团魂遗物用 RelicRarity.Event → 原版 RelicGrabBag 只收 Common/Uncommon/Rare/Shop，
//     因此它们**不会**出现在普通遗物奖励/商店里，只能靠任务拿到。
// =====================================================================================

/// <summary>
/// 阿拉蕾的任务书（初始遗物）：开局写下 4 条任务，战斗结束时结算并给出团魂遗物奖励。
///
/// <para><b>状态持久化</b>：全部挂在 <c>[SavedProperty]</c> 上（随存档保存、读档恢复、多人各算各的）。
/// 注意 <c>[SavedProperty]</c> 只支持 int / int[] / bool / string / enum / ModelId / SerializableCard，
/// 所以任务用 int[] 存（模板 ID、档位、进度、是否完成）。</para>
///
/// <para>战斗中的任务纸展示任务与实时进度；遗物图标显示已完成数量。</para>
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
[RegisterCharacterStarterRelic(typeof(Arale))]
public sealed class AraleQuestBookRelic : AraleRelicModel
{
    /// <summary>本场战斗的临时计数（不存档；每场战斗结束时清零）。</summary>
    private int[] _combat = new int[AraleQuestTable.MetricCount];

    /// <summary>本场战斗打出的攻击牌数（花式型「不打出攻击牌」用）。</summary>
    private int _attacksThisCombat;

    // Presentation only: AfterCombatEnd resets counters before the combat manager changes phase.
    private bool _presentationCombatEnded;

    /// <summary>
    /// 稀有度刻意**不用 Starter**：原版「欧洛巴斯之触」是用
    /// <c>Relics.FirstOrDefault(r =&gt; r.Rarity == RelicRarity.Starter)</c> 挑初始遗物的，
    /// 任务书也在初始遗物列表里，若同样是 Starter 就有概率被它"精炼"掉（换成头环 → 任务系统直接失效）。
    /// 用 Event 稀有度既能避开这个坑，又刚好让原版 RelicGrabBag（只收 Common/Uncommon/Rare/Shop）
    /// 不把它算进普通遗物奖励。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleQuestBookRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleQuestBookRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleQuestBookRelic));

    public override bool ShowCounter => true;

    /// <summary>图标上的数字 = 已完成的任务数。</summary>
    public override int DisplayAmount => DoneCount;

    // ---------------------------------------------------------------------------------
    //  存档字段（★属性名会进网络序列化的名称表，发布后不要再改名★）
    // ---------------------------------------------------------------------------------

    /// <summary>是否已经抽过任务（0/1）。</summary>
    [SavedProperty]
    public int QuestRolled { get; private set; }

    /// <summary>每格任务用的模板 ID（= <see cref="AraleQuestTable.All" /> 的索引）。</summary>
    [SavedProperty]
    public int[] QuestTemplateIds { get; private set; } = new int[AraleQuestTable.QuestCount];

    /// <summary>每格任务的难度档（0/1/2 → T1/T2/T3）。</summary>
    [SavedProperty]
    public int[] QuestTiers { get; private set; } = new int[AraleQuestTable.QuestCount];

    /// <summary>每格任务的当前进度（展示用）。</summary>
    [SavedProperty]
    public int[] QuestProgress { get; private set; } = new int[AraleQuestTable.QuestCount];

    /// <summary>每格任务是否已完成（0/1）。</summary>
    [SavedProperty]
    public int[] QuestDone { get; private set; } = new int[AraleQuestTable.QuestCount];

    /// <summary>连续型任务的当前连续场数。</summary>
    [SavedProperty]
    public int[] QuestStreaks { get; private set; } = new int[AraleQuestTable.QuestCount];

    /// <summary>本局累计指标（按 <see cref="AraleQuestMetric" /> 索引）。</summary>
    [SavedProperty]
    public int[] QuestRunTotals { get; private set; } = new int[AraleQuestTable.MetricCount];

    /// <summary>本局实际使用的难度（0 简单 / 1 中等 / 2 困难）——多人为所有阿拉蕾玩家中的最高值。</summary>
    [SavedProperty]
    public int QuestDifficulty { get; private set; }

    /// <summary>本局抽任务用的随机种子（十进制字符串；与游戏种子无关，用于复现/排查）。</summary>
    [SavedProperty]
    public string QuestSeed { get; private set; } = string.Empty;

    private int DoneCount => QuestDone?.Count(d => d != 0) ?? 0;

    /// <summary>
    /// <c>MutableClone()</c> 走的是 <c>MemberwiseClone()</c>（浅拷贝），数组会被两个实例共享，
    /// 因此这里必须重新分配，否则写任务数据会污染原型实例（原版 PaelsTooth 也是这么处理的）。
    /// </summary>
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _combat = new int[AraleQuestTable.MetricCount];
        _attacksThisCombat = 0;
        _presentationCombatEnded = false;
        QuestTemplateIds = new int[AraleQuestTable.QuestCount];
        QuestTiers = new int[AraleQuestTable.QuestCount];
        QuestProgress = new int[AraleQuestTable.QuestCount];
        QuestDone = new int[AraleQuestTable.QuestCount];
        QuestStreaks = new int[AraleQuestTable.QuestCount];
        QuestRunTotals = new int[AraleQuestTable.MetricCount];
        QuestDifficulty = 0;
        QuestSeed = string.Empty;
        QuestRolled = 0;
    }

    // ---------------------------------------------------------------------------------
    //  生命周期
    // ---------------------------------------------------------------------------------

    public override Task AfterObtained()
    {
        EnsureQuests();
        Refresh();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _presentationCombatEnded = false;
        EnsureQuests();
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>战斗结束：推进连续型、结算任务、把团魂塞进这场战斗的奖励里。</summary>
    /// <remarks>
    /// 时机很关键：原版 <c>NCombatUi</c> 在战斗结束流程之后才调 <c>CombatRoom.OfferRoomEndRewards()</c>，
    /// 而奖励一旦生成就不会再补生成，所以必须在这里用 <c>AddExtraReward</c> 塞进去。
    /// </remarks>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (!IsMutable || Owner == null)
        {
            return Task.CompletedTask;
        }

        EnsureQuests();
        bool alive = Owner.Creature == null || !Owner.Creature.IsDead;
        if (alive)
        {
            // 通用任务的几个指标在战斗结束时采样（胜负、卡组诅咒数）
            AddRunTotal(AraleQuestMetric.BattleWins, 1);
            if (_attacksThisCombat == 0)
            {
                AddRunTotal(AraleQuestMetric.WinsWithoutAttacks, 1);
            }
            RaiseRunTotal(AraleQuestMetric.CurseInDeck, CurseInDeckCount());
            EvaluateQuests(room);
        }

        _presentationCombatEnded = true;
        LogQuestState();
        ResetCombatState();
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>进入房间（商店）：记录"进商店时身上带着多少金币"。</summary>
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (!IsMutable || Owner == null || room is not MerchantRoom)
        {
            return Task.CompletedTask;
        }

        RaiseRunTotal(AraleQuestMetric.MaxGoldInShop, Owner.Gold);
        return Task.CompletedTask;
    }

    /// <summary>自己掉血（任何来源）→ 通用任务「不失去生命」用。</summary>
    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (!IsMutable || Owner?.Creature == null || creature != Owner.Creature || delta >= 0m)
        {
            return Task.CompletedTask;
        }

        AddCombatValue(AraleQuestMetric.CombatHpLost, ToInt(-delta));
        return Task.CompletedTask;
    }

    /// <summary>敌人死亡 → 通用任务「累计击败 N 名敌人」用。</summary>
    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!IsMutable || Owner?.Creature == null || wasRemovalPrevented || creature == null || !creature.IsDead)
        {
            return Task.CompletedTask;
        }

        if (creature.Side != Owner.Creature.Side && creature.Player == null)
        {
            AddRunTotal(AraleQuestMetric.EnemiesKilled, 1);
        }

        return Task.CompletedTask;
    }

    /// <summary>回合结束清手牌之前触发：这时手牌里带「保留」的牌就是「被保留的牌」。</summary>
    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsMutable)
        {
            return Task.CompletedTask;
        }

        int retained = RetainInHandCount();
        AddRunTotal(AraleQuestMetric.RetainCountTotal, retained);
        RaiseCombatValue(AraleQuestMetric.RetainInHandPeak, retained);
        Refresh();
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsMutable || Owner == null || cardPlay?.Player?.Creature != Owner.Creature)
        {
            return Task.CompletedTask;
        }

        CardModel card = cardPlay.Card;
        if (card.Type == CardType.Attack)
        {
            _attacksThisCombat++;
        }

        AddRunTotal(AraleQuestMetric.CardsPlayed, 1);
        AddCombatValue(AraleQuestMetric.CardsPlayed, 1);

        if (card.ShouldRetainThisTurn)
        {
            AddRunTotal(AraleQuestMetric.RetainCardsPlayed, 1);
        }

        SampleRetainInHand();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------------------
    //  来自 AraleCmd 的进度上报
    // ---------------------------------------------------------------------------------

    internal void OnDreamGained(decimal amount)
    {
        AddRunTotal(AraleQuestMetric.DreamGained, ToInt(amount));
        AddCombatValue(AraleQuestMetric.DreamGained, ToInt(amount));
        SampleDreamPeak();
    }

    internal void OnDreamConsumed(decimal amount, bool reachedZero)
    {
        if (reachedZero)
        {
            AddRunTotal(AraleQuestMetric.DreamZeroCount, 1);
            AddCombatValue(AraleQuestMetric.DreamZeroCount, 1);
        }
    }

    internal void OnTrained(decimal amount)
    {
        AddRunTotal(AraleQuestMetric.TrainingGained, ToInt(amount));
        AddCombatValue(AraleQuestMetric.TrainingGained, ToInt(amount));
    }

    internal void OnLuluEntered()
    {
        AddRunTotal(AraleQuestMetric.LuluCount, 1);
        AddCombatValue(AraleQuestMetric.LuluCount, 1);
        SampleDreamPeak();
    }

    internal void OnSongApplied(decimal applied, int intoxicatedLayers, decimal hpLoss)
    {
        AddRunTotal(AraleQuestMetric.SongGiven, ToInt(applied));
        AddCombatValue(AraleQuestMetric.SongGiven, ToInt(applied));
        if (intoxicatedLayers > 0)
        {
            AddRunTotal(AraleQuestMetric.IntoxicatedLayers, intoxicatedLayers);
            AddCombatValue(AraleQuestMetric.IntoxicatedLayers, intoxicatedLayers);
        }
        if (hpLoss > 0m)
        {
            AddRunTotal(AraleQuestMetric.IntoxicatedHpLoss, ToInt(hpLoss));
            AddCombatValue(AraleQuestMetric.IntoxicatedHpLoss, ToInt(hpLoss));
        }
    }

    /// <summary>采样手牌里的保留牌数量（峰值型任务）。</summary>
    internal void SampleRetainInHand()
    {
        if (!IsMutable || Owner == null)
        {
            return;
        }

        RaiseCombatValue(AraleQuestMetric.RetainInHandPeak, RetainInHandCount());
    }

    /// <summary>
    /// 手牌里「会被保留的牌」数量。
    /// 用 <c>ShouldRetainThisTurn</c> 而不是 <c>CanonicalKeywords</c>：前者就是原版清手牌时的判定
    /// （含附魔赋予的保留与"本回合保留"效果），和任务文案「手牌里拥有 N 张保留牌」的直觉一致。
    /// </summary>
    private int RetainInHandCount()
    {
        var hand = Owner?.PlayerCombatState?.Hand;
        if (hand?.Cards == null)
        {
            return 0;
        }

        return hand.Cards.Count(c => c != null && c.ShouldRetainThisTurn);
    }

    private void SampleDreamPeak()
    {
        if (!IsMutable || Owner?.Creature == null)
        {
            return;
        }

        RaiseCombatValue(AraleQuestMetric.DreamPeak, ToInt(AraleCmd.Dream(Owner.Creature)));
    }

    // ---------------------------------------------------------------------------------
    //  任务生成与结算
    // ---------------------------------------------------------------------------------

    /// <summary>开局抽 4 条任务（幂等：已经抽过就不动，读档后保持原样）。</summary>
    /// <remarks>
    /// 难度取「所有阿拉蕾玩家里选得最高的那个」（配置页选择，见 <see cref="AraleQuestRunData" />）；
    /// 随机数用**每名玩家自己随身携带的随机种子**（大厅暂存同步），因此任务内容与游戏种子无关、
    /// 各玩家互相独立，但同一名玩家在各客户端上算出来的一定一样。
    /// 种子同步不可用时退回一次性随机（单人游戏一定是这种情况）。
    /// </remarks>
    private void EnsureQuests()
    {
        if (!IsMutable || Owner == null || QuestRolled != 0)
        {
            return;
        }

        AraleQuestDifficulty difficulty = AraleQuestRunData.EffectiveDifficulty(Owner);
        ulong seed = AraleQuestRunData.SeedFor(Owner);
        if (seed == 0UL)
        {
            seed = Rng.Chaotic.NextUnsignedLong();
        }

        var rng = new Rng(seed, "arale_quest");
        AraleQuestGenerator.Roll(rng, difficulty, out int[] templateIds, out int[] tiers);
        QuestTemplateIds = templateIds;
        QuestTiers = tiers;
        QuestProgress = new int[AraleQuestTable.QuestCount];
        QuestDone = new int[AraleQuestTable.QuestCount];
        QuestStreaks = new int[AraleQuestTable.QuestCount];
        QuestRunTotals = new int[AraleQuestTable.MetricCount];
        QuestDifficulty = (int)difficulty;
        QuestSeed = seed.ToString(CultureInfo.InvariantCulture);
        QuestRolled = 1;
        Entry.Logger.Info($"[Arale] quest book rolled: difficulty={AraleQuestGenerator.RecipeName(tiers)} ({difficulty}), tiers=[{string.Join(",", tiers)}], templates=[{string.Join(",", templateIds)}]");
        Refresh();
    }

    private void EvaluateQuests(CombatRoom room)
    {
        for (int slot = 0; slot < AraleQuestTable.QuestCount; slot++)
        {
            if (QuestDone[slot] != 0)
            {
                continue;
            }

            AraleQuestTemplate template = AraleQuestTable.Get(QuestTemplateIds[slot]);
            AraleQuestTier tier = template.Tiers[QuestTiers[slot]];

            if (template.Mode == AraleQuestMode.Streak)
            {
                int combatValue = _combat[(int)template.Metric];
                // AtMost：单场条件反向（例如「本场没有失去生命」= 失去量 ≤ 0）
                bool met = template.AtMost ? combatValue <= tier.A : combatValue >= tier.A;
                QuestStreaks[slot] = met ? QuestStreaks[slot] + 1 : 0;
                QuestProgress[slot] = QuestStreaks[slot];
                if (QuestStreaks[slot] >= tier.B)
                {
                    CompleteQuest(room, slot, template);
                }
                continue;
            }

            // 花式型：本场打出过攻击牌 → 本场不结算（也不写进度，避免把"未达标"显示成进度）
            if (template.RequireNoAttacks && _attacksThisCombat > 0)
            {
                continue;
            }

            // 最值型（MaxEver）与累计型都读本局值；峰值型读本场值
            int value = template.Mode == AraleQuestMode.CombatPeak || template.Mode == AraleQuestMode.Twist
                ? _combat[(int)template.Metric]
                : QuestRunTotals[(int)template.Metric];
            QuestProgress[slot] = value;
            bool done = template.AtMost ? value <= tier.A : value >= tier.A;
            if (done)
            {
                CompleteQuest(room, slot, template);
            }
        }
    }

    private void CompleteQuest(CombatRoom room, int slot, AraleQuestTemplate template)
    {
        QuestDone[slot] = 1;
        QuestProgress[slot] = AraleQuestTable.TargetFor(template, QuestTiers[slot]);
        // ★奖励看「槽位的主题」而不是模板的 Theme★：通用任务顶替了某个槽位后，
        // 仍然奖励那个槽位对应的团魂（模板里的 Theme 只是通用任务的占位值）。
        GrantSoul(room, AraleQuestGenerator.ThemeOfSlot(slot));
    }

    /// <summary>把对应的团魂遗物塞进这场战斗的奖励。</summary>
    private void GrantSoul(CombatRoom room, AraleQuestTheme theme)
    {
        RelicModel soul = theme switch
        {
            AraleQuestTheme.Dream => ModelDb.Relic<AraleYunoSoulRelic>(),
            AraleQuestTheme.Training => ModelDb.Relic<AraleRitsuSoulRelic>(),
            AraleQuestTheme.Song => ModelDb.Relic<AraleNonoSoulRelic>(),
            _ => ModelDb.Relic<AraleFujikoSoulRelic>()
        };

        room?.AddExtraReward(Owner, new RelicReward(soul.ToMutable(), Owner));
        Flash();
    }

    private void ResetCombatState()
    {
        _combat = new int[AraleQuestTable.MetricCount];
        _attacksThisCombat = 0;
    }

    // ---------------------------------------------------------------------------------
    //  指标读写
    // ---------------------------------------------------------------------------------

    private static int ToInt(decimal value)
    {
        return value <= 0m ? 0 : (int)decimal.Truncate(value);
    }

    private void AddRunTotal(AraleQuestMetric metric, int amount)
    {
        if (amount != 0 && QuestRunTotals != null)
        {
            QuestRunTotals[(int)metric] += amount;
        }
    }

    private void AddCombatValue(AraleQuestMetric metric, int amount)
    {
        if (amount != 0)
        {
            _combat[(int)metric] += amount;
        }
    }

    /// <summary>峰值型 / 最值型指标取最大值。</summary>
    private void RaiseCombatValue(AraleQuestMetric metric, int value)
    {
        if (value > _combat[(int)metric])
        {
            _combat[(int)metric] = value;
        }
    }

    /// <summary>本局最值型指标（MaxEver）取最大值。</summary>
    private void RaiseRunTotal(AraleQuestMetric metric, int value)
    {
        if (QuestRunTotals != null && value > QuestRunTotals[(int)metric])
        {
            QuestRunTotals[(int)metric] = value;
        }
    }

    /// <summary>卡组里的诅咒牌数量（通用任务用）。</summary>
    private int CurseInDeckCount()
    {
        if (Owner == null)
        {
            return 0;
        }

        var deck = PileType.Deck.GetPile(Owner)?.Cards;
        return deck?.Count(c => c != null && c.Type == CardType.Curse) ?? 0;
    }

    /// <summary>把本局的任务状态写一行日志（用于按目标完成率标定阈值）。</summary>
    private void LogQuestState()
    {
        if (!IsMutable || QuestRolled == 0)
        {
            return;
        }

        List<string> parts = new List<string>();
        for (int slot = 0; slot < AraleQuestTable.QuestCount; slot++)
        {
            AraleQuestTemplate template = AraleQuestTable.Get(QuestTemplateIds[slot]);
            string name = template.LocKey.Replace("ARALE_QUEST_", string.Empty);
            int target = AraleQuestTable.TargetFor(template, QuestTiers[slot]);
            parts.Add($"{name}={QuestProgress[slot]}/{target}{(QuestDone[slot] != 0 ? "*" : string.Empty)}");
        }

        Entry.Logger.Info($"[Arale] quests done={DoneCount}/4 diff={QuestDifficulty} :: {string.Join(" | ", parts)}");
    }

    // ---------------------------------------------------------------------------------
    //  展示
    // ---------------------------------------------------------------------------------

    /// <summary>刷新遗物图标计数。任务进度由任务纸读取，不写入遗物描述。</summary>
    private void Refresh()
    {
        if (!IsMutable)
        {
            return;
        }

        InvokeDisplayAmountChanged();
    }

    /// <summary>Read live counters without changing gameplay or any SavedProperty.</summary>
    public AraleQuestPresentation? GetQuestPresentation(int slot)
    {
        if (QuestRolled == 0 || slot < 0 || slot >= AraleQuestTable.QuestCount)
        {
            return null;
        }
        AraleQuestTemplate template = AraleQuestTable.Get(QuestTemplateIds[slot]);
        bool inCombat = CombatManager.Instance.IsInProgress && !_presentationCombatEnded;
        int value = inCombat ? _combat[(int)template.Metric] : QuestProgress[slot];
        // Peak sampling also sees the current hand/power between gameplay reporting hooks.
        // This is presentation only: never advance the recorded peak from a render frame.
        if (inCombat && template.Metric == AraleQuestMetric.DreamPeak && Owner?.Creature != null)
        {
            value = System.Math.Max(value, ToInt(AraleCmd.Dream(Owner.Creature)));
        }
        if (inCombat && template.Metric == AraleQuestMetric.RetainInHandPeak)
        {
            value = System.Math.Max(value, RetainInHandCount());
        }
        return AraleQuestPresentation.Create(template, QuestTiers[slot],
            AraleQuestTable.BuildText(QuestTemplateIds[slot], QuestTiers[slot]).GetFormattedText(),
            QuestDone[slot] != 0, value, QuestRunTotals[(int)template.Metric],
            QuestStreaks[slot], _attacksThisCombat, inCombat,
            AraleQuestTable.RewardNameFor(AraleQuestGenerator.ThemeOfSlot(slot)));
    }
}

/// <summary>
/// 由乃的团魂：每回合前两次卡牌格挡获得梦想加成，不消耗梦想。
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleYunoSoulRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleYunoSoulRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleYunoSoulRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleYunoSoulRelic));

    [SavedProperty]
    public int CardBlockGainsThisTurn { get; set; }

    public override Task BeforeCombatStart()
    {
        CardBlockGainsThisTurn = 0;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature)) CardBlockGainsThisTurn = 0;
        return Task.CompletedTask;
    }

    private bool IsOwnerCardBlock(Creature target, ValueProp props, CardModel? source) =>
        target == Owner.Creature && source?.Owner == Owner && props.IsPoweredCardOrMonsterMoveBlock();

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        return CardBlockGainsThisTurn < 2 && IsOwnerCardBlock(target, props, cardSource)
            ? AraleCmd.Dream(Owner.Creature) : 0m;
    }

    public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        // Only completed block gains count. Preview hooks are pure; relic block is excluded.
        if (amount > 0 && IsOwnerCardBlock(creature, props, cardSource))
            CardBlockGainsThisTurn = System.Math.Min(2, CardBlockGainsThisTurn + 1);
        return Task.CompletedTask;
    }
}
/// <summary>律的团魂：每当进入魔法露露状态时，抽 2 张牌。</summary>

[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleRitsuSoulRelic : AraleRelicModel, IAraleMagicLuluListener
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleRitsuSoulRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleRitsuSoulRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleRitsuSoulRelic));

    public async Task OnAraleMagicLuluEntered(PlayerChoiceContext choiceContext, Player owner, CardModel? source)
    {
        if (owner != Owner) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, 2, Owner);
    }
}

/// <summary>野乃花的团魂：陶醉使敌人额外失去 4 点生命值，并给予 1 层虚弱。</summary>
/// <remarks>实际结算在 <see cref="AraleCmd.ApplySong" /> 里，通过 <see cref="IAraleIntoxicationBonus" /> 参与。</remarks>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleNonoSoulRelic : AraleRelicModel, IAraleIntoxicationBonus
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleNonoSoulRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleNonoSoulRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleNonoSoulRelic));

    /// <summary>每层陶醉额外失去 4 点生命值。</summary>
    public decimal ExtraHpLoss => 4m;

    /// <summary>每次产生陶醉时额外给予的虚弱层数。</summary>
    public decimal ExtraWeak => 1m;
}

/// <summary>
/// 藤都子的团魂：每当有卡牌被保留时，将其在本场战斗的耗能减少 1。
///
/// <para>用 <see cref="BeforeFlush" />（清手牌之前触发，此时还留在手上的牌就是「会被保留的牌」），
/// 给每张牌挂一个「打出前一直有效」的折扣。费用降低时游戏会自动把数字显示成绿色。</para>
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleFujikoSoulRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleFujikoSoulRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleFujikoSoulRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleFujikoSoulRelic));

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner?.PlayerCombatState?.Hand == null)
        {
            return Task.CompletedTask;
        }

        bool any = false;
        foreach (CardModel card in Owner.PlayerCombatState.Hand.Cards.ToList())
        {
            if (card == null || !card.IsMutable || !card.ShouldRetainThisTurn)
            {
                continue;
            }

            // 已经 0 费就不用再挂了（否则每回合白堆一层费用修正）
            if (card.EnergyCost.GetWithModifiers(CostModifiers.Local) <= 0)
            {
                continue;
            }

            card.EnergyCost.AddUntilPlayed(-1, reduceOnly: true);
            any = true;
        }

        if (any)
        {
            Flash();
        }

        return Task.CompletedTask;
    }
}

