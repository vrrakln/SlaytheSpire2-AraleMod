using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Data.Models;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;
using Arale.Scripts.Cards;
using Arale.Scripts.Combat;

namespace Arale.Scripts;

/// <summary>
/// 新角色「阿拉蕾」。
/// 使用已审查的阿拉蕾角色资源；尚未替换的字段由占位角色补全。
/// </summary>
[RegisterCharacter]
public sealed class Arale : ModCharacterTemplate<AraleCardPool, AraleRelicPool, AralePotionPool>
{
    /// <summary>缺失资源的占位角色：铁甲战士。</summary>
    public override string PlaceholderCharacterId => "ironclad";

    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(),
        new(Scenes: new(
            VisualsPath: AraleCombatSprite.ScenePath,
            MerchantAnimPath: "res://arale_mod/scenes/arale_merchant.tscn",
            RestSiteAnimPath: "res://arale_mod/scenes/arale_rest_site.tscn"
        ), Ui: new(
            IconTexturePath: "res://arale_mod/images/avatar/avatar.png",
            IconOutlineTexturePath: "res://arale_mod/images/avatar/avatar_outline.png",
            IconPath: "res://arale_mod/scenes/arale_icon.tscn",
            MapMarkerPath: "res://arale_mod/images/avatar/avatar.png",
            CharacterSelectBgPath: "res://arale_mod/scenes/character_select_background.tscn",
            CharacterSelectIconPath: "res://arale_mod/images/character_select/Avatar.png",
            CharacterSelectLockedIconPath: "res://arale_mod/images/character_select/Avatar.png",
            CharacterSelectTransitionPath: "res://arale_mod/vfx/transition.tres"
        ), Multiplayer: new(
            ArmPointingTexturePath: "res://arale_mod/images/hands/point.png",
            ArmRockTexturePath: "res://arale_mod/images/hands/rock.png",
            ArmPaperTexturePath: "res://arale_mod/images/hands/paper.png",
            ArmScissorsTexturePath: "res://arale_mod/images/hands/scissors.png"
        ), Vfx: new(TrailPath: "res://arale_mod/vfx/card_trail_arale.tscn")));

    public override Color NameColor => new Color("CB282B");

    public override Color EnergyLabelOutlineColor => new Color("594016FF");

    // 地图绘线由角色颜色直接着色，与阿拉蕾的亮黄色能量主题一致。
    public override Color MapDrawingColor => new Color("FFD85A");

    public override CharacterGender Gender => CharacterGender.Feminine;

    public override int StartingHp => 75;

    public override int StartingGold => 99;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    /// <summary>v1 暂不需要时间线小故事。</summary>
    public override bool RequiresEpochAndTimeline => false;

    /// <summary>
    /// 初始牌组（顺序即牌组界面的排列顺序）：
    /// 4 张打击 → 4 张防御 → 梦的开始 → 逐梦。
    ///
    /// 注意：RitsuLib 的 <c>[RegisterCharacterStarterCard]</c> 是**追加**在 <c>LocalStartingDeck</c> 之后的，
    /// 而注册表按类名的字母序处理，会得到「防御在打击前、逐梦在梦的开始前」的顺序，
    /// 因此这里直接像原版角色那样显式声明整个牌组（打击/防御上的注册特性已删除，避免重复）。
    /// </summary>
    protected override IEnumerable<CardModel> LocalStartingDeck => new CardModel[]
    {
        ModelDb.Card<AraleStrike>(),
        ModelDb.Card<AraleStrike>(),
        ModelDb.Card<AraleStrike>(),
        ModelDb.Card<AraleStrike>(),
        ModelDb.Card<AraleDefend>(),
        ModelDb.Card<AraleDefend>(),
        ModelDb.Card<AraleDefend>(),
        ModelDb.Card<AraleDefend>(),
        ModelDb.Card<DreamStart>(),
        ModelDb.Card<ChaseDream>()
    };

    /// <summary>逐帧战斗形象；保留 RitsuLib 的 NCreatureVisuals 场景转换。</summary>
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AraleCombatSprite.ScenePath);
    }

    /// <summary>建筑师战的五种特效；SceneHelper 自动添加场景前缀与扩展名。</summary>
    public override List<string> GetArchitectAttackVfx()
    {
        return new List<string>
        {
            "arale_mod/vfx/star_punch",
            "arale_mod/vfx/voice_rings",
            "arale_mod/vfx/twin_arc",
            "arale_mod/vfx/dream_shower",
            "arale_mod/vfx/cat_resonance"
        };
    }
}
