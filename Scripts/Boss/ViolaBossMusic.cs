using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Combat;

namespace Arale.Scripts.Boss;

// The game's GDScript music proxy calls native FMOD directly and cannot resolve
// RitsuLib virtual events. Intercept only this encounter's music before that proxy.
public partial class ViolaBossMusic : AudioStreamPlayer
{
    public const string NodeName = "ViolaBossMusic";
    public const string ResourcePath = "res://arale_mod/audio/viola_boss.wav";

    public override void _Ready() => RefreshVolume();
    public override void _Process(double delta)
    {
        if (!CombatManager.Instance.IsInProgress && GetParent() is NRunMusicController controller)
        {
            controller.StopCustomMusic();
            return;
        }
        RefreshVolume();
    }

    private void RefreshVolume()
    {
        float volume = SaveManager.Instance.SettingsSave.VolumeBgm;
        VolumeLinear = volume * volume;
    }

    public static bool PlayCustom(NRunMusicController controller, string track)
    {
        if (track != ViolaBoss.MusicEvent)
        {
            StopCustom(controller);
            return true;
        }
        if (controller.GetNodeOrNull<ViolaBossMusic>(NodeName) is { } active && active.Playing)
            return false;
        StopCustom(controller);
        var stream = ResourceLoader.Load<AudioStream>(ResourcePath);
        if (stream == null)
        {
            GD.PushError("[Arale] Could not load Viola boss music: " + ResourcePath);
            return false;
        }
        controller.GetNode<Node>("Proxy").Call("stop_music");
        var player = new ViolaBossMusic { Name = NodeName, Stream = stream, Bus = "Master" };
        controller.AddChild(player);
        player.Play();
        return false;
    }

    public static void StopCustom(NRunMusicController controller)
    {
        var player = controller.GetNodeOrNull<ViolaBossMusic>(NodeName);
        if (player == null) return;
        player.Stop();
        controller.RemoveChild(player);
        player.QueueFree();
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.PlayCustomMusic))]
internal static class ViolaMusicPlayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NRunMusicController __instance, string customMusic) =>
        ViolaBossMusic.PlayCustom(__instance, customMusic);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopCustomMusic))]
internal static class ViolaMusicStopCustomPatch
{
    [HarmonyPrefix]
    private static void Prefix(NRunMusicController __instance) => ViolaBossMusic.StopCustom(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopMusic))]
internal static class ViolaMusicStopPatch
{
    [HarmonyPrefix]
    private static void Prefix(NRunMusicController __instance) => ViolaBossMusic.StopCustom(__instance);
}
