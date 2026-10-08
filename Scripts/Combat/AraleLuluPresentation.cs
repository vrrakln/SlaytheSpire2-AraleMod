using System;
namespace Arale.Scripts.Combat;
public sealed class AraleLuluPresentation
{
    public const double ExitSeconds = 2;
    public bool Active { get; private set; }
    public double Age { get; private set; }
    public float Opacity { get; private set; }
    private float _transitionStart;
    public void Tick(double delta, bool active)
    {
        delta = Math.Max(0, delta);
        if (active != Active) { Active = active; Age = 0; _transitionStart = Opacity; }
        Age += delta;
        Opacity = Active
            ? _transitionStart + (1 - _transitionStart) * (float)Math.Min(1, Age / .25)
            : _transitionStart * (1 - (float)Math.Min(1, Age / ExitSeconds));
    }
}
