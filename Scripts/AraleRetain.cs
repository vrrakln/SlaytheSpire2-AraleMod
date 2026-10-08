using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Arale.Scripts;

/// <summary>
/// 「保留」附魔：被附魔的牌获得保留。
/// 由遗物「阿拉蕾的书包」在拾起时施加到牌组的 2 张牌上。
///
/// 为什么用附魔而不是直接 <c>CardCmd.ApplyKeyword</c>：存档只保存卡牌的
/// id / 升级次数 / 附魔（<c>SerializableCard</c>），**不保存关键词**，
/// 直接改关键词会在读档后丢失；附魔则会保存，并在读档时由
/// <c>EnchantmentModel.ModifyCard()</c> 重新执行 <see cref="OnEnchant"/> 补回关键词。
/// 参考原版遗物「王室印章」（把 <c>RoyallyApproved</c> 附魔加到牌上）。
/// </summary>
[RegisterEnchantment]
public sealed class AraleRetain : ModEnchantmentTemplate
{
    /// <summary>金色星形书签与粉蓝飘带。</summary>
    public override string? CustomIconPath => "res://arale_mod/images/enchantments/retain_64.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromKeyword(CardKeyword.Retain) };

    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Retain);
    }
}
