using System;

namespace Arale.Scripts.Combat;

// Engine-independent playback policy, also exercised by the animation smoke test.
public sealed class AralePlayback
{
    public const int FrameCount = 32;
    public string State { get; private set; } = "idle";
    public double Elapsed { get; private set; }
    public bool LowHp { get; private set; }
    public static double Duration(string state) => state switch
    {
        "attack" => .85, "skill" => 1.0, "power" => 1.2,
        "hit" => .6, "death" => 1.1, "low_hp" => 1.6, _ => 1.6
    };
    // Original key-pose timing is baked into 32 uniformly sampled inbetweens.
    public int Frame => State is "idle" or "low_hp" ? 0 :
        Math.Min(FrameCount - 1, (int)(Elapsed / Duration(State) * FrameCount));
    public void Trigger(string state, bool newCard = false)
    {
        if (State == "death" && state != "revive") return;
        if (state == "revive") { State = LowHp ? "low_hp" : "idle"; Elapsed = 0; return; }
        if (!newCard && state == State) return;
        if (!newCard && state == "skill" && State == "power") return; // generic Cast must not replace Power
        State = state;
        Elapsed = 0;
    }
    public void Tick(double delta, int hp, int maxHp)
    {
        if (hp <= 0) Trigger("death");
        else
        {
            if (State == "death") Trigger("revive");
            double ratio = hp / (double)Math.Max(1, maxHp);
            if (ratio <= .30) LowHp = true;
            else if (ratio > .35) LowHp = false;
        }
        Elapsed += Math.Max(0, delta);
        if (State == "death") { Elapsed = Math.Min(Elapsed, Duration(State)); return; }
        if (State is "idle" or "low_hp")
        {
            string desired = LowHp ? "low_hp" : "idle";
            if (State != desired) { State = desired; Elapsed = 0; }
            else Elapsed %= Duration(State);
        }
        else if (Elapsed >= Duration(State)) { State = LowHp ? "low_hp" : "idle"; Elapsed = 0; }
    }
}
