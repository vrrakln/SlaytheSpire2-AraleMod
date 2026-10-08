using System;
using System.Collections.Generic;
using System.Linq;

namespace Arale.Scripts.Combat;

public static class AralePowerOrder
{
    // Stable ordering preserves application order among all non-core powers.
    public static void Prioritize<T>(List<T> nodes, Func<T, int> priority)
    {
        var ordered = nodes.OrderBy(priority).ToArray();
        nodes.Clear();
        nodes.AddRange(ordered);
    }
}
