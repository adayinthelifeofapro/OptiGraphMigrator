using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags Find's server-side custom scoring APIs (<c>Boost</c>, <c>CustomScore</c>,
    /// <c>FunctionScore</c>), which have no equivalent scoring pipeline in Optimizely Graph.
    /// </summary>
    public sealed class CustomScoringRule : IMigrationRule
    {
        private static readonly HashSet<string> ScoringMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Boost",
            "CustomScore",
            "FunctionScore",
            "InField",
        };

        /// <inheritdoc />
        public string Id => "OGM202";

        /// <inheritdoc />
        public string Title => "Custom relevance scoring has no Graph equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Query;

        /// <inheritdoc />
        public string MessageFormat => "Find custom scoring '{0}(...)' has no clean Graph SDK translation";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   ScoringMethodNames.Contains(method.Name);
        }

        /// <inheritdoc />
        public RuleMatch? Evaluate(RuleMatchContext context)
        {
            if (!AppliesTo(context))
            {
                return null;
            }

            var caveats = new[]
            {
                "Optimizely Graph does not expose a custom relevance scoring pipeline equivalent to Find's " +
                "Boost/CustomScore/FunctionScore. Result ranking must rely on Graph's built-in relevance " +
                "scoring, or be re-implemented as post-query re-ranking in application code.",
            };

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Blocked,
                string.Empty,
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName });
        }
    }
}
