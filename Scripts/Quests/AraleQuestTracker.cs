using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Arale.Scripts.Quests;

/// <summary>
/// 任务进度的「上报入口」：<see cref="Mechanics.AraleCmd" /> 里所有机制事件（获得/消耗梦想、锻炼、
/// 进入魔法露露、施加歌声）都会调这里，由它转交给该玩家的任务书遗物。
///
/// <para>为什么走遗物而不是静态存档：状态挂在任务书遗物的 <c>[SavedProperty]</c> 上，
/// 于是「随存档保存 / 读档恢复 / 多人各算各的 / 获得时机」全部免费拿到，
/// 不需要自己写存档管线。这里只做一件事：找到遗物并转发。</para>
/// </summary>
public static class AraleQuestTracker
{
    private static AraleQuestBookRelic? Book(Player? player)
    {
        return player?.Relics?.OfType<AraleQuestBookRelic>().FirstOrDefault();
    }

    /// <summary>获得梦想（会顺带采样梦想峰值）。</summary>
    public static void NotifyDreamGained(Player player, decimal amount)
    {
        Book(player)?.OnDreamGained(amount);
    }

    /// <summary>消耗梦想（<paramref name="reachedZero" /> = 本次消耗后梦想归 0）。</summary>
    public static void NotifyDreamConsumed(Player player, decimal amount, bool reachedZero)
    {
        Book(player)?.OnDreamConsumed(amount, reachedZero);
    }

    /// <summary>锻炼（<paramref name="amount" /> 已经是翻倍后的实际层数）。</summary>
    public static void NotifyTrained(Player player, decimal amount)
    {
        Book(player)?.OnTrained(amount);
    }

    /// <summary>进入魔法露露状态。</summary>
    public static void NotifyLuluEntered(Player player)
    {
        Book(player)?.OnLuluEntered();
    }

    /// <summary>施加歌声（含本次转化出的陶醉层数与陶醉造成的生命损失）。</summary>
    public static void NotifySongApplied(Player player, decimal applied, int intoxicatedLayers, decimal hpLoss)
    {
        Book(player)?.OnSongApplied(applied, intoxicatedLayers, hpLoss);
    }

    /// <summary>采样「手牌里的保留牌数量」（峰值型任务用）。</summary>
    public static void NotifyRetainInHand(Player player)
    {
        Book(player)?.SampleRetainInHand();
    }
}
