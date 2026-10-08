using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Scaffolding.Content;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

/// <summary>
/// 阿拉蕾卡牌的公共基类。
/// 卡图与卡框都按「约定命名 + 文件存在则生效」的规则自动挂载（见 <see cref="AraleArt" />）：
/// 把图片丢进 <c>arale_mod\images\cards\</c> / <c>arale_mod\images\card_frames\</c> 后重新构建即可，
/// 不需要改代码；文件不存在时自动回退到原版素材（卡图回退铁甲战士「打击」，卡框回退原版卡框）。
/// 类名与卡牌的对应关系见 <c>docs\卡牌ID对照表.md</c>。
/// </summary>
public abstract class AraleCardModel : ModCardTemplate
{
    /// <summary>卡图目录（pck 内路径，对应工程目录 arale-mod\arale_mod\images\cards\）。</summary>
    public const string ArtDirectory = AraleArt.CardDir;

    /// <summary>占位卡图：原版铁甲战士「打击」。</summary>
    public const string PlaceholderPortraitPath = AraleArt.PlaceholderPortrait;

    public override string? CustomPortraitPath => AraleArt.PortraitFor(GetType().Name);

    /// <summary>卡图 + 卡框 + 卡图边框 + 标题横幅 的统一入口（缺省字段留 null 即沿用原版）。</summary>
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: AraleArt.PortraitFor(GetType().Name),
        FramePath: AraleArt.FrameFor(Rarity, Type),
        FrameMaterialPath: AraleArt.PaperMaterialFor(Rarity, Type, Pool.IsColorless),
        PortraitBorderPath: AraleArt.PortraitBorderFor(Type),
        EnergyIconPath: AraleEnergy.CardIcon,
        BannerTexturePath: AraleArt.Banner());

    /// <summary>
    /// 鼠标移到卡牌上时，在卡牌旁边显示的机制词条（梦想/歌声/陶醉/锻炼/魔法露露/肌肉棒棒 以及原版状态等）。
    /// 按描述文本里出现的名词自动匹配，见 <see cref="AraleHoverTips" />。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => AraleHoverTips.For(this);

    /// <summary>
    /// 这张牌是否会「锻炼」。用于 ★红边提醒★：
    /// 打出「组间休息」（本回合不能再锻炼）后，手牌里会锻炼的牌会标红。
    /// 凡是 OnPlay 里调用 <c>AraleCmd.Train</c> 的牌都覆盖为 true。
    /// </summary>
    protected virtual bool TrainsThisTurn => false;

    /// <summary>卡面显示的伤害（已含力量/梦想等修正；预览未刷新时退回基础值）。</summary>
    protected decimal PreviewDamage =>
        DynamicVars.Damage.PreviewValue > 0m ? DynamicVars.Damage.PreviewValue : DynamicVars.Damage.BaseValue;

    /// <summary>是否有敌人可能被这张牌的伤害击杀（「击杀」类牌的金边提示用；<b>爪牙也算</b>）。</summary>
    protected bool CanKillAnEnemy =>
        CombatState?.HittableEnemies?.Any(e => e != null && e.CurrentHp <= PreviewDamage) ?? false;

    /// <summary>
    /// 是否有**非爪牙**敌人可能被这张牌的伤害斩杀（「斩杀」类牌的金边提示用）。
    ///
    /// <para>判定口径与原版「狂宴」Feed / 「贪婪之手」HandOfGreed 一致：
    /// 目标身上所有能力都允许「击杀视作斩杀」（<c>ShouldOwnerDeathTriggerFatal()</c>）时才算，
    /// 爪牙身上带的是 <c>MinionPower</c>（返回 false），因此爪牙**不会**让金边亮起。</para>
    /// </summary>
    protected bool CanFatalAnEnemy =>
        CombatState?.HittableEnemies?.Any(e =>
            e != null
            && e.CurrentHp <= PreviewDamage
            && e.Powers.All(p => p.ShouldOwnerDeathTriggerFatal())) ?? false;

    /// <summary>
    /// ★红边警告★（原版 Normality 的用法：卡牌不能打出/有负面时亮红），只需要返回「现在打这张牌有问题」：
    /// <list type="bullet">
    ///   <item>本回合不能再打出攻击牌（「声嘶力竭」）时，手牌里的攻击牌亮红；</item>
    ///   <item>本回合不能再锻炼（「组间休息」）时，手牌里会锻炼的牌亮红。</item>
    /// </list>
    /// 注：原版 UI 只在「你的出牌阶段」显示红边（`NHandCardHolder.ShouldGlowRed`），不需要自己判断阶段。
    /// </summary>
    protected override bool ShouldGlowRedInternal
    {
        get
        {
            Creature? creature = Owner?.Creature;
            if (creature == null)
            {
                return false;
            }
            if (Type == CardType.Attack && creature.GetPower<AraleNoAttackThisTurnPower>() != null)
            {
                return true;
            }
            if (TrainsThisTurn && creature.GetPower<AraleNoTrainPower>() != null)
            {
                return true;
            }
            return false;
        }
    }

    protected AraleCardModel(int energyCost, CardType type, CardRarity rarity, TargetType targetType, bool shouldShowInCardLibrary = true)
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }
}
