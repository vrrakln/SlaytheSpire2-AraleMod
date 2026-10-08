using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace Arale.Scripts.Boss;

/// <summary>
/// 第三幕（<c>Glory</c>）的 BOSS 遭遇「薇欧拉」。
///
/// <para>RitsuLib 的 <see cref="RegisterActEncounterAttribute" /> 会把这个遭遇**追加**到 Glory 的
/// <c>GenerateAllEncounters()</c> 结果里，于是它自动进入 <c>AllBossEncounters</c>，
/// 被抽为第三幕 BOSS 的概率变成 1/4（原版 3 个 + 我们 1 个）。</para>
///
/// <para><b>★美术占位★</b>：BOSS 地图节点与「顶部 BOSS 图标 / 运行历史图标」都用本 mod 自带的占位图
/// （`arale_mod\images\map\viola_boss_icon(.|_outline).png` 与 `arale_mod\images\ui\run_history\viola_boss(.|_outline).png`，
/// 目前是阿拉蕾头像的拷贝），等正式美术接入后替换文件即可。</para>
///
/// <para><b>⚠ 这两组图标必须真实存在</b>：原版 <c>PreloadManager.Cache.GetAsset()</c> 对**不存在的路径会直接抛
/// <c>AssetLoadException</c>**（见 <c>AssetCache.LoadAsset</c>），而地图节点（<c>NBossMapPoint._Ready</c>）与
/// 顶部 BOSS 图标（<c>NTopBarBossIcon.OnActEntered</c>）都是**直接**用这些路径取贴图 ——
/// 路径不存在就会在进入该幕时抛异常，表现为**黑屏**（`9/15` 修的正是这个 bug）。
/// 其中「顶部 BOSS 图标 / 运行历史图标」走的是原版按内容 ID 拼出来的路径，mod 内容必须用
/// <see cref="ViolaBossArt" /> 注册 RitsuLib 的路径覆盖，否则同样会指向不存在的文件。</para>
/// </summary>
[RegisterActEncounter(typeof(MegaCrit.Sts2.Core.Models.Acts.Glory))]
public sealed class ViolaBoss : EncounterModel
{
    /// <summary>薇欧拉的站位。</summary>
    public const string ViolaSlot = "viola";

    /// <summary>BOSS 地图节点贴图的基路径（实际取 `<c>基路径 + ".png"</c>` 与 `<c>+ "_outline.png"</c>`）。</summary>
    public const string BossNodeArtBase = "res://arale_mod/images/map/viola_boss_icon";

    /// <summary>顶部 BOSS 图标 / 运行历史图标的基路径（同上）。</summary>
    public const string RunHistoryIconArtBase = "res://arale_mod/images/ui/run_history/viola_boss";

    public override RoomType RoomType => RoomType.Boss;
    public override bool HasScene => true;
    protected override bool HasCustomBackground => true;
    public const string MusicEvent = "event:/arale_mod/music/viola_boss";
    public override string CustomBgm => MusicEvent;
    public override IEnumerable<string> ExtraAssetPaths => new[]
    {
        ModelDb.Card<Rumor>().OverlayPath,
        ModelDb.Card<Bleak>().OverlayPath
    };

    /// <summary>藤都子在左且先行动；原版按此列表排序敌方行动。</summary>
    public override IReadOnlyList<string> Slots => new[] { CharmedFujiko.SlotName, ViolaSlot };

    /// <summary>★占位美术★（必须存在，见类注释）。</summary>
    public override string BossNodePath => BossNodeArtBase;

    public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[] { ModelDb.Monster<Viola>(), ModelDb.Monster<CharmedFujiko>() };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new[] { (ModelDb.Monster<Viola>().ToMutable(), (string?)ViolaSlot) };
    }

    public override float GetCameraScaling()
    {
        return 0.9f;
    }

    public override Vector2 GetCameraOffset()
    {
        return Vector2.Down * 60f;
    }
}

/// <summary>
/// 把「顶部 BOSS 图标 / 运行历史图标」的路径指向本 mod 自带的占位图。
///
/// <para>原版是按内容 ID 拼路径：<c>res://images/ui/run_history/&lt;id小写&gt;.png</c>（见
/// <c>ImageHelper.GetRoomIconPath</c>），mod 内容的这个文件并不存在，而取贴图的地方
/// （<c>NTopBarBossIcon.RefreshBossIcon</c>）用的是会**抛异常**的 <c>Cache.GetTexture2D</c>，
/// 于是进入该幕直接黑屏。RitsuLib 提供了路径覆盖入口，这里把薇欧拉的两条路径换成我们自己的文件。</para>
/// </summary>
public static class ViolaBossArt
{
    private static bool _registered;

    /// <summary>在 mod 初始化时调用一次。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }


        ExternalAssetOverrideRegistry.RegisterEncounterRunHistoryIconPathProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? ViolaBoss.RunHistoryIconArtBase + ".png" : null!);
        ExternalAssetOverrideRegistry.RegisterEncounterRunHistoryIconOutlinePathProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? ViolaBoss.RunHistoryIconArtBase + "_outline.png" : null!);
        ExternalAssetOverrideRegistry.RegisterEncounterBossNodePathProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? ViolaBoss.BossNodeArtBase : null!);
        ExternalAssetOverrideRegistry.RegisterEncounterScenePathProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? "res://scenes/encounters/arale_mod_encounter_viola_boss.tscn" : null!);
        ExternalAssetOverrideRegistry.RegisterEncounterBackgroundScenePathProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? "res://scenes/backgrounds/queen_boss/queen_boss_background.tscn" : null!);
        ExternalAssetOverrideRegistry.RegisterEncounterBackgroundLayersDirectoryProvider(Entry.ModId,
            encounter => encounter is ViolaBoss ? "res://scenes/backgrounds/queen_boss/layers" : null!);
        _registered = true;
    }
}

