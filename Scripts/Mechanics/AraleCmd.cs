using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Arale.Scripts.Cards;
using Arale.Scripts.Quests;

namespace Arale.Scripts.Mechanics;

/// <summary>
/// 阿拉蕾各项机制的统一入口：卡牌代码只调用这里的方法，规则集中在本文件维护。
/// 规则来源：docs\角色卡池.md
/// </summary>
public static class AraleCmd
{
    /// <summary>锻炼到该层数即进入魔法露露状态。</summary>
    public const int TrainingThreshold = 10;

    /// <summary>
    /// 该玩家实际需要的锻炼阈值（律的团魂已改为进入时抽牌）
    /// （走 <see cref="IAraleLuluThresholdModifier" />，机制代码不直接依赖具体遗物类）。
    /// </summary>
    public static int TrainingThresholdFor(Player player)
    {
        foreach (IAraleLuluThresholdModifier modifier in player.Relics.OfType<IAraleLuluThresholdModifier>().ToList())
        {
            if (modifier.LuluTrainingThreshold is int threshold && threshold > 0)
            {
                return threshold;
            }
        }
        return TrainingThreshold;
    }

    /// <summary>每累计该层数歌声转化为 1 层陶醉。</summary>
    public const int SongPerIntoxication = 5;

    /// <summary>每层陶醉让敌人失去的生命值（9/15 起为固定值，不再是最大生命的 10%）。</summary>
    public const decimal IntoxicatedHpLoss = 10m;

    public static decimal Dream(Creature creature)
    {
        return creature.GetPower<AraleDreamPower>()?.Amount ?? 0;
    }

    public static bool IsMagicLulu(Creature creature)
    {
        return creature.GetPower<AraleMagicLuluPower>() != null;
    }

    public static decimal Song(Creature? creature)
    {
        return creature?.GetPower<AraleSongPower>()?.Amount ?? 0;
    }

    public static decimal TotalSong(ICombatState? state)
    {
        var enemies = state?.HittableEnemies;
        return enemies == null ? 0m : enemies.Sum(Song);
    }

    public static decimal Training(Creature creature)
    {
        return creature.GetPower<AraleTrainingPower>()?.Amount ?? 0;
    }

    public static int RetainCardsInHand(Player player)
    {
        return player.PlayerCombatState?.Hand?.Cards?.Count(c => c.CanonicalKeywords.Contains(CardKeyword.Retain)) ?? 0;
    }

    /// <summary>获得梦想。</summary>
    public static async Task GainDream(PlayerChoiceContext choiceContext, Player player, decimal amount, CardModel? source = null)
    {
        if (amount <= 0m)
        {
            return;
        }
        await PowerCmd.Apply<AraleDreamPower>(choiceContext, player.Creature, amount, player.Creature, source);
        if (player.Creature.GetPower<AraleDreamGainedThisTurnPower>() == null)
        {
            await PowerCmd.Apply<AraleDreamGainedThisTurnPower>(choiceContext, player.Creature, 1m, player.Creature, source);
        }
        // 团魂任务：获得梦想（同时采样梦想峰值）
        AraleQuestTracker.NotifyDreamGained(player, amount);
    }

    /// <summary>本回合是否获得过梦想。</summary>
    public static bool GainedDreamThisTurn(Creature creature)
    {
        return creature.GetPower<AraleDreamGainedThisTurnPower>() != null;
    }

    /// <summary>失去（消耗）梦想，并触发所有「消耗梦想时」的联动能力。</summary>
    public static async Task LoseDream(PlayerChoiceContext choiceContext, Player player, decimal amount, CardModel? source = null)
    {
        if (amount <= 0m)
        {
            return;
        }
        var dream = player.Creature.GetPower<AraleDreamPower>();
        if (dream == null)
        {
            return;
        }
        decimal actual = Math.Min(dream.Amount, amount);
        if (actual <= 0m)
        {
            return;
        }
        int left = dream.Amount - (int)actual;
        if (left <= 0)
        {
            await PowerCmd.Remove(dream);
        }
        else
        {
            dream.SetAmount(left);
        }
        await NotifyDreamConsumed(choiceContext, player, actual, source);
        // 团魂任务：梦想被消耗（含"被消耗到 0"的次数）
        AraleQuestTracker.NotifyDreamConsumed(player, actual, left <= 0);
        // 遗物「红豆面包」：梦想归 0 时立即获得梦想（只在「刚好降到 0」时触发）
        if (left <= 0)
        {
            foreach (IAraleDreamZeroListener listener in player.Relics.OfType<IAraleDreamZeroListener>().ToList())
            {
                await listener.OnAraleDreamReachedZero(choiceContext, player);
            }
        }
    }

    /// <summary>失去全部梦想。</summary>
    public static async Task LoseAllDream(PlayerChoiceContext choiceContext, Player player, CardModel? source = null)
    {
        decimal amount = Dream(player.Creature);
        if (amount > 0m)
        {
            await LoseDream(choiceContext, player, amount, source);
        }
    }

    private static async Task NotifyDreamConsumed(PlayerChoiceContext choiceContext, Player player, decimal consumed, CardModel? source)
    {
        var creature = player.Creature;
        var dexterity = creature.GetPower<AraleDreamLoseDexterityPower>();
        if (dexterity != null)
        {
            await dexterity.HandleDreamConsumed(choiceContext, player, consumed, source);
        }
        var damage = creature.GetPower<AraleDreamConsumeDamagePower>();
        if (damage != null)
        {
            await damage.HandleDreamConsumed(choiceContext, player, consumed, source);
        }
        foreach (var energy in creature.Powers.OfType<AraleDreamConsumeEnergyPower>().ToArray())
        {
            await energy.HandleDreamConsumed(choiceContext, player, consumed, source);
        }
    }

    // 避免重复收到同一次出牌通知时重复消耗。
    // 以 CardPlay 身份去重，重放仍是独立出牌；弱引用不跨战斗保留牌对象。
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CardPlay, object> DreamSettled = new();

    /// <summary>有色攻击按规则消耗梦想；由乃的格挡加成不消耗梦想。</summary>
    public static async Task ConsumeDreamForAttack(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (DreamSettled.TryGetValue(cardPlay, out _)) return;
        DreamSettled.Add(cardPlay, new object());
        CardModel card = cardPlay.Card;
        Player player = cardPlay.Player;
        Creature creature = player.Creature;
        // 落幕已自行清空，不能再次扣掉红豆面包等效果返还的梦想。
        if (card is CurtainCall) return;

        bool attackConsumes = card.Type == CardType.Attack &&
            !(card.Pool?.IsColorless ?? false) && !IsMagicLulu(creature);
        if (attackConsumes)
        {
            // 使用同步战斗历史，包含无色/免费攻击；只免第 2 张，不是每隔 2 张。
            var second = CombatManager.Instance.History.CardPlaysStarted
                .Where(e => e.HappenedThisTurn(creature.CombatState) && e.CardPlay.Player == player && e.CardPlay.Card.Type == CardType.Attack)
                .Skip(1).FirstOrDefault()?.CardPlay;
            if (creature.GetPower<AraleFreeAttackDreamPower>() is { Amount: > 0 } free)
            {
                if (free.Amount == 1) await PowerCmd.Remove(free);
                else free.SetAmount(free.Amount - 1);
                attackConsumes = false;
            }
            else if (creature.HasPower<AraleThirdAttackFreePower>() && second == cardPlay)
            {
                attackConsumes = false;
            }
        }
        if (attackConsumes)
            await LoseDream(choiceContext, player, 1m, card);
    }
    /// <summary>给予歌声（含 5 层转陶醉的转化与联动）。</summary>
    public static async Task ApplySong(PlayerChoiceContext choiceContext, Creature target, decimal amount, Player owner, CardModel? source = null)
    {
        if (amount <= 0m || target == null)
        {
            return;
        }
        // 遗物「麦克风」：每当你施加歌声时额外施加层数。必须加在总量上，
        // 才能和「每 5 层歌声转化 1 层陶醉」的结算保持一致（否则会出现 5 层歌声）。
        amount += owner.Relics.OfType<IAraleSongStackBonus>().Sum(r => r.ExtraSongStacks);
        // 先经过原版状态施加流程，让人工制品等效果拦截本次新增歌声。
        // 新建 Power 被挡住时 Apply 仍可能返回未挂载的对象，故比较实际层数。
        decimal current = Song(target);
        await PowerCmd.Apply<AraleSongPower>(choiceContext, target, amount, owner.Creature, source);
        decimal sum = Song(target);
        amount = sum - current;
        if (amount <= 0m) return;

        // 本场战斗累计施加的歌声层数（序号 82「余音绕梁」用）
        var given = owner.Creature.GetPower<AraleSongGivenTotalPower>();
        if (given != null)
        {
            given.SetAmount(given.Amount + (int)amount);
        }
        else
        {
            await PowerCmd.Apply<AraleSongGivenTotalPower>(choiceContext, owner.Creature, amount, owner.Creature, source);
        }

        // 转化规则：每 5 层实际生效的歌声 -> 1 层陶醉；余数（0~4）留在敌人身上。
        // 注意必须按「已有层数 + 本次施加层数」整体计算：一次性施加 6 层应当是 1 层陶醉 + 1 层歌声。
        int intoxicated = (int)Math.Floor(sum / SongPerIntoxication);
        decimal remain = sum - intoxicated * SongPerIntoxication;

        var song = target.GetPower<AraleSongPower>();
        if (remain > 0m)
        {
            song!.SetAmount((int)remain);
        }
        else if (song != null)
        {
            await PowerCmd.Remove(song);
        }

        if (intoxicated > 0)
        {
            // 统一的陶醉施加入口（含每层固定生命损失、野乃花团魂的额外值）
            decimal hpLoss = await ApplyIntoxicated(choiceContext, target, intoxicated, owner, source);
            // 团魂任务：歌声累计施加量、产生的陶醉层数、陶醉造成的生命损失
            AraleQuestTracker.NotifySongApplied(owner, amount, intoxicated, hpLoss);
        }
        else
        {
            AraleQuestTracker.NotifySongApplied(owner, amount, 0, 0m);
        }

        var draw = owner.Creature.GetPower<AraleSongDrawPower>();
        if (draw != null)
        {
            await draw.HandleSongApplied(choiceContext, owner, source);
        }
    }

    /// <summary>
    /// 直接施加陶醉（走「正常」路径：每层固定失去生命、并触发野乃花团魂的额外值）。
    /// </summary>
    /// <returns>本次一共造成的生命损失（供任务统计用）。</returns>
    /// <remarks>
    /// 9/15 机制改动：陶醉从「每层失去 10% 最大生命」改成**每层固定失去 10 点生命**——
    /// 按百分比会在 BOSS 身上超模、在杂兵身上收益又过低，改成固定值后两者收益一致。
    /// BOSS「薇欧拉」的召唤物需要**静默**施加 99 层陶醉（不造成伤害），那种情况直接调
    /// <c>PowerCmd.Apply&lt;AraleIntoxicatedPower&gt;</c>，不走本方法。
    /// </remarks>
    public static async Task<decimal> ApplyIntoxicated(PlayerChoiceContext choiceContext, Creature target, decimal layers, Player owner, CardModel? source = null)
    {
        if (layers <= 0m || target == null)
        {
            return 0m;
        }

        decimal previous = target.GetPower<AraleIntoxicatedPower>()?.Amount ?? 0;
        await PowerCmd.Apply<AraleIntoxicatedPower>(choiceContext, target, layers, owner.Creature, source);
        layers = (target.GetPower<AraleIntoxicatedPower>()?.Amount ?? 0) - previous;
        if (layers <= 0m) return 0m;

        // 野乃花的团魂仅增强施加给敌人的陶醉，不能反噬自己或队友。
        var bonuses = target.Side == CombatSide.Enemy
            ? owner.Relics.OfType<IAraleIntoxicationBonus>().ToArray()
            : Array.Empty<IAraleIntoxicationBonus>();
        decimal extraFlat = bonuses.Sum(r => r.ExtraHpLoss);
        decimal extraWeak = bonuses.Sum(r => r.ExtraWeak);
        decimal hpLoss = (IntoxicatedHpLoss + extraFlat) * layers;
        await CreatureCmd.Damage(choiceContext, target, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, owner.Creature, source, null);
        if (extraWeak > 0m)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target, extraWeak, owner.Creature, source);
        }

        return hpLoss;
    }

    /// <summary>给所有敌人施加歌声。</summary>
    public static async Task ApplySongToAll(PlayerChoiceContext choiceContext, Player owner, decimal amount, CardModel? source = null)
    {
        var enemies = owner.Creature.CombatState?.HittableEnemies?.ToList();
        if (enemies == null)
        {
            return;
        }
        foreach (var enemy in enemies)
        {
            await ApplySong(choiceContext, enemy, amount, owner, source);
        }
    }

    /// <summary>锻炼（含翻倍与进入魔法露露的判定）。</summary>
    public static async Task Train(PlayerChoiceContext choiceContext, Player player, decimal amount, CardModel? source = null)
    {
        if (amount <= 0m)
        {
            return;
        }
        if (player.Creature.GetPower<AraleNoTrainPower>() != null)
        {
            return;
        }
        var doubler = player.Creature.GetPower<AraleTrainingDoublePower>();
        if (doubler != null && doubler.Amount > 0)
        {
            amount *= 2m;
            int left = doubler.Amount - 1;
            if (left <= 0)
            {
                await PowerCmd.Remove(doubler);
            }
            else
            {
                doubler.SetAmount(left);
            }
        }
        // 阈值判定必须放在这里：PowerModel.AfterApplied 只在能力首次施加时触发，
        // 分多次锻炼时不会触发，会导致永远进不了魔法露露。
        // 遗物「保护腕带」：每当你锻炼时获得格挡（格挡在进入魔法露露之前结算，不受清空锻炼影响）
        foreach (IAraleTrainingListener listener in player.Relics.OfType<IAraleTrainingListener>().ToList())
        {
            await listener.OnAraleTrain(choiceContext, player, amount);
        }
        // 团魂任务：锻炼进度（用翻倍后的实际层数）
        AraleQuestTracker.NotifyTrained(player, amount);
        int threshold = TrainingThresholdFor(player);
        decimal total = Training(player.Creature) + amount;
        if (total < threshold)
        {
            await PowerCmd.Apply<AraleTrainingPower>(choiceContext, player.Creature, amount, player.Creature, source);
            await ReturnTempoCardsToHand(choiceContext, player);
            return;
        }

        // 达到阈值：清空锻炼（是否保留溢出层数取决于是否有「余裕」），并进入魔法露露
        bool keepOverflow = player.Creature.GetPower<AraleOverflowTrainingPower>() != null;
        decimal overflow = keepOverflow ? total - threshold : 0m;
        var training = player.Creature.GetPower<AraleTrainingPower>();
        if (overflow > 0m)
        {
            if (training == null)
            {
                await PowerCmd.Apply<AraleTrainingPower>(choiceContext, player.Creature, overflow, player.Creature, source);
            }
            else
            {
                training.SetAmount((int)overflow);
            }
        }
        else if (training != null)
        {
            await PowerCmd.Remove(training);
        }

        // 每回合只能进入 1 次魔法露露；已在状态中则只清空锻炼
        if (player.Creature.GetPower<AraleMagicLuluPower>() == null)
        {
            await EnterMagicLulu(choiceContext, player.Creature, source);
        }
        await ReturnTempoCardsToHand(choiceContext, player);
    }

    /// <summary>直接进入魔法露露状态。</summary>
    public static async Task EnterMagicLulu(PlayerChoiceContext choiceContext, Creature creature, CardModel? source = null)
    {
        await PowerCmd.Apply<AraleMagicLuluPower>(choiceContext, creature, 1m, creature, source);
        // 序号 97「传感器」：进入魔法露露时，其他玩家获得能量并抽牌
        var sensor = creature.GetPower<AraleSensorPower>();
        if (sensor != null)
        {
            await sensor.OnMagicLuluEntered(choiceContext, source);
        }
        // 遗物「收藏贴纸」：进入魔法露露时抽牌
        var owner = creature.Player;
        if (owner != null)
        {
            foreach (IAraleMagicLuluListener listener in owner.Relics.OfType<IAraleMagicLuluListener>().ToList())
            {
                await listener.OnAraleMagicLuluEntered(choiceContext, owner, source);
            }
            // 团魂任务：进入魔法露露的次数
            AraleQuestTracker.NotifyLuluEntered(owner);
        }
    }

    /// <summary>为一张牌添加保留。</summary>
    public static void AddRetain(CardModel card)
    {
        CardCmd.ApplyKeyword(card, CardKeyword.Retain);
        // 团魂任务：手牌里保留牌数量峰值（"给牌添加保留"常常发生在回合中）
        if (card.Owner != null)
        {
            AraleQuestTracker.NotifyRetainInHand(card.Owner);
        }
    }

    /// <summary>
    /// 获得临时敏捷：直接给原版的「敏捷」本身（因此 UI 显示的就是原版敏捷图标与数值），
    /// 另外记一笔隐藏账，回合结束时把这一笔扣回去。
    /// </summary>
    public static async Task GainTemporaryDexterity(PlayerChoiceContext choiceContext, Player owner, decimal amount, CardModel? source = null)
    {
        if (amount <= 0m)
        {
            return;
        }
        await PowerCmd.Apply<DexterityPower>(choiceContext, owner.Creature, amount, owner.Creature, source);
        await PowerCmd.Apply<AraleTemporaryDexterityPower>(choiceContext, owner.Creature, amount, owner.Creature, source, silent: true);
    }

    /// <summary>
    /// 获得临时荆棘：直接给原版的「荆棘」本身，另外记一笔隐藏账，回合结束时扣回。
    /// </summary>
    public static async Task GainTemporaryThorns(PlayerChoiceContext choiceContext, Player owner, decimal amount, CardModel? source = null)
    {
        if (amount <= 0m)
        {
            return;
        }
        await PowerCmd.Apply<ThornsPower>(choiceContext, owner.Creature, amount, owner.Creature, source);
        await PowerCmd.Apply<AraleTemporaryThornsPower>(choiceContext, owner.Creature, amount, owner.Creature, source, silent: true);
    }

    /// <summary>生成若干张衍生牌「肌肉棒棒」到手牌。</summary>
    public static async Task<IReadOnlyList<CardModel>> CreateMuscleBars(PlayerChoiceContext choiceContext, Player owner, ICombatState? combatState, int count, bool overflowToDiscard = false)
    {
        if (count <= 0 || combatState == null)
        {
            return Array.Empty<CardModel>();
        }
        int toHand = count;
        if (overflowToDiscard)
        {
            toHand = Math.Min(count, FreeHandSlots(owner));
        }
        int toDiscard = count - toHand;

        List<CardModel> handCards = new List<CardModel>();
        for (int i = 0; i < toHand; i++)
        {
            handCards.Add(combatState.CreateCard<MuscleBar>(owner));
        }
        if (handCards.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(handCards, PileType.Hand, owner);
        }

        // 手牌放不下的部分送入弃牌堆
        List<CardModel> discardCards = new List<CardModel>();
        for (int i = 0; i < toDiscard; i++)
        {
            discardCards.Add(combatState.CreateCard<MuscleBar>(owner));
        }
        if (discardCards.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(discardCards, PileType.Discard, owner);
        }

        return handCards.Concat(discardCards).ToList();
    }

    /// <summary>序号 27「节拍器」：每当你锻炼时，把弃牌堆里的节拍器放回手牌。</summary>
    private static async Task ReturnTempoCardsToHand(PlayerChoiceContext choiceContext, Player player)
    {
        var pile = PileType.Discard.GetPile(player);
        if (pile == null)
        {
            return;
        }
        foreach (var card in pile.Cards.Where(c => c is Metronome).ToList())
        {
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    /// <summary>手牌还能再放几张衍生牌（默认手牌上限 10）。</summary>
    public static int FreeHandSlots(Player player, int handLimit = 10)
    {
        int inHand = player.PlayerCombatState?.Hand?.Cards?.Count ?? 0;
        return Math.Max(0, handLimit - inHand);
    }
}

