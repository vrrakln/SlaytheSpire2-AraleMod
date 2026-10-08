using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts.Cards;

// =====================================================================================
//  先古卡（序号 91、92）
// =====================================================================================

/// <summary>序号 91 · 梦幻开局：古老牙齿将「梦的开始」转化而成的先古牌（保留，获得梦想）。</summary>
/// <remarks>9/15 加强：2/3 点梦想 → **3/5 点梦想**，并追加「保留」关键词。</remarks>
[RegisterCard(typeof(AraleCardPool))]
public sealed class DreamOpening : AraleCardModel
{
    public DreamOpening() : base(0, CardType.Skill, CardRarity.Ancient, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 3m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AraleCmd.GainDream(choiceContext, Owner, DynamicVars["Dream"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dream"].UpgradeValueBy(2m);
    }
}

/// <summary>序号 92：先古能力牌（尘封魔典的奖励）——你每打出 1 张牌，获得 1 点梦想。</summary>
/// <remarks>9/15 削弱：费用 1 → **2**（升级后 1），并**移除打出瞬间的 +1 梦想**。</remarks>
[RegisterCard(typeof(AraleCardPool))]
[RegisterDustyTomeCard(typeof(Arale))]
public sealed class InfiniteDream : AraleCardModel
{
    public InfiniteDream() : base(2, CardType.Power, CardRarity.Ancient, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Dream", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 9/15：不再在打出瞬间额外获得 1 点梦想（现在只有「每打出 1 张牌 +1」这一条）
        await PowerCmd.Apply<AraleDreamPerCardPlayedPower>(choiceContext, Owner.Creature, DynamicVars["Dream"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
