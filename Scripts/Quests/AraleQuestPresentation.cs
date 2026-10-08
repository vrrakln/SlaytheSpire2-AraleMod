using System.Text.RegularExpressions;

namespace Arale.Scripts.Quests;

/// <summary>A read-only UI snapshot. No quest counters or completion flags are advanced here.</summary>
public sealed record AraleQuestPresentation(string Text, string Tooltip, bool Completed)
{
    public static AraleQuestPresentation Create(AraleQuestTemplate template, int tierIndex,
        string text, bool completed, int combatValue, int runValue, int streak,
        int attacks, bool inCombat, string? rewardName = null)
    {
        var tier = template.Tiers[tierIndex];
        string reward = string.IsNullOrEmpty(rewardName) ? AraleQuestTable.RewardNameFor(template.Theme) : rewardName;
        text = Regex.Replace(text, @"\[[^\]]+\]", string.Empty);
        string tooltip;
        if (completed) tooltip = AraleQuestText.Get("COMPLETED");
        else
        {
            string comparison = template.AtMost ? AraleQuestText.Get("AT_MOST") : string.Empty;
            tooltip = template.Mode switch
            {
                AraleQuestMode.RunTotal => AraleQuestText.Get("RUN_TOTAL", runValue, tier.A, comparison),
                AraleQuestMode.MaxEver => AraleQuestText.Get("RUN_BEST", runValue, tier.A, comparison),
                AraleQuestMode.Streak => AraleQuestText.Get("STREAK", streak, tier.B) + "\n" +
                    (inCombat ? AraleQuestText.Get("COMBAT", combatValue, tier.A, comparison)
                        : AraleQuestText.Get("PER_COMBAT", tier.A, comparison)) + "\n" + AraleQuestText.Get("STREAK_RESET"),
                _ => AraleQuestText.Get(inCombat ? "COMBAT" : "LAST_COMBAT", combatValue, tier.A, comparison)
            };
            if (template.RequireNoAttacks)
                tooltip += "\n" + (inCombat
                    ? AraleQuestText.Get("ATTACK_COUNT", attacks) + "\n" + AraleQuestText.Get(attacks == 0 ? "NO_ATTACK_OK" : "NO_ATTACK_FAILED")
                    : AraleQuestText.Get("NO_ATTACK_RULE"));
            tooltip += "\n" + AraleQuestText.Get("SETTLEMENT");
        }
        if (template.Metric is AraleQuestMetric.DreamPeak or AraleQuestMetric.RetainInHandPeak)
            tooltip += "\n" + AraleQuestText.Get("PEAK_RULE");
        if (template.Metric is AraleQuestMetric.RetainInHandPeak or AraleQuestMetric.RetainCountTotal or AraleQuestMetric.RetainCardsPlayed)
            tooltip += "\n" + AraleQuestText.Get("RETAIN_RULE");
        if (template.Metric == AraleQuestMetric.RetainCountTotal)
            tooltip += "\n" + AraleQuestText.Get("RETAIN_COUNT_RULE");
        if (template.Metric == AraleQuestMetric.CombatHpLost)
            tooltip += "\n" + AraleQuestText.Get("NO_HP_LOSS_RULE");
        if (template.Metric == AraleQuestMetric.EnemiesKilled)
            tooltip += "\n" + AraleQuestText.Get("KILLS_RULE");
        // Always expose the full condition, including any no-Attacks restriction.
        return new(text, text + "\n\n" + tooltip + "\n" + AraleQuestText.Get("REWARD", reward), completed);
    }
}
