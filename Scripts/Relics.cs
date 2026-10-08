using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Arale.Scripts.Mechanics;

namespace Arale.Scripts;

/// <summary>
/// 阿拉蕾初始遗物：每场战斗开始时，获得 1 点梦想。
/// 使用已确认的阿拉蕾专属遗物图标。
/// </summary>
[RegisterRelic(typeof(AraleRelicPool))]
[RegisterCharacterStarterRelic(typeof(Arale))]
[RegisterTouchOfOrobasRefinement(typeof(AraleDreamRelicPlus))]
public sealed class AraleDreamRelic : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleDreamRelic));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleDreamRelic));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleDreamRelic));

    public override async Task BeforeCombatStart()
    {
        Flash();
        await AraleCmd.GainDream(new ThrowingPlayerChoiceContext(), Owner, 1m);
    }
}

/// <summary>欧洛巴斯之触精炼后的初始遗物：每场战斗开始时，获得 3 点梦想。</summary>
[RegisterRelic(typeof(AraleRelicPool))]
public sealed class AraleDreamRelicPlus : AraleRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string? CustomIconPath => AraleRelicArt.Icon(nameof(AraleDreamRelicPlus));
    public override string? CustomIconOutlinePath => AraleRelicArt.Outline(nameof(AraleDreamRelicPlus));
    public override string? CustomBigIconPath => AraleRelicArt.Big(nameof(AraleDreamRelicPlus));

    public override async Task BeforeCombatStart()
    {
        Flash();
        await AraleCmd.GainDream(new ThrowingPlayerChoiceContext(), Owner, 3m);
    }
}
