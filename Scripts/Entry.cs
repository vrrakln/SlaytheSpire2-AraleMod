using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using Arale.Scripts.Quests;

namespace Arale.Scripts;

// 必须要加的属性，用于注册Mod。字符串和初始化函数命名一致。
[ModInitializer(nameof(Init))]
public class Entry
{
    // 内容注册用的 modId：必须与 mod 清单 arale_mod.json 的 "id" 一致。
    // RitsuLib 用它生成内容 ID 前缀：ARALE_MOD_CARD_XXX / ARALE_MOD_CHARACTER_ARALE 等，
    // pck 内的资源与本地化目录同样是 arale_mod/。
    public const string ModId = "arale_mod";

    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 初始化函数
    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // 启用 RitsuLib 自动注册：这样 [RegisterCard] / [RegisterCharacter] 等 attribute 才会生效
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        AraleSettings.Register();
        AraleQuestRunData.Register();
        Boss.ViolaBossArt.Register();
        new Harmony(ModId + ".card_art").PatchAll(assembly);
        Log.Info("Arale mod initialized!");
    }
}