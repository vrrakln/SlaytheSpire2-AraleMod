using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Arale.Scripts.Quests;

[HarmonyPatch(typeof(NRun), nameof(NRun._Ready))]
internal static class AraleQuestPageRunPatch
{
    private static void Postfix(NRun __instance, RunState ____state)
    {
        if (TestMode.IsOn || DisplayServer.GetName() == "headless" || __instance.HasNode("AraleQuestPage")) return;
        var run = __instance;
        var state = ____state;
        var page = new AraleQuestPage {
            Name = "AraleQuestPage", Host = run,
            FindBook = () => LocalContext.GetMe(state.Players)?.Relics.OfType<AraleQuestBookRelic>().FirstOrDefault(),
            ReadScreen = () => {
                var screen = ActiveScreenContext.Instance.GetCurrentScreen();
                bool combat = screen is NCombatRoom { Mode: CombatRoomMode.ActiveCombat };
                var player = LocalContext.GetMe(state.Players);
                var creature = player == null ? null : run.CombatRoom?.GetCreatureNode(player.Creature);
                Vector2? anchor = creature == null ? null : run.GetGlobalTransformWithCanvas().AffineInverse() * creature.Hitbox.GetGlobalRect().Position;
                return (screen, combat, anchor);
            }
        };
        run.AddChild(page);
    }
}
