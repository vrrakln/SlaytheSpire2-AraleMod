using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Arale.Scripts.Cards;

/// <summary>
/// 美术资源路径解析：把图片按约定命名丢进目录即生效，缺省时自动回退到原版素材。
/// 约定（工程内 → pck 内路径）：
/// <list type="bullet">
///   <item>卡图：<c>arale_mod\images\cards\{类名}.png</c>（普通卡 1000×760，先古卡 606×852）</item>
///   <item>卡框：<c>arale_mod\images\card_frames\{attack|skill|power}.png</c>（598×844），先古卡用 <c>ancient.png</c>（618×862）</item>
///   <item>卡图边框：<c>arale_mod\images\card_frames\{attack|skill|power}_border.png</c>（551×420）</item>
///   <item>标题横幅：<c>arale_mod\images\card_frames\banner.png</c>（653×145，会被稀有度配色渲染）</item>
/// </list>
/// </summary>
public static class AraleArt
{
    public const string CardDir = "res://arale_mod/images/cards/";

    public const string FrameDir = "res://arale_mod/images/card_frames/";

    /// <summary>卡图缺失时的占位图（原版铁甲战士「打击」）。</summary>
    public const string PlaceholderPortrait = "res://images/packed/card_portraits/ironclad/strike_ironclad.png";

    public static string PortraitFor(string className)
    {
        string path = CardDir + className + ".png";
        return ResourceLoader.Exists(path) ? path : PlaceholderPortrait;
    }

    /// <summary>卡框：先古稀有度用 ancient.png，其余按卡牌类型取 attack/skill/power.png。</summary>
    public static string? FrameFor(CardRarity rarity, CardType type)
    {
        if (rarity == CardRarity.Ancient && Exists(FrameDir + "ancient.png") is not null)
        {
            return FrameDir + "ancient.png";
        }
        return Exists(FrameDir + TypeStem(type) + ".png");
    }

    /// <summary>卡图外圈边框（攻击/技能/能力 各不相同）。</summary>
    public static string? PortraitBorderFor(CardType type)
    {
        return Exists(FrameDir + TypeStem(type) + "_border.png");
    }

    /// <summary>标题横幅（贴图共用，配色由稀有度决定）。</summary>
    public static string? Banner()
    {
        return Exists(FrameDir + "banner.png");
    }

    /// <summary>只柔化普通卡的纸面；先古卡保持原有资源与材质。</summary>
    public static string? PaperMaterialFor(CardRarity rarity, CardType type, bool colorless) =>
        rarity == CardRarity.Ancient ? null :
        $"res://arale_mod/materials/arale_paper_{TypeStem(type)}{(colorless ? "_colorless" : "")}.tres";

    public static string TypeStem(CardType type)
    {
        return type switch
        {
            CardType.Attack => "attack",
            CardType.Power => "power",
            _ => "skill"
        };
    }

    private static string? Exists(string path)
    {
        return ResourceLoader.Exists(path) ? path : null;
    }
}
