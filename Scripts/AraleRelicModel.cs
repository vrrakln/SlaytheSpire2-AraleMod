using System.Collections.Generic;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Scaffolding.Content;
using Arale.Scripts.Cards;

namespace Arale.Scripts;

/// <summary>
/// 阿拉蕾遗物基类：把遗物描述里出现的机制名词，自动变成鼠标悬停时显示的说明词条
/// （与卡牌用的是同一套 <see cref="AraleHoverTips" />）。
///
/// 例：「安可曲目」的描述里写了[gold]歌声[/gold]与[gold]陶醉[/gold] → 悬停时会显示这两条词条；
/// 「收藏贴纸」写了[gold]魔法露露[/gold] → 显示魔法露露词条（魔法露露虽然不在状态栏显示，
/// 但它的词条本身是可以正常取到的，见 <see cref="AraleHoverTips" /> 的说明）。
///
/// 注意：RitsuLib 的 <c>ModRelicTemplate.ExtraHoverTips</c> 是 sealed，扩展点是 <c>AdditionalHoverTips</c>
/// （与卡牌、药水一致）。
/// </summary>
public abstract class AraleRelicModel : ModRelicTemplate
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => AraleHoverTips.For(DynamicDescription);
}
