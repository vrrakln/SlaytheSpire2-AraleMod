using System.Collections.Generic;
using MegaCrit.Sts2.Core.Random;

namespace Arale.Scripts.Quests;

/// <summary>
/// 任务生成器：一局只在开局跑一次（结果存进任务书遗物的 <c>[SavedProperty]</c>，读档不会重抽）。
///
/// <para><b>难度构成用「配方」而不是 4 条各自随机</b>：纯随机会出现「4 条全是 T1」（白送 4 个团魂）
/// 或「4 条全是 T3」（一局颗粒无收）。配方把每局的总工作量压进一个窄区间，
/// 随机性则体现在「哪条任务是哪一档」以及「每条抽到哪种玩法型」。
/// 依据见 <c>docs\团魂任务生成设计.md</c> 第三节。</para>
/// </summary>
public static class AraleQuestGenerator
{
    /// <summary>三种难度配方（索引 = 档位，0 = T1 / 1 = T2 / 2 = T3）。</summary>
    private static readonly int[][] Recipes =
    {
        new[] { 0, 0, 1, 1 }, // 甲·轻松（简单）
        new[] { 0, 1, 1, 2 }, // 乙·标准（中等，默认）
        new[] { 1, 1, 2, 2 }  // 丙·挑战（困难）
    };

    /// <summary>主题按固定顺序占位：第 1 条梦想、第 2 条锻炼、第 3 条歌声、第 4 条保留。</summary>
    private static readonly AraleQuestTheme[] SlotThemes =
    {
        AraleQuestTheme.Dream,
        AraleQuestTheme.Training,
        AraleQuestTheme.Song,
        AraleQuestTheme.Retain
    };

    public static AraleQuestTheme ThemeOfSlot(int slot)
    {
        return SlotThemes[slot];
    }

    /// <summary>难度配方：简单 = 轻松、中等 = 标准、困难 = 挑战。</summary>
    public static int[] RecipeFor(AraleQuestDifficulty difficulty)
    {
        return Recipes[Math.Clamp((int)difficulty, 0, Recipes.Length - 1)];
    }

    /// <summary>
    /// 4 条任务里「有 1 条被换成通用任务」的概率，按难度递增
    /// （通用任务与阿拉蕾机制无关，用来打乱"只顾着刷本命机制"的套路）。
    /// </summary>
    private static readonly float[] GenericChance = { 0.30f, 0.40f, 0.50f };

    /// <summary>
    /// 抽一局的任务。<paramref name="templateIds" /> 是 <see cref="AraleQuestTable.All" /> 的索引，
    /// <paramref name="tiers" /> 是 0/1/2（对应 T1/T2/T3）。
    /// </summary>
    public static void Roll(Rng rng, AraleQuestDifficulty difficulty, out int[] templateIds, out int[] tiers)
    {
        templateIds = new int[AraleQuestTable.QuestCount];
        tiers = new int[AraleQuestTable.QuestCount];

        // 1. 按配置的难度取配方，得到 4 个档位
        int[] recipe = RecipeFor(difficulty);
        for (int i = 0; i < tiers.Length; i++)
        {
            tiers[i] = recipe[i];
        }

        // 2. 档位在 4 个主题之间洗牌（避免「第 1 条永远最简单」的定式）
        Shuffle(rng, tiers);

        // 3. 每个主题各抽一条模板（允许不同主题抽到同一种玩法型）
        for (int slot = 0; slot < SlotThemes.Length; slot++)
        {
            IReadOnlyList<int> pool = AraleQuestTable.IndicesForTheme(SlotThemes[slot]);
            templateIds[slot] = pool[rng.NextInt(0, pool.Count)];
        }

        // 4. 有概率把其中随机一条换成**通用任务**（只换任务内容；奖励仍看这个槽位的主题）
        if (rng.NextFloat() < GenericChance[Math.Clamp((int)difficulty, 0, GenericChance.Length - 1)])
        {
            IReadOnlyList<int> generic = AraleQuestTable.GenericIndices;
            if (generic.Count > 0)
            {
                int slot = rng.NextInt(0, AraleQuestTable.QuestCount);
                templateIds[slot] = generic[rng.NextInt(0, generic.Count)];
            }
        }
    }

    /// <summary>本局用到的配方名（日志/调试用）。</summary>
    public static string RecipeName(int[] tiers)
    {
        int sum = 0;
        foreach (int t in tiers)
        {
            sum += t;
        }
        return sum switch
        {
            <= 2 => "简单",
            <= 4 => "中等",
            _ => "困难"
        };
    }

    private static void Shuffle(Rng rng, int[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = rng.NextInt(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
