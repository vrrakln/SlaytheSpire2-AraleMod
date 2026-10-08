using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts;

// =====================================================================================
//  阿拉蕾专属遗物（除初始遗物外）
//  设计文档：docs\阿拉蕾专属遗物.md
//  图标：独立的小图、大图和轮廓资源。
// =====================================================================================

/// <summary>阿拉蕾专属遗物图标路径。</summary>
internal static class AraleRelicArt
{
    private const string Root = "res://arale_mod/images/relics/";
    public static string Icon(string id) => Root + id + ".png";
    public static string Outline(string id) => Root + "outlines/" + id + ".png";
    public static string Big(string id) => Root + "big/" + id + ".png";
}

/// <summary>保护腕带（罕见）：每当你锻炼时，获得 2 点格挡。（参考亡灵契约师遗物「骨笛」）</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleWristbandRelic : AraleRelicModel, IAraleTrainingListener
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleWristbandRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleWristbandRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleWristbandRelic));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(2m, ValueProp.Unpowered) };

    public async Task OnAraleTrain(PlayerChoiceContext choiceContext, Player owner, decimal amount)
    {
        if (owner != Owner)
        {
            return;
        }
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }
}

/// <summary>麦克风（罕见）：每当你施加歌声时，额外施加 1 层。（参考静默猎手遗物「异蛇头骨」）</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleMicrophoneRelic : AraleRelicModel, IAraleSongStackBonus
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleMicrophoneRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleMicrophoneRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleMicrophoneRelic));

    /// <summary>额外层数：加在「本次施加量」上一起参与每 5 层转化陶醉的结算。</summary>
    public decimal ExtraSongStacks => 1m;
}

/// <summary>能量棒（普通）：每场战斗的前 2 张攻击牌不会消耗梦想。</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleEnergyBarRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleEnergyBarRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleEnergyBarRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleEnergyBarRelic));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("FreeAttacks", 2m) };

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<AraleFreeAttackDreamPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars["FreeAttacks"].BaseValue,
            Owner.Creature,
            null);
    }
}

/// <summary>收藏贴纸（稀有）：每当你进入魔法露露状态时，抽 3 张牌。</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleStickerRelic : AraleRelicModel, IAraleMagicLuluListener
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleStickerRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleStickerRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleStickerRelic));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(3) };

    public async Task OnAraleMagicLuluEntered(PlayerChoiceContext choiceContext, Player owner, CardModel? source)
    {
        if (owner != Owner)
        {
            return;
        }
        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }
}

/// <summary>红豆面包（稀有）：当你的梦想点数归 0 时，立即获得 2 点梦想。</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleRedBeanBreadRelic : AraleRelicModel, IAraleDreamZeroListener
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleRedBeanBreadRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleRedBeanBreadRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleRedBeanBreadRelic));

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 2m) };

    public async Task OnAraleDreamReachedZero(PlayerChoiceContext choiceContext, Player owner)
    {
        if (owner != Owner)
        {
            return;
        }
        Flash();
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, null);
    }
}

/// <summary>
/// 安可曲目（稀有）：敌人死亡时，它身上的歌声和陶醉层数会传递给所有敌人。
/// 参考亡灵契约师的卡牌「苦难」（Misery：把目标身上的负面状态复制给其他敌人）。
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleEncoreRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleEncoreRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleEncoreRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleEncoreRelic));

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature == null || !creature.IsDead)
        {
            return;
        }
        Creature me = Owner.Creature;
        if (creature.Side == me.Side || me.CombatState == null)
        {
            return;
        }
        // 原版在派发 AfterDeath 之后才清空该生物的能力，因此这里仍能读到它身上的层数
        decimal song = AraleCmd.Song(creature);
        decimal intoxicated = creature.GetPower<AraleIntoxicatedPower>()?.Amount ?? 0m;
        if (song <= 0m && intoxicated <= 0m)
        {
            return;
        }
        var enemies = me.CombatState.HittableEnemies?
            .Where(e => e != null && e != creature && e.IsAlive).ToList();
        if (enemies == null || enemies.Count == 0)
        {
            return;
        }
        Flash();
        foreach (Creature enemy in enemies)
        {
            if (song > 0m)
            {
                // 走统一的施加入口：仍受「每 5 层歌声转化为 1 层陶醉」的规则约束
                await AraleCmd.ApplySong(choiceContext, enemy, song, Owner, null);
            }
            if (intoxicated > 0m)
            {
                await PowerCmd.Apply<AraleIntoxicatedPower>(choiceContext, enemy, intoxicated, me, null);
            }
        }
    }
}

/// <summary>
/// 阿拉蕾的书包（商店）：拾起时，选择你牌组里的 2 张牌，它们获得保留。
/// 实现方式参考遗物「王室印章」+「虚无种子」：用附魔（enchantment）实现对牌组卡牌的永久修改
/// —— 附魔会随存档保存，读档时由 EnchantmentModel.ModifyCard() 重新施加关键词，因此不会丢。
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleBackpackRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleBackpackRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleBackpackRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleBackpackRelic));

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2) };

    public override async Task AfterObtained()
    {
        var retain = ModelDb.Enchantment<AraleRetain>();
        List<CardModel> candidates = PileType.Deck.GetPile(Owner).Cards
            .Where(c => c != null && retain.CanEnchant(c)).ToList();
        if (candidates.Count == 0)
        {
            return;
        }
        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue);
        foreach (CardModel card in await CardSelectCmd.FromDeckGeneric(Owner, prefs, c => retain.CanEnchant(c)))
        {
            CardCmd.Enchant<AraleRetain>(card, 1m);
        }
    }
}
