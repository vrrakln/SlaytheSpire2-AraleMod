using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using Arale.Scripts.Quests;

namespace Arale.Scripts;

/// <summary>
/// 多人游戏：玩家在大厅里选定角色时，把自己的「任务书难度 + 任务随机种子」暂存并通过
/// RitsuLib 的贡献通道推给主机（<c>LobbyPlayerChangedCharacterMessage</c> 的尾部数据）。
///
/// <para>用 Prefix 是为了**赶在原版发送角色变更消息之前**写好暂存，
/// 这样 RitsuLib 推送贡献时带上的一定是最新值。单人游戏下这个大厅同样存在（只是没有别的玩家），
/// 写进去也无害；万一 RitsuLib 的暂存不可用，<see cref="AraleQuestRunData" /> 会退回本机设置。</para>
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetLocalCharacter))]
internal static class AraleQuestLobbyPatch
{
    [HarmonyPrefix]
    private static void Prefix(StartRunLobby __instance)
    {
        AraleQuestRunData.StageLocal(__instance);
    }
}
