using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Arale.Scripts.Mechanics;

// =====================================================================================
//  遗物联动钩子（由 AraleCmd 在关键节点回调）
//
//  为什么用接口：阿拉蕾的核心机制（梦想 / 锻炼 / 歌声 / 魔法露露）都集中在 AraleCmd 里结算，
//  遗物需要在这些节点插一脚。用接口可以让「机制代码」不直接依赖具体遗物类，
//  新增遗物只要实现对应接口即可被自动识别。
// =====================================================================================

/// <summary>每当你锻炼时（对应遗物「保护腕带」）。</summary>
public interface IAraleTrainingListener
{
    /// <param name="amount">本次锻炼的实际层数（已计入「锻炼翻倍」等修正）。</param>
    Task OnAraleTrain(PlayerChoiceContext choiceContext, Player owner, decimal amount);
}

/// <summary>施加歌声时的额外层数（对应遗物「麦克风」）。</summary>
public interface IAraleSongStackBonus
{
    decimal ExtraSongStacks { get; }
}

/// <summary>每当你进入魔法露露状态时（对应遗物「收藏贴纸」）。</summary>
public interface IAraleMagicLuluListener
{
    Task OnAraleMagicLuluEntered(PlayerChoiceContext choiceContext, Player owner, CardModel? source);
}

/// <summary>当你的梦想点数归 0 时（对应遗物「红豆面包」）。</summary>
public interface IAraleDreamZeroListener
{
    Task OnAraleDreamReachedZero(PlayerChoiceContext choiceContext, Player owner);
}

/// <summary>陶醉的额外效果（对应团魂遗物「野乃花的团魂」）。</summary>
public interface IAraleIntoxicationBonus
{
    /// <summary>每层陶醉额外失去的生命值（9/15 起为固定值；陶醉本体是每层固定 10 点）。</summary>
    decimal ExtraHpLoss { get; }

    /// <summary>每次产生陶醉时额外给予的虚弱层数。</summary>
    decimal ExtraWeak { get; }
}

/// <summary>降低进入魔法露露所需的锻炼层数（对应团魂遗物「律的团魂」）。</summary>
public interface IAraleLuluThresholdModifier
{
    /// <summary>替代的阈值；返回 null 表示不改。</summary>
    int? LuluTrainingThreshold { get; }
}
