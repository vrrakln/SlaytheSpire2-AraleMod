using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Arale.Scripts.Cards;

// Preview the existing formula without changing the card's execution or saved variable names.
internal sealed class CurtainCallDamagePreviewVar : CalculatedDamageVar
{
    public CurtainCallDamagePreviewVar() : base(ValueProp.Move) { }
    protected override DynamicVar GetBaseVar() => ((CardModel)_owner!).DynamicVars.Damage;
    protected override DynamicVar GetExtraVar() => ((CardModel)_owner!).DynamicVars["DamagePerDream"];
}

internal sealed class SongBlockPreviewVar : CalculatedBlockVar
{
    public SongBlockPreviewVar() : base(ValueProp.Move) { }
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        // Shape of Voice skips GainBlock entirely with zero Song, even with Dexterity.
        if (Calculate(target) <= 0m) PreviewValue = 0m;
    }
}
