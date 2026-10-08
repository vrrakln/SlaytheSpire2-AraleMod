using Godot;
using HarmonyLib;
using Arale.Scripts.Cards;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Arale.Scripts.Combat;

// This event runs once for each actual resolution, including auto-play and repeats.
// It does not run for targeting previews, cancelled plays, or individual damage hits.
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class AraleCardAudio
{
    internal const string AttackPath = "res://arale_mod/audio/card_attack.mp3";
    internal const string DreamPath = "res://arale_mod/audio/card_dream_is_power.wav";

    [HarmonyPostfix]
    internal static void Postfix(CardPlay cardPlay)
    {
        var room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;

        string? path = null;
        if (cardPlay.Card is DreamIsPower)
            path = DreamPath; // Applies to every character, independent of the attack toggle.
        else if (cardPlay.Card.Type == CardType.Attack && cardPlay.Player.Character is global::Arale.Scripts.Arale
                 && AraleSettings.AttackSoundEnabled)
            path = AttackPath;
        if (path == null) return;

        var stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null) return;
        var player = new AudioStreamPlayer { Name = "AraleCardSound", Stream = stream, Bus = "SFX" };
        room.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
        // Parenting to the combat room also cleans up unfinished sounds on leaving combat.
    }
}
