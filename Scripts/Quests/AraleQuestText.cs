using System.Globalization;
using MegaCrit.Sts2.Core.Localization;
namespace Arale.Scripts.Quests;

/// <summary>Notebook UI strings live alongside quest descriptions in static_hover_tips.json.</summary>
internal static class AraleQuestText
{
    public static string Get(string key, params object[] args) => string.Format(
        CultureInfo.InvariantCulture, new LocString(AraleQuestTable.LocTable, "ARALE_QUEST_UI_" + key).GetRawText(), args);
}
