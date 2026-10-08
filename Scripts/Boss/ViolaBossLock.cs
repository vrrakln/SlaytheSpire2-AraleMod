using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Arale.Scripts.Quests;

namespace Arale.Scripts.Boss;

/// <summary>
/// 关闭时过滤薇欧拉；随机时保留原版遭遇池抽取（包含已注册的薇欧拉）。
/// 开启后单 BOSS 固定薇欧拉；双 BOSS 先等概率决定薇欧拉的位置，
/// 再从本幕其余 BOSS 中抽取另一场。原版发现顺序修正完成后才执行，
/// 使用本局同步的 UpFront 随机流，不在读档或打开地图时重抽。
/// </summary>
public static class ViolaBossLock
{
    /// <summary>本局遭遇模式：多人时只读取房主同步的设置。</summary>
    public static AraleViolaEncounterMode Mode => AraleQuestRunData.EffectiveViolaMode(AraleSettings.ViolaMode);
    public static bool IsLocked => Mode == AraleViolaEncounterMode.Locked;

    internal static IEnumerable<EncounterModel> FilterPool(ActModel act, IEnumerable<EncounterModel> pool, AraleViolaEncounterMode mode)
    {
        // Filter the returned view, not ActModel's cached pool: switching settings must be reversible.
        return act is Glory && mode == AraleViolaEncounterMode.Disabled
            ? pool.Where(encounter => encounter is not ViolaBoss)
            : pool;
    }

    /// <summary>
    /// 本局是否是「进阶 10 双 BOSS」。用 <c>RunManager.Instance.AscensionManager</c> 而不是缓存状态，
    /// 因为它在开局初始化（<c>InitializeShared</c>）时就已经设好，早于房间生成；拿不到时按「否」处理。
    /// </summary>
    public static bool IsDoubleBossRun
    {
        get
        {
            try
            {
                return RunManager.Instance?.AscensionManager?.HasLevel(AscensionLevel.DoubleBoss) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 单 BOSS 模式：把该章节（如果是第三幕）唯一的 BOSS 换成薇欧拉。
    /// 双 BOSS 模式（进阶 10）在这里**什么都不做**，交给 <see cref="ApplyDoubleBossRule" />。
    /// </summary>
    public static void ForceIfNeeded(ActModel? act)
    {
        if (act is not Glory)
        {
            return;
        }

        try
        {
            if (!IsLocked || IsDoubleBossRun)
            {
                return;
            }

            EncounterModel viola = ModelDb.Encounter<ViolaBoss>();
            if (act.BossEncounter != null && act.BossEncounter.Id == viola.Id)
            {
                return;
            }

            act.SetBossEncounter(viola);
            Entry.Logger.Info("[Arale] act 3 boss locked to Viola (single boss)");
        }
        catch (System.Exception ex)
        {
            // 锁 BOSS 失败不能影响开局（退回原版的随机 BOSS）
            Entry.Logger.Warn("Arale viola boss lock failed: " + ex.Message);
        }
    }

    /// <summary>原版生成两场遭遇并应用发现顺序后，执行锁定规则。</summary>
    public static void ApplyDoubleBossRule()
    {
        try
        {
            if (!IsLocked || !IsDoubleBossRun)
                return;
            var state = AraleQuestRunData.CachedRunState;
            if (state == null || AraleQuestRunData.LastAct is not Glory act)
                return;
            ConfigureDoubleBoss(act, state.Rng.UpFront, true);
            Entry.Logger.Info("[Arale] act 3 locked double boss: first=" + act.BossEncounter.Id.Entry
                + ", second=" + act.SecondBossEncounter?.Id.Entry);
        }
        catch (System.Exception ex)
        {
            Entry.Logger.Warn("Arale viola double boss rule failed: " + ex.Message);
        }
    }

    internal static void ConfigureDoubleBoss(ActModel act, MegaCrit.Sts2.Core.Random.Rng rng, bool locked)
    {
        if (!locked || act is not Glory)
            return;
        EncounterModel viola = ModelDb.Encounter<ViolaBoss>();
        // Use the actual registered encounter pool, including other mods' bosses.
        List<EncounterModel> pool = act.AllBossEncounters.Where(e => e.Id != viola.Id).ToList();
        if (pool.Count == 0)
            return;
        bool violaFirst = rng.NextBool();
        EncounterModel other = rng.NextItem(pool)!; // Non-empty pool checked above.
        act.SetBossEncounter(violaFirst ? viola : other);
        act.SetSecondBossEncounter(violaFirst ? other : viola);
    }
}

// Both the first and second boss are drawn from this property by the native generator.
[HarmonyPatch(typeof(ActModel), nameof(ActModel.AllBossEncounters), MethodType.Getter)]
internal static class ViolaBossEncounterPoolPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref IEnumerable<EncounterModel> __result)
    {
        if (__instance is Glory)
            __result = ViolaBossLock.FilterPool(__instance, __result, ViolaBossLock.Mode);
    }
}

/// <summary>第三幕房间生成完成后尝试锁定（非标准模式也会跑）。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class ViolaBossLockGenerateRoomsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        ViolaBossLock.ForceIfNeeded(__instance);
    }
}

/// <summary>「首次遇到顺序」调整之后再过一次，防止原版把未见过的 BOSS 顶到前面覆盖掉我们的锁定。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.ApplyDiscoveryOrderModifications))]
internal static class ViolaBossLockDiscoveryOrderPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        ViolaBossLock.ForceIfNeeded(__instance);
    }
}

/// <summary>进阶 10 的双 BOSS 规则收尾：原版抽完两只之后再按规则决定薇欧拉位置和另一场遭遇。</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class ViolaBossLockDoubleBossPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        ViolaBossLock.ApplyDoubleBossRule();
    }
}

/// <summary>
/// 开局时把 RunState 记下来：第三幕房间是在 <c>RunManager.GenerateRooms</c> 里生成的，
/// 那时我们手上没有 Player 对象，而 <c>RunManager.State</c> 又不是公开成员，
/// 所以从这几个「开局/读档」入口把状态缓存起来，供房主遭遇模式的同步判定使用。
/// </summary>
[HarmonyPatch(typeof(RunManager))]
internal static class ViolaBossLockRunStatePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.SetUpNewSingleplayer))]
    private static void NewSingleplayerPrefix(RunState state)
    {
        AraleQuestRunData.CacheRunState(state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.SetUpNewMultiplayer))]
    private static void NewMultiplayerPrefix(RunState state)
    {
        AraleQuestRunData.CacheRunState(state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.SetUpSavedSingleplayer))]
    private static void SavedSingleplayerPrefix(RunState state)
    {
        AraleQuestRunData.CacheRunState(state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.SetUpSavedMultiplayer))]
    private static void SavedMultiplayerPrefix(RunState state)
    {
        AraleQuestRunData.CacheRunState(state);
    }
}

