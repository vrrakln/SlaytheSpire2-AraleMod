using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Random;

namespace Arale.Scripts.Quests;

// =====================================================================================
//  团魂「任务书」的数据层：主题 / 玩法型 / 指标 / 模板表 / 难度档
//  设计文档：docs\团魂体系说明.md、docs\团魂任务生成设计.md
//
//  ★注意★：任务模板的阈值一律来自「指标」在游戏里真实可达的范围（见设计稿第二节），
//  尤其是：敌人身上的歌声永远是 0~4 层（每 5 层立刻转陶醉），所以歌声任务只能写「累计施加量」。
//
//  9/15 第三次平衡调整：
//   - 全部阈值上调（目标：一局平均完成 ≈1.6 / 1.2 / 1.0 条，分别对应简单 / 中等 / 困难）；
//   - 新增「通用任务」：与阿拉蕾机制无关的任务，开局时有概率替换掉 4 条专属任务中的 1 条；
//   - 新增两种判定：`MaxEver`（本局出现过的最值）与 `AtMost`（≤ 判定，用于「不失去生命」）。
// =====================================================================================

/// <summary>任务主题：决定奖励哪一个团魂遗物。每局固定 4 条，各占一个主题。</summary>
public enum AraleQuestTheme
{
    /// <summary>梦想 → 由乃的团魂。</summary>
    Dream = 0,

    /// <summary>锻炼 / 魔法露露 → 律的团魂。</summary>
    Training = 1,

    /// <summary>歌声 → 野乃花的团魂。</summary>
    Song = 2,

    /// <summary>保留 → 藤都子的团魂。</summary>
    Retain = 3
}

/// <summary>任务的「玩法型」：决定什么样的行为算推进。</summary>
public enum AraleQuestMode
{
    /// <summary>峰值型：任意一场战斗中达到阈值（战斗结束结算）。</summary>
    CombatPeak = 0,

    /// <summary>累计型：本局游戏累计达到阈值。</summary>
    RunTotal = 1,

    /// <summary>连续型：连续 N 场战斗每场都达到单场阈值。</summary>
    Streak = 2,

    /// <summary>花式型：峰值型 + 额外约束（例如本场未打出攻击牌）。</summary>
    Twist = 3,

    /// <summary>最值型：本局里该指标**曾经**达到过阈值（例如进商店时的金币、卡组里的诅咒数）。</summary>
    MaxEver = 4
}

/// <summary>被追踪的指标。每个指标同时维护「本场战斗值」与「本局累计值」。</summary>
public enum AraleQuestMetric
{
    /// <summary>战斗中梦想点数的最高值。</summary>
    DreamPeak = 0,

    /// <summary>获得的梦想合计。</summary>
    DreamGained = 1,

    /// <summary>梦想被消耗到 0 的次数。</summary>
    DreamZeroCount = 2,

    /// <summary>锻炼层数合计。</summary>
    TrainingGained = 3,

    /// <summary>进入魔法露露状态的次数。</summary>
    LuluCount = 4,

    /// <summary>累计施加的歌声层数。</summary>
    SongGiven = 5,

    /// <summary>产生的陶醉层数。</summary>
    IntoxicatedLayers = 6,

    /// <summary>陶醉造成的生命损失合计。</summary>
    IntoxicatedHpLoss = 7,

    /// <summary>手牌里同时拥有的保留牌数量峰值。</summary>
    RetainInHandPeak = 8,

    /// <summary>「牌被保留」的累计次数（每回合结束时留在手牌的保留牌数之和）。</summary>
    RetainCountTotal = 9,

    /// <summary>打出的带保留的牌数。</summary>
    RetainCardsPlayed = 10,

    /// <summary>本场战斗自己失去的生命合计（通用任务「不失去生命」用）。</summary>
    CombatHpLost = 11,

    /// <summary>打出的牌数（本场 / 本局都记）。</summary>
    CardsPlayed = 12,

    /// <summary>击杀的敌人数量（本局累计）。</summary>
    EnemiesKilled = 13,

    /// <summary>进入商店时身上带着的金币（本局最大值）。</summary>
    MaxGoldInShop = 14,

    /// <summary>获胜的战斗场数（本局累计）。</summary>
    BattleWins = 15,

    /// <summary>「不打出攻击牌获胜」的场数（本局累计）。</summary>
    WinsWithoutAttacks = 16,

    /// <summary>卡组里的诅咒牌数量（本局最大值）。</summary>
    CurseInDeck = 17
}

/// <summary>一档的两个数字：<see cref="A" /> 与 <see cref="B" />。</summary>
/// <remarks>
/// 含义随玩法型而定：
/// <list type="bullet">
///   <item>峰值型 / 累计型 / 花式型 / 最值型：<c>A</c> = 目标值。</item>
///   <item>连续型：<c>A</c> = 每场战斗的单场门槛，<c>B</c> = 需要连续的场数。</item>
/// </list>
/// </remarks>
public readonly struct AraleQuestTier
{
    public AraleQuestTier(int a, int b = 0)
    {
        A = a;
        B = b;
    }

    public int A { get; }

    public int B { get; }
}

/// <summary>一条任务模板。</summary>
public sealed class AraleQuestTemplate
{
    public required AraleQuestTheme Theme { get; init; }

    /// <summary>本地化键（放在原版 <c>static_hover_tips</c> 表里，见 AraleQuestTable.LocTable）。</summary>
    public required string LocKey { get; init; }

    public required AraleQuestMode Mode { get; init; }

    public required AraleQuestMetric Metric { get; init; }

    /// <summary>三档难度（T1 / T2 / T3）。</summary>
    public required AraleQuestTier[] Tiers { get; init; }

    /// <summary>花式型专用：是否要求本场战斗没有打出过攻击牌。</summary>
    public bool RequireNoAttacks { get; init; }

    /// <summary>判定方向反转：目标变成「不超过 A」（用于「不失去生命」这类条件）。</summary>
    public bool AtMost { get; init; }

    /// <summary>通用任务（与阿拉蕾机制无关，会顶替掉某个主题的任务）。</summary>
    public bool IsGeneric { get; init; }

    /// <summary>本条任务奖励的团魂遗物名（展示/日志用）。</summary>
    public required string RewardName { get; init; }
}

/// <summary>模板表：专属任务 = 4 个主题 × 5 种玩法型 = 20 条，另有 6 条**通用任务**。</summary>
public static class AraleQuestTable
{
    /// <summary>每局生成的任务条数（= 主题数）。</summary>
    public const int QuestCount = 4;

    /// <summary>难度档数量（T1/T2/T3）。</summary>
    public const int TierCount = 3;

    /// <summary>指标数量。</summary>
    public const int MetricCount = 18;

    /// <summary>
    /// 任务文案所在的本地化表。原版只会为「原版语言目录里存在的表名」建表，mod 只能往这些表里合并键，
    /// 所以任务文案借用原版的 <c>static_hover_tips</c> 表。
    /// </summary>
    public const string LocTable = "static_hover_tips";

    /// <summary>
    /// 全部模板，索引即持久化用的模板 ID（**新模板只能往末尾追加**，否则旧存档的任务会串味）。
    /// 前 20 条是 4 主题 × 5 型的专属任务，21 条之后是通用任务。
    /// </summary>
    public static readonly IReadOnlyList<AraleQuestTemplate> All = new List<AraleQuestTemplate>
    {
        // ---------- 主题 1：梦想（由乃的团魂） ----------
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "由乃的团魂",
            LocKey = "ARALE_QUEST_DREAM_PEAK",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.DreamPeak,
            Tiers = new[] { new AraleQuestTier(6), new AraleQuestTier(9), new AraleQuestTier(13) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "由乃的团魂",
            LocKey = "ARALE_QUEST_DREAM_TOTAL",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.DreamGained,
            Tiers = new[] { new AraleQuestTier(55), new AraleQuestTier(110), new AraleQuestTier(180) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "由乃的团魂",
            LocKey = "ARALE_QUEST_DREAM_ZERO",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.DreamZeroCount,
            Tiers = new[] { new AraleQuestTier(5), new AraleQuestTier(10), new AraleQuestTier(16) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "由乃的团魂",
            LocKey = "ARALE_QUEST_DREAM_STREAK",
            Mode = AraleQuestMode.Streak,
            Metric = AraleQuestMetric.DreamPeak,
            Tiers = new[] { new AraleQuestTier(4, 3), new AraleQuestTier(6, 3), new AraleQuestTier(8, 4) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "由乃的团魂",
            LocKey = "ARALE_QUEST_DREAM_TWIST",
            Mode = AraleQuestMode.Twist,
            Metric = AraleQuestMetric.DreamPeak,
            RequireNoAttacks = true,
            Tiers = new[] { new AraleQuestTier(5), new AraleQuestTier(7), new AraleQuestTier(9) }
        },

        // ---------- 主题 2：锻炼 / 魔法露露（律的团魂） ----------
        new()
        {
            Theme = AraleQuestTheme.Training,
            RewardName = "律的团魂",
            LocKey = "ARALE_QUEST_TRAIN_PEAK",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.TrainingGained,
            Tiers = new[] { new AraleQuestTier(14), new AraleQuestTier(22), new AraleQuestTier(32) }
        },
        new()
        {
            Theme = AraleQuestTheme.Training,
            RewardName = "律的团魂",
            LocKey = "ARALE_QUEST_TRAIN_TOTAL",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.TrainingGained,
            Tiers = new[] { new AraleQuestTier(70), new AraleQuestTier(140), new AraleQuestTier(240) }
        },
        new()
        {
            Theme = AraleQuestTheme.Training,
            RewardName = "律的团魂",
            LocKey = "ARALE_QUEST_LULU_TOTAL",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.LuluCount,
            Tiers = new[] { new AraleQuestTier(5), new AraleQuestTier(8), new AraleQuestTier(12) }
        },
        new()
        {
            Theme = AraleQuestTheme.Training,
            RewardName = "律的团魂",
            LocKey = "ARALE_QUEST_LULU_STREAK",
            Mode = AraleQuestMode.Streak,
            Metric = AraleQuestMetric.LuluCount,
            Tiers = new[] { new AraleQuestTier(1, 3), new AraleQuestTier(1, 4), new AraleQuestTier(1, 5) }
        },
        new()
        {
            Theme = AraleQuestTheme.Training,
            RewardName = "律的团魂",
            LocKey = "ARALE_QUEST_LULU_MULTI",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.LuluCount,
            Tiers = new[] { new AraleQuestTier(2), new AraleQuestTier(3), new AraleQuestTier(4) }
        },

        // ---------- 主题 3：歌声（野乃花的团魂） ----------
        new()
        {
            Theme = AraleQuestTheme.Song,
            RewardName = "野乃花的团魂",
            LocKey = "ARALE_QUEST_SONG_PEAK",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.SongGiven,
            Tiers = new[] { new AraleQuestTier(20), new AraleQuestTier(32), new AraleQuestTier(50) }
        },
        new()
        {
            Theme = AraleQuestTheme.Song,
            RewardName = "野乃花的团魂",
            LocKey = "ARALE_QUEST_SONG_TOTAL",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.SongGiven,
            Tiers = new[] { new AraleQuestTier(110), new AraleQuestTier(200), new AraleQuestTier(320) }
        },
        new()
        {
            Theme = AraleQuestTheme.Song,
            RewardName = "野乃花的团魂",
            LocKey = "ARALE_QUEST_INTOX_LAYERS",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.IntoxicatedLayers,
            Tiers = new[] { new AraleQuestTier(8), new AraleQuestTier(15), new AraleQuestTier(24) }
        },
        new()
        {
            Theme = AraleQuestTheme.Song,
            RewardName = "野乃花的团魂",
            LocKey = "ARALE_QUEST_SONG_STREAK",
            Mode = AraleQuestMode.Streak,
            Metric = AraleQuestMetric.SongGiven,
            Tiers = new[] { new AraleQuestTier(12, 3), new AraleQuestTier(14, 4), new AraleQuestTier(16, 5) }
        },
        new()
        {
            Theme = AraleQuestTheme.Song,
            RewardName = "野乃花的团魂",
            LocKey = "ARALE_QUEST_INTOX_HP",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.IntoxicatedHpLoss,
            // 9/15 陶醉改成每层固定 10 点（原为 10% 最大生命），阈值随之重算
            Tiers = new[] { new AraleQuestTier(20), new AraleQuestTier(35), new AraleQuestTier(60) }
        },

        // ---------- 主题 4：保留（藤都子的团魂） ----------
        new()
        {
            Theme = AraleQuestTheme.Retain,
            RewardName = "藤都子的团魂",
            LocKey = "ARALE_QUEST_RETAIN_PEAK",
            Mode = AraleQuestMode.CombatPeak,
            Metric = AraleQuestMetric.RetainInHandPeak,
            Tiers = new[] { new AraleQuestTier(5), new AraleQuestTier(7), new AraleQuestTier(9) }
        },
        new()
        {
            Theme = AraleQuestTheme.Retain,
            RewardName = "藤都子的团魂",
            LocKey = "ARALE_QUEST_RETAIN_TOTAL",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.RetainCountTotal,
            Tiers = new[] { new AraleQuestTier(55), new AraleQuestTier(100), new AraleQuestTier(170) }
        },
        new()
        {
            Theme = AraleQuestTheme.Retain,
            RewardName = "藤都子的团魂",
            LocKey = "ARALE_QUEST_RETAIN_PLAYED",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.RetainCardsPlayed,
            Tiers = new[] { new AraleQuestTier(14), new AraleQuestTier(26), new AraleQuestTier(42) }
        },
        new()
        {
            Theme = AraleQuestTheme.Retain,
            RewardName = "藤都子的团魂",
            LocKey = "ARALE_QUEST_RETAIN_STREAK",
            Mode = AraleQuestMode.Streak,
            Metric = AraleQuestMetric.RetainInHandPeak,
            Tiers = new[] { new AraleQuestTier(3, 3), new AraleQuestTier(4, 3), new AraleQuestTier(5, 4) }
        },
        new()
        {
            Theme = AraleQuestTheme.Retain,
            RewardName = "藤都子的团魂",
            LocKey = "ARALE_QUEST_RETAIN_TWIST",
            Mode = AraleQuestMode.Twist,
            Metric = AraleQuestMetric.RetainInHandPeak,
            RequireNoAttacks = true,
            Tiers = new[] { new AraleQuestTier(3), new AraleQuestTier(4), new AraleQuestTier(5) }
        },

        // ---------- 通用任务（与阿拉蕾机制无关；★新模板只能追加在末尾★） ----------
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_NO_DAMAGE",
            Mode = AraleQuestMode.Streak,
            Metric = AraleQuestMetric.CombatHpLost,
            AtMost = true,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(0, 2), new AraleQuestTier(0, 3), new AraleQuestTier(0, 4) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_SHOP_GOLD",
            Mode = AraleQuestMode.MaxEver,
            Metric = AraleQuestMetric.MaxGoldInShop,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(500), new AraleQuestTier(600), new AraleQuestTier(700) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_WIN_NO_ATTACK",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.WinsWithoutAttacks,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(1), new AraleQuestTier(2), new AraleQuestTier(3) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_CURSES",
            Mode = AraleQuestMode.MaxEver,
            Metric = AraleQuestMetric.CurseInDeck,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(3), new AraleQuestTier(4), new AraleQuestTier(5) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_KILLS",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.EnemiesKilled,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(20), new AraleQuestTier(30), new AraleQuestTier(45) }
        },
        new()
        {
            Theme = AraleQuestTheme.Dream,
            RewardName = "通用任务",
            LocKey = "ARALE_QUEST_GEN_CARDS",
            Mode = AraleQuestMode.RunTotal,
            Metric = AraleQuestMetric.CardsPlayed,
            IsGeneric = true,
            Tiers = new[] { new AraleQuestTier(120), new AraleQuestTier(200), new AraleQuestTier(300) }
        }
    };

    /// <summary>某主题的专属模板索引（本主题内的 5 条）。</summary>
    public static IReadOnlyList<int> IndicesForTheme(AraleQuestTheme theme)
    {
        List<int> list = new List<int>();
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].Theme == theme && !All[i].IsGeneric)
            {
                list.Add(i);
            }
        }
        return list;
    }

    /// <summary>全部通用模板索引。</summary>
    public static IReadOnlyList<int> GenericIndices { get; } = BuildGenericIndices();

    private static IReadOnlyList<int> BuildGenericIndices()
    {
        List<int> list = new List<int>();
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].IsGeneric)
            {
                list.Add(i);
            }
        }
        return list;
    }

    public static AraleQuestTemplate Get(int templateIndex)
    {
        return All[templateIndex];
    }

    /// <summary>该档的目标值（连续型返回需要连续的场数）。</summary>
    public static int TargetFor(AraleQuestTemplate template, int tier)
    {
        AraleQuestTier t = template.Tiers[tier];
        return template.Mode == AraleQuestMode.Streak ? t.B : t.A;
    }

    /// <summary>生成任务文案（含 {N}/{M} 参数，不含进度后缀）。</summary>
    public static LocString BuildText(int templateIndex, int tier)
    {
        AraleQuestTemplate template = Get(templateIndex);
        AraleQuestTier t = template.Tiers[tier];
        LocString loc = new LocString(LocTable, template.LocKey);
        loc.Add("N", t.A);
        if (template.Mode == AraleQuestMode.Streak)
        {
            loc.Add("M", t.B);
        }
        return loc;
    }

    /// <summary>该主题专属任务奖励的团魂名。</summary>
    public static string RewardNameFor(AraleQuestTheme theme)
    {
        string relic = theme switch { AraleQuestTheme.Dream => "YUNO", AraleQuestTheme.Training => "RITSU", AraleQuestTheme.Song => "NONO", _ => "FUJIKO" };
        return new LocString("relics", $"ARALE_MOD_RELIC_ARALE_{relic}_SOUL_RELIC.title").GetFormattedText();
    }
}

