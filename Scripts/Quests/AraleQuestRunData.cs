using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace Arale.Scripts.Quests;

/// <summary>
/// 每名玩家在局内的任务书数据（RitsuLib run data 槽位）。
/// <c>PlayerRunSavedData&lt;T&gt;</c> 要求 T 是「有公共无参构造函数的引用类型」，所以用一个模型类装两个值。
/// </summary>
public sealed class AraleQuestRunSlot
{
    /// <summary>该玩家在配置里选的任务难度（0 简单 / 1 中等 / 2 困难）。</summary>
    public int Difficulty { get; set; }

    /// <summary>该玩家的任务随机种子（十进制字符串；与游戏种子无关）。</summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>该玩家是否开启「锁定第三幕 BOSS 为薇欧拉」（0/1）。</summary>
    public int LockViolaBoss { get; set; }
    public int? ViolaEncounterMode { get; set; }
    public bool IsBossSettingsHost { get; set; }
    public bool IncludeAraleCardsForVanillaCharacters { get; set; } = true;
}

/// <summary>
/// 任务书的「局内共享数据」：**难度**与**每名玩家自己的随机种子**。
///
/// <para><b>为什么要走 RitsuLib 的 run data</b>：
/// 难度要按「所有阿拉蕾玩家里选得最高的那个」来定，而玩家的配置是**本机**的；
/// 任务内容又要求「和种子编号无关的独立随机事件」。两者都必须在各客户端上算出**同样的结果**
/// （否则各端给同一个玩家发的团魂奖励会不一致）。
/// RitsuLib 的 <see cref="PlayerRunSavedData{T}" /> 支持「大厅暂存 → 开局提交」的多人同步：
/// 每名玩家在大厅选角色时，把自己的「难度 + 随机种子」推给主机，
/// 开局后所有客户端都能读到同一份数据 —— 于是：
/// 难度 = 所有阿拉蕾玩家里的最大值 ✓；任务内容 = 各自随身携带的随机种子（与游戏种子无关）✓。</para>
///
/// <para><b>降级策略</b>：如果大厅暂存没读到（单人、或平台/版本差异导致同步没生效），
/// 就退回「本机设置 + 本机随机」，功能照常，只是多人下各端内容可能不同。</para>
/// </summary>
public static class AraleQuestRunData
{
    private const string Slot = "quest_book";

    private static PlayerRunSavedData<AraleQuestRunSlot>? _data;

    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        try
        {
            var options = new RunSavedDataOptions
            {
                SchemaVersion = 1,
                WritePolicy = RunSavedDataWritePolicy.WhenSet,
                SyncLobbyOnChange = true
            };
            _data = RunSavedDataStore.For(Entry.ModId)
                .RegisterPerPlayer<AraleQuestRunSlot>(Slot, static () => new AraleQuestRunSlot(), options);
            _registered = true;
        }
        catch (Exception ex)
        {
            // 注册失败不能让 mod 初始化炸掉：任务书会自动退回「本机设置 + 本机随机」
            Entry.Logger.Warn("Arale quest run data registration failed: " + ex.Message);
        }
    }

    /// <summary>在大厅里同步任务难度、随机种子、薇欧拉遭遇模式和房主身份。</summary>
    public static void StageLocal(StartRunLobby? lobby)
    {
        if (lobby == null || _data == null)
        {
            return;
        }

        // StartRunLobbyPlayer 是 struct：没找到时 LocalPlayer 返回默认值（character 为 null）
        var me = lobby.LocalPlayer;
        if (me.character == null)
        {
            return;
        }

        try
        {
            _data.Lobby.Set(lobby, me.id, new AraleQuestRunSlot
            {
                Difficulty = (int)AraleSettings.QuestDifficulty,
                Seed = Rng.Chaotic.NextUnsignedLong().ToString(CultureInfo.InvariantCulture),
                LockViolaBoss = AraleSettings.LockViolaBoss ? 1 : 0,
                ViolaEncounterMode = (int)AraleSettings.ViolaMode,
                IsBossSettingsHost = lobby.NetService.Type != NetGameType.Client,
                IncludeAraleCardsForVanillaCharacters = AraleSettings.IncludeAraleCardsForVanillaCharacters
            });
        }
        catch (Exception ex)
        {
            // 大厅暂存失败不能影响开局
            Entry.Logger.Warn("Arale quest lobby staging failed: " + ex.Message);
        }
    }

    /// <summary>Only the host's synchronized contribution controls multiplayer boss generation.</summary>
    public static AraleViolaEncounterMode EffectiveViolaMode(AraleViolaEncounterMode localDefault)
    {
        var players = _cachedRunState?.Players;
        if (players == null || players.Count <= 1)
            return localDefault;
        return ResolveHostViolaMode(players.Select(Read));
    }

    public static bool IncludeAraleCardsFor(Player player)
    {
        // Never consult the observing client's preference for a multiplayer recipient.
        if (player.RunState.Players.Count > 1)
            return Read(player)?.IncludeAraleCardsForVanillaCharacters ?? true;
        return AraleSettings.IncludeAraleCardsForVanillaCharacters;
    }

    internal static AraleViolaEncounterMode ResolveHostViolaMode(IEnumerable<AraleQuestRunSlot?> slots)
    {
        var host = slots.FirstOrDefault(slot => slot?.IsBossSettingsHost == true);
        // Missing/legacy contributions must resolve identically on all peers, never to local settings.
        if (host == null) return AraleViolaEncounterMode.Random;
        return host.ViolaEncounterMode is int mode
            ? (AraleViolaEncounterMode)Math.Clamp(mode, 0, 2)
            : host.LockViolaBoss != 0 ? AraleViolaEncounterMode.Locked : AraleViolaEncounterMode.Random;
    }

    /// <summary>
    /// 记下当前这一局的 RunState（由 <c>ViolaBossLock</c> 在开局时打补丁传入）。
    /// 用它是因为 <c>RunManager.State</c> 不是公开成员，而开局时我们拿不到 Player 对象。
    /// </summary>
    public static void CacheRunState(IRunState? state)
    {
        _cachedRunState = state;
    }

    private static IRunState? _cachedRunState;

    /// <summary>本局缓存的 RunState（没有则 null）。给需要 <c>Acts</c> / <c>Rng</c> 的补丁用。</summary>
    public static IRunState? CachedRunState => _cachedRunState;

    /// <summary>本局的**最后一幕**（薇欧拉所在第三幕；双 BOSS 的第二只在最后一幕生成）。</summary>
    public static ActModel? LastAct
    {
        get
        {
            IReadOnlyList<ActModel>? acts = _cachedRunState?.Acts;
            return acts != null && acts.Count > 0 ? acts[acts.Count - 1] : null;
        }
    }

    /// <summary>
    /// 本局实际使用的任务难度 = 所有**阿拉蕾玩家**里选得最高的那个（含本机设置）。
    /// </summary>
    public static AraleQuestDifficulty EffectiveDifficulty(Player owner)
    {
        int best = (int)AraleSettings.QuestDifficulty;
        var players = owner?.RunState?.Players;
        if (players != null && _data != null)
        {
            foreach (Player player in players)
            {
                if (player?.Character is not global::Arale.Scripts.Arale)
                {
                    continue;
                }

                int staged = Read(player)?.Difficulty ?? 0;
                if (staged > best)
                {
                    best = staged;
                }
            }
        }

        return (AraleQuestDifficulty)Math.Clamp(best, 0, 2);
    }

    /// <summary>
    /// 本机玩家的任务随机种子（与游戏种子无关，由大厅暂存同步给所有人）。
    /// 返回 0 表示没有同步到，调用方自行用一次性随机数兜底。
    /// </summary>
    public static ulong SeedFor(Player player)
    {
        string raw = Read(player)?.Seed ?? string.Empty;
        return ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value) ? value : 0UL;
    }

    private static AraleQuestRunSlot? Read(Player? player)
    {
        if (player == null || _data == null)
        {
            return null;
        }

        try
        {
            return _data.Get(player);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
