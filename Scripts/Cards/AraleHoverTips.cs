using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

/// <summary>
/// 悬浮说明框：把「卡牌 / 遗物 / 药水」描述里出现的机制名词，自动映射成鼠标悬停时显示的词条。
/// 做法参考原版（例如「痛击」用 <c>HoverTipFactory.FromPower&lt;VulnerablePower&gt;()</c> 显示易伤词条）。
///
/// <para><b>递归展开</b>：词条自身的描述里如果又提到了别的机制，也会一并展开——
/// 例如「歌声」的描述提到[gold]陶醉[/gold]，因此悬停时歌声词条下面会出现陶醉词条；
/// 「锻炼」提到[gold]魔法露露[/gold]，同理展开。已展开过的名词会被记录，不会无限递归。</para>
///
/// <para><b>为什么用「描述文本提供器」而不是 <c>IHoverTip.CanonicalModel</c></b>：
/// `HoverTipFactory.FromPower&lt;T&gt;()` 走的是 <c>PowerModel.GetDumbHoverTip()</c>，
/// 它构造的 <c>HoverTip</c> 里 <c>CanonicalModel</c> **始终是 null**（原版只给能力施加路径
/// 调用了 <c>SetCanonicalModel</c>），所以之前靠 <c>tip.CanonicalModel</c> 取文本的递归其实永远不会展开。
/// 现在每个词条自己带上「它的原始描述文本」，递归不再依赖 HoverTip 的内部字段。</para>
/// </summary>
public static class AraleHoverTips
{
    /// <summary>最大展开轮数（防止意外循环）。</summary>
    private const int MaxPasses = 16;

    // Keep one registry identity per concept, including both language spellings.
    // Word boundaries prevent matches such as "Train" inside "Strain".
    private static readonly Dictionary<string, string> EnglishWords = new()
    {
        ["梦想"] = "Dream|Dreams", ["歌声"] = "Song", ["陶醉"] = "Intoxication",
        ["锻炼"] = "Train|Training", ["魔法露露"] = "Magic Lulu", ["肌肉棒棒"] = "Muscle Bar|Muscle Bars",
        ["易伤"] = "Vulnerable", ["虚弱"] = "Weak", ["力量"] = "Strength", ["敏捷"] = "Dexterity",
        ["荆棘"] = "Thorns", ["再生"] = "Regen|Regeneration", ["活力"] = "Vigor",
        ["无实体"] = "Intangible", ["覆甲"] = "Plating", ["晕眩"] = "Stun|Stunned",
        ["斩杀"] = "Fatal", ["保留"] = "Retain|Retained"
    };

    private static bool ContainsTerm(string text, string word) =>
        text.Contains(word, StringComparison.Ordinal) ||
        (EnglishWords.TryGetValue(word, out string? aliases) &&
         System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:" + aliases + @")\b",
             System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant));

    /// <summary>名词 → 词条 + 该词条自身的原始描述文本（用于继续展开）。</summary>
    private sealed class Entry
    {
        public required string Word { get; init; }

        public required Func<IHoverTip?> Tip { get; init; }

        /// <summary>该名词对应文本的原始描述（可为 null = 不再展开）。延迟求值，避免在 ModelDb 初始化前访问。</summary>
        public Func<string>? Source { get; init; }
    }

    /// <summary>能力词条：词条本身由 <see cref="HoverTipFactory.FromPower{T}" /> 生成，展开文本取该能力的 description。</summary>
    private static Entry Power<T>(string word) where T : PowerModel => new Entry
    {
        Word = word,
        Tip = () => HoverTipFactory.FromPower<T>(),
        Source = () => RawText(ModelDb.Power<T>().Description)
    };

    /// <summary>卡牌词条（衍生牌等）。</summary>
    private static Entry Card<T>(string word) where T : CardModel => new Entry
    {
        Word = word,
        Tip = () => HoverTipFactory.FromCard<T>(),
        Source = () => RawText(ModelDb.Card<T>().Description)
    };

    /// <summary>关键词词条（保留/消耗这类由游戏自动追加的词）。</summary>
    private static Entry Keyword(string word, CardKeyword keyword) => new Entry
    {
        Word = word,
        Tip = () => HoverTipFactory.FromKeyword(keyword)
    };

    private static readonly Entry[] Registry =
    {
        Power<AraleDreamPower>("梦想"),
        Power<AraleSongPower>("歌声"),
        Power<AraleIntoxicatedPower>("陶醉"),
        Power<AraleTrainingPower>("锻炼"),
        Power<AraleMagicLuluPower>("魔法露露"),
        Card<MuscleBar>("肌肉棒棒"),
        Power<VulnerablePower>("易伤"),
        Power<WeakPower>("虚弱"),
        Power<StrengthPower>("力量"),
        Power<DexterityPower>("敏捷"),
        Power<ThornsPower>("荆棘"),
        Power<RegenPower>("再生"),
        Power<VigorPower>("活力"),
        Power<IntangiblePower>("无实体"),
        Power<PlatingPower>("覆甲"),
        new Entry { Word = "晕眩", Tip = () => HoverTipFactory.Static(StaticHoverTip.Stun) },
        // 「斩杀」是原版的静态词条：标题「斩杀」、说明「当这张牌杀死一名非爪牙敌人时触发。」
        // 原版「狂宴」Feed、「贪婪之手」HandOfGreed、「狩猎」TheHunt 都挂这一条。
        new Entry { Word = "斩杀", Tip = () => HoverTipFactory.Static(StaticHoverTip.Fatal) },
        Keyword("保留", CardKeyword.Retain)
    };

    /// <summary>卡牌：扫描卡牌描述文本（由 <c>AraleCardModel.AdditionalHoverTips</c> 调用）。</summary>
    public static IEnumerable<IHoverTip> For(CardModel card) => For(card?.Description);

    /// <summary>遗物 / 药水：扫描它们自己的描述文本（传 DynamicDescription 即可）。</summary>
    public static IEnumerable<IHoverTip> For(LocString? description)
    {
        List<IHoverTip> tips = new List<IHoverTip>();
        HashSet<string> seenWords = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seenTipIds = new HashSet<string>(StringComparer.Ordinal);
        Queue<string> pendingTexts = new Queue<string>();

        string text0 = RawText(description);
        if (!string.IsNullOrEmpty(text0))
        {
            pendingTexts.Enqueue(text0);
        }

        int passes = 0;
        while (pendingTexts.Count > 0 && passes++ < MaxPasses)
        {
            string text = pendingTexts.Dequeue();
            foreach (Entry entry in Registry)
            {
                if (!ContainsTerm(text, entry.Word) || !seenWords.Add(entry.Word))
                {
                    continue;
                }
                IHoverTip? tip = SafeCreate(entry.Tip);
                if (tip == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(tip.Id) && !seenTipIds.Add(tip.Id))
                {
                    continue;
                }
                tips.Add(tip);

                // 递归：继续扫描该词条自己的描述，把它提到的其它机制也展开
                string subText = SafeSource(entry.Source);
                if (!string.IsNullOrEmpty(subText))
                {
                    pendingTexts.Enqueue(subText);
                }
            }
        }

        return tips;
    }

    private static string RawText(LocString? loc)
    {
        try
        {
            return loc?.GetRawText() ?? string.Empty;
        }
        catch (Exception)
        {
            // 本地化缺失时按空处理，绝不让悬浮提示影响游戏运行
            return string.Empty;
        }
    }

    private static string SafeSource(Func<string>? source)
    {
        if (source == null)
        {
            return string.Empty;
        }
        try
        {
            return source();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static IHoverTip? SafeCreate(Func<IHoverTip?> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
