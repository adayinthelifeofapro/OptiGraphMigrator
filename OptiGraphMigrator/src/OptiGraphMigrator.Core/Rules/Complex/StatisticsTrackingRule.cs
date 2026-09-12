using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags Find's search statistics and popularity tracking APIs, which have no equivalent in
    /// Optimizely Graph.
    /// </summary>
    public sealed class StatisticsTrackingRule : IMigrationRule
    {
        private static readonly HashSet<string> StatisticsMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "GetPopularSearchTerms",
            "GetSearchesPerHour",
            "GetSearchesPerDay",
            "GetClickedResults",
            "GetPopularClickedDocuments",
        };

        /// <inheritdoc />
        public string Id => "OGM201";

        /// <inheritdoc />
        public string Title => "Search statistics and popularity tracking have no Graph equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Statistics;

        /// <inheritdoc />
        public string MessageFormat => "Find statistics API '{0}(...)' has no clean Graph SDK translation";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;

            if (context.Symbols.StatisticsType is not null &&
                context.Symbols.IsAssignableTo(context.Segment.ReceiverType, context.Symbols.StatisticsType))
            {
                return true;
            }

            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   StatisticsMethodNames.Contains(method.Name);
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
                "Optimizely Graph does not expose a search statistics or popularity-tracking API. This " +
                "functionality must be replaced with an application-level analytics/telemetry solution " +
                "(for example capturing query events and aggregating them separately).",
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
