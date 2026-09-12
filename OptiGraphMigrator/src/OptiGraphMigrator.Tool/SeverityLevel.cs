using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Tool
{
    /// <summary>Common severity ordering shared by the <c>--severity-threshold</c> and <c>--fail-on</c> options.</summary>
    internal static class SeverityLevel
    {
        private static readonly Dictionary<string, int> Ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["info"] = 0,
            ["warning"] = 1,
            ["error"] = 2
        };

        /// <summary>Returns true when <paramref name="severity"/> is at or above <paramref name="threshold"/>.</summary>
        public static bool Meets(string severity, string threshold)
        {
            return Rank(severity) >= Rank(threshold);
        }

        private static int Rank(string severity)
        {
            return Ranks.TryGetValue(severity, out var rank) ? rank : 0;
        }
    }
}
