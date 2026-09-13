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

            const string suggestedApproach =
                "Wrap your Graph query execution in a thin search service and emit a structured event " +
                "(search term, result count, filters, timestamp, and a correlation id) for every query, plus a " +
                "second event when a user clicks a result. Send those events to whatever telemetry sink the " +
                "solution already uses - for example ILogger + Application Insights custom events, Optimizely " +
                "Data Platform / Web Experimentation tracking, or an events table in your own database - then " +
                "rebuild the popular-terms, searches-per-hour/day and clicked-result reports as queries " +
                "(Kusto, SQL, or a dashboard) over that event stream.";

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Blocked,
                string.Empty,
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName },
                suggestedApproach: suggestedApproach);
        }
    }
}
