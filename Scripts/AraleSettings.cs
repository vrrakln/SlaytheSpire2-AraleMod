using System;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Arale.Scripts;

public enum AraleViolaEncounterMode
{
    Disabled = 0,
    Random = 1,
    Locked = 2
}

/// <summary>任务书的难度（对应三套「难度配方」，见 <c>docs\团魂任务生成设计.md</c>）。</summary>
public enum AraleQuestDifficulty
{
    /// <summary>简单：T1 T1 T2 T2。</summary>
    Easy = 0,

    /// <summary>中等（默认）：T1 T2 T2 T3。</summary>
    Normal = 1,

    /// <summary>困难：T2 T2 T3 T3。</summary>
    Hard = 2
}

/// <summary>音效相关偏好（存档 <c>audio_settings.json</c>）。</summary>
public sealed class AraleAudioPreferences
{
    /// <summary>是否播放攻击牌音效（由 GPT 加入的开关）。</summary>
    public bool AttackSoundEnabled { get; set; } = true;

    /// <summary>魔法露露音乐的音量倍率（1.0 = 原来的音量）。</summary>
    public double LuluVolume { get; set; } = 1.0;
}

/// <summary>玩法相关偏好（存档 <c>gameplay_settings.json</c>）。</summary>
public sealed class AraleGameplayPreferences
{
    public bool IncludeAraleCardsForVanillaCharacters { get; set; } = true;
    /// <summary>Deepen non-Ancient Arale card descriptions; local visual preference only.</summary>
    public bool DarkCardText { get; set; } = true;
    /// <summary>任务书难度：0 简单 / 1 中等 / 2 困难。</summary>
    public int QuestDifficulty { get; set; } = (int)AraleQuestDifficulty.Normal;

    /// <summary>是否把第三幕 BOSS 锁定为「薇欧拉」（默认关闭）。</summary>
    public bool LockViolaBoss { get; set; }

    /// <summary>Null migrates the old boolean without changing existing preferences.</summary>
    public int? ViolaEncounterMode { get; set; }

    public AraleViolaEncounterMode ResolveViolaMode() => ViolaEncounterMode is int mode
        ? (AraleViolaEncounterMode)Math.Clamp(mode, 0, 2)
        : LockViolaBoss ? AraleViolaEncounterMode.Locked : AraleViolaEncounterMode.Random;
}

/// <summary>
/// 模组配置页面（主菜单 / 局内暂停界面里的「阿拉蕾」页）：音效 + 玩法。
///
/// <para>注意：同一个 mod 只调用一次 <c>RitsuLibFramework.RegisterModSettings</c>，两段设置都挂在同一个页面上
/// （分成「音效」「玩法」两节）。</para>
/// </summary>
public static class AraleSettings
{
    private const string AudioKey = "audio_settings";

    private const string GameplayKey = "gameplay_settings";

    /// <summary>魔法露露音量滑条的范围与步长（1.0 = 原音量）。</summary>
    public const double LuluVolumeMax = 1.5;

    public const double LuluVolumeStep = 0.05;

    private static readonly ModSettingsValueBinding<AraleAudioPreferences, bool> AttackBinding = new(
        Entry.ModId, AudioKey, SaveScope.Global,
        static settings => settings.AttackSoundEnabled,
        static (settings, value) => settings.AttackSoundEnabled = value);

    private static readonly ModSettingsValueBinding<AraleAudioPreferences, double> LuluVolumeBinding = new(
        Entry.ModId, AudioKey, SaveScope.Global,
        static settings => Math.Clamp(settings.LuluVolume, 0.0, LuluVolumeMax),
        static (settings, value) => settings.LuluVolume = Math.Clamp(value, 0.0, LuluVolumeMax));

    private static readonly ModSettingsValueBinding<AraleGameplayPreferences, AraleQuestDifficulty> QuestDifficultyBinding = new(
        Entry.ModId, GameplayKey, SaveScope.Global,
        static settings => (AraleQuestDifficulty)Math.Clamp(settings.QuestDifficulty, 0, 2),
        static (settings, value) => settings.QuestDifficulty = Math.Clamp((int)value, 0, 2));

    private static readonly ModSettingsValueBinding<AraleGameplayPreferences, AraleViolaEncounterMode> ViolaModeBinding = new(
        Entry.ModId, GameplayKey, SaveScope.Global,
        static settings => settings.ResolveViolaMode(),
        static (settings, value) =>
        {
            settings.ViolaEncounterMode = Math.Clamp((int)value, 0, 2);
            settings.LockViolaBoss = value == AraleViolaEncounterMode.Locked;
        });

    public static bool AttackSoundEnabled => AttackBinding.Read();

    private static readonly ModSettingsValueBinding<AraleGameplayPreferences, bool> CardPoolBinding = new(
        Entry.ModId, GameplayKey, SaveScope.Global,
        static settings => settings.IncludeAraleCardsForVanillaCharacters,
        static (settings, value) => settings.IncludeAraleCardsForVanillaCharacters = value);

    public static bool IncludeAraleCardsForVanillaCharacters => CardPoolBinding.Read();

    private static readonly ModSettingsValueBinding<AraleGameplayPreferences, bool> DarkTextBinding = new(
        Entry.ModId, GameplayKey, SaveScope.Global,
        static settings => settings.DarkCardText,
        static (settings, value) =>
        {
            settings.DarkCardText = value;
            Cards.AraleCardTextStyle.RefreshVisibleCards(value);
        });

    public static bool DarkCardText => DarkTextBinding.Read();

    /// <summary>魔法露露音乐的音量倍率（0 ~ 1.5）。</summary>
    public static double LuluVolume => LuluVolumeBinding.Read();

    /// <summary>本机玩家在配置里选的任务书难度。</summary>
    public static AraleQuestDifficulty QuestDifficulty => QuestDifficultyBinding.Read();

    /// <summary>本机玩家是否开启「锁定第三幕 BOSS 为薇欧拉」。</summary>
    public static bool LockViolaBoss => ViolaMode == AraleViolaEncounterMode.Locked;
    public static AraleViolaEncounterMode ViolaMode => ViolaModeBinding.Read();

    public static void Register()
    {
        ModDataStore.For(Entry.ModId).Register<AraleAudioPreferences>(
            key: AudioKey, fileName: "audio_settings.json", scope: SaveScope.Global,
            defaultFactory: static () => new AraleAudioPreferences(), autoCreateIfMissing: true);

        ModDataStore.For(Entry.ModId).Register<AraleGameplayPreferences>(
            key: GameplayKey, fileName: "gameplay_settings.json", scope: SaveScope.Global,
            defaultFactory: static () => new AraleGameplayPreferences(), autoCreateIfMissing: true);

        try
        {
            RitsuLibFramework.RegisterModSettings(Entry.ModId, BuildPage);
        }
        catch (Exception ex)
        {
            // 配置页注册失败不能影响 mod 本体：设置读值都有默认值，功能照常
            Entry.Logger.Warn("Arale mod settings page registration failed: " + ex);
        }
    }

    private static void BuildPage(ModSettingsPageBuilder page)
    {
        page
            .WithTitle(Text("阿拉蕾", "Arale"))
            .WithModDisplayName(Text("阿拉蕾", "Arale"))
            .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("appearance", section => section
                .WithTitle(ModSettingsText.Dynamic(() => IsEnglish ? "Appearance" : "外观"))
                .AddToggle("dark_card_text",
                    ModSettingsText.Dynamic(() => IsEnglish ? "Darker card text" : "加深卡牌文字"), DarkTextBinding,
                    ModSettingsText.Dynamic(() => IsEnglish
                        ? "Off: original text. On: dark text for Arale cards. Ancient cards are always unchanged."
                        : "关闭：保持原版文字；开启：使用深色文字。先古卡的字体和底色始终不受影响。")))
            .AddSection("audio", section => section
                .WithTitle(Text("音效", "Audio"))
                .AddToggle("attack_sound", Text("开启攻击牌音效", "Enable Attack card sound effects"), AttackBinding)
                .AddSlider(
                    "lulu_volume",
                    Text("魔法露露音量", "Magic Lulu volume"),
                    LuluVolumeBinding,
                    0.0,
                    LuluVolumeMax,
                    LuluVolumeStep,
                    static value => $"{value * 100.0:0}%",
                    Text("阿拉蕾进入魔法露露状态时播放的音乐音量（100% = 默认）。", "Music volume when Arale enters Magic Lulu (100% = default).")))
            .AddSection("gameplay", section => section
                .WithTitle(Text("玩法", "Gameplay"))
                .AddToggle("arale_cards_for_vanilla",
                    Text("阿拉蕾参与原版角色的跨职业卡池", "Include Arale in vanilla characters' cross-character card pools"),
                    CardPoolBinding,
                    Text("开启：飞溅、万花筒等跨职业抽选可以出现阿拉蕾职业牌；关闭：排除阿拉蕾职业牌。阿拉蕾自身卡池不受影响。多人游戏按获得卡牌的玩家自己的设置生效，新开局应用。",
                        "On: cross-character selections such as Splash and Kaleidoscope can include Arale cards. Off: exclude Arale's character card pool. Arale's own pool is unaffected. In multiplayer, each recipient uses their own setting. Applies to new runs."))
                .AddEnumChoice(
                    "quest_difficulty",
                    Text("任务书难度", "Quest difficulty"),
                    QuestDifficultyBinding,
                    static value => value switch
                    {
                        AraleQuestDifficulty.Easy => Text("简单", "Easy"),
                        AraleQuestDifficulty.Hard => Text("困难", "Hard"),
                        _ => Text("中等", "Normal")
                    },
                    Text("多人游戏时取所有阿拉蕾玩家中选择难度最高的一个。", "In multiplayer, uses the highest difficulty selected by any Arale player.")))
            .AddSection("boss", section => section
                .WithTitle(Text("BOSS", "Bosses"))
                .AddEnumChoice(
                    "viola_encounter_mode",
                    Text("薇欧拉遭遇", "Viola encounters"),
                    ViolaModeBinding,
                    static value => value switch
                    {
                        AraleViolaEncounterMode.Disabled => Text("关闭", "Off"),
                        AraleViolaEncounterMode.Locked => Text("锁定", "Guaranteed"),
                        _ => Text("随机", "Random")
                    },
                    Text("关闭：不再遭遇薇欧拉；随机：可能遭遇薇欧拉；锁定：必定遭遇薇欧拉。\n锁定时，进阶 10 的两场 BOSS 中，薇欧拉出现在第一场或第二场的概率各为 50%。\n多人游戏以房主设置为准。仅影响新一局，已生成的遭遇不变。",
                        "Off: Viola will not appear. Random: Viola may appear. Guaranteed: Viola will always appear.\nAt Ascension 10, Guaranteed gives Viola a 50% chance of being the first or second boss.\nMultiplayer uses the host's setting. Applies to new runs; existing encounters remain unchanged.")));
    }

    private static bool IsEnglish => MegaCrit.Sts2.Core.Localization.LocManager.Instance?.Language == "eng";
    private static ModSettingsText Text(string chinese, string english) =>
        ModSettingsText.Dynamic(() => IsEnglish ? english : chinese);
}

