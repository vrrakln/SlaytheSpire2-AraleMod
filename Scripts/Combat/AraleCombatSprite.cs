using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Arale.Scripts.Combat;

public partial class AraleCombatSprite : Sprite2D, IDeathDelayer
{
    public const string ScenePath = "res://arale_mod/scenes/arale_combat.tscn";
    private const string Art = "res://arale_mod/images/combat/";
    private readonly Dictionary<string, Texture2D[]> _frames = new();
    private readonly AralePlayback _playback = new();
    private NCreature? _creature;
    private string _lastState = "";
    private int _lastFrame = -1;
    private AraleLuluVfx _lulu = null!;

    public override void _Ready()
    {
        _lulu = new AraleLuluVfx { Name = "MagicLuluVfx", ShowBehindParent = true };
        AddChild(_lulu);
        foreach (string state in new[] { "attack", "skill", "power", "hit", "low_hp", "death" })
        {
            var frames = new Texture2D[state == "low_hp" ? 1 : AralePlayback.FrameCount];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = ResourceLoader.Load<Texture2D>($"{Art}{state}_{i:00}.png");
            _frames[state] = frames;
        }
        _frames["idle"] = new[] { ResourceLoader.Load<Texture2D>(Art + "idle.png") };
        RefreshFrame();
    }

    public void Play(string state, bool newCard = false) { _playback.Trigger(state, newCard); RefreshFrame(); }
    public async Task GetDelayTask()
    {
        Play("death");
        while (IsInsideTree() && _playback.State == "death" && _playback.Elapsed < 1.1)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    public override void _Process(double delta)
    {
        if (_creature == null)
            for (Node? node = GetParent(); node != null; node = node.GetParent())
                if (node is NCreature creature) { _creature = creature; break; }
        if (_creature?.Entity == null) return;
        _playback.Tick(delta, _creature.Entity.CurrentHp, _creature.Entity.MaxHp);
        _lulu.UpdateState(delta,
            _creature.Entity.IsAlive && Mechanics.AraleCmd.IsMagicLulu(_creature.Entity));
        RefreshFrame();
        // The scene supplies the fixed .72 scale. SurroundedPower owns the body
        // transform's facing; frame updates must preserve its horizontal flip.
        QueueRedraw();
    }
    private void RefreshFrame()
    {
        if (!_frames.TryGetValue(_playback.State, out var frames)) return;
        int frame = _playback.Frame;
        if (_lastState == _playback.State && _lastFrame == frame) return;
        Texture = frames[frame];
        _lastState = _playback.State; _lastFrame = frame;
    }
    public override void _Draw()
    {
        float progress = (float)(_playback.Elapsed / AralePlayback.Duration(_playback.State));
        float alpha = MathF.Sin(MathF.PI * progress);
        if (_playback.State == "skill")
            DrawArc(new Vector2(0, -240), 130, 0, MathF.Tau, 40, new Color(.2f, .95f, 1, alpha * .6f), 3, true);
        else if (_playback.State == "power")
            DrawArc(new Vector2(0, -260), 145, 0, MathF.Tau, 40, new Color(1, .8f, .2f, alpha * .65f), 5, true);
        else if (_playback.State == "attack" && progress > .3f && progress < .75f)
            DrawArc(new Vector2(150, -260), 65 + progress * 55, -.7f, .7f, 16, new Color(.3f, .9f, 1, alpha * .7f), 5, true);
    }
    public static AraleCombatSprite? For(NCreature? creature) =>
        creature?.Entity?.Player?.Character is Arale
            ? creature.Visuals?.GetNodeOrNull<AraleCombatSprite>("Visuals") : null;
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class AraleCombatTriggerPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(NCreature __instance, string trigger)
    {
        var sprite = AraleCombatSprite.For(__instance);
        if (sprite == null) return true;
        string? state = trigger switch
        {
            "Attack" => "attack", "Cast" or "Skill" => "skill", "Power" or "PowerUp" => "power",
            "Hit" or "Hurt" => "hit", "Dead" or "Die" => "death", "Revive" => "revive",
            "Idle" or "Relaxed" => "idle", _ => null
        };
        if (state != null) sprite.Play(state);
        return false;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class AraleCardAnimationPatch
{
    [HarmonyPrefix]
    private static void Prefix(CardModel __instance)
    {
        var sprite = AraleCombatSprite.For(__instance.Owner?.Creature.GetCreatureNode());
        if (sprite == null) return;
        string? state = __instance.Type switch
        { CardType.Attack => "attack", CardType.Skill => "skill", CardType.Power => "power", _ => null };
        if (state != null) sprite.Play(state, newCard: true);
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class AraleDeathAnimationPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance, ref float __result)
    {
        var sprite = AraleCombatSprite.For(__instance);
        if (sprite == null) return;
        sprite.Play("death");
        __result = Math.Max(__result, 1.1f);
    }
}
