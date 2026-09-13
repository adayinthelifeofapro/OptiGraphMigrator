using System.Linq;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags <c>UnifiedSearch</c> queries, which search across multiple content types in a
    /// single Find call. Optimizely Graph has no single-query equivalent for heterogeneous
    /// content type unions.
    /// </summary>
    public sealed class UnifiedSearchMultiTypeRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM102";

        /// <inheritdoc />
        public string Title => "UnifiedSearch has no single-query Graph equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.UnifiedSearch;

        /// <inheritdoc />
        public string MessageFormat => "Find '{0}(...)' searches multiple content types; this requires re-modelling as separate Graph queries";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var root = context.Chain.Root;
            return root is not null &&
                   context.Segment.Invocation == root.Invocation &&
                   string.Equals(root.MethodName, "UnifiedSearch", System.StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public RuleMatch? Evaluate(RuleMatchContext context)
        {
            if (!AppliesTo(context))
            {
                return null;
            }

            var forSegmentCount = context.Chain.Segments.Count(s => string.Equals(s.MethodName, "For", System.StringComparison.Ordinal));

            var caveats = new System.Collections.Generic.List<string>
            {
                "UnifiedSearch queries multiple content types in one round trip. Optimizely Graph queries are " +
                "shaped per GraphQL type, so this must be re-modelled as either multiple targeted queries or a " +
                "query against a shared interface/union type exposed by the Graph schema, if one exists.",
            };

            if (forSegmentCount > 1)
            {
                caveats.Add(
                    forSegmentCount + " '.For<T>()' type filters were found on this UnifiedSearch chain; each " +
                    "will likely need its own Graph query.");
            }

            var hasWeightMultiplier = context.Chain.Segments.Any(s => string.Equals(s.MethodName, "WeightMultiplier", System.StringComparison.Ordinal));
            if (hasWeightMultiplier)
            {
                caveats.Add(
                    "WeightMultiplier() boosts one content type's relevance score relative to the others within a " +
                    "single UnifiedSearch call. Graph has no equivalent cross-type score weighting, so this must be " +
                    "approximated with per-query result-ordering logic in application code, or with a Graph 'boost' " +
                    "applied within each separate per-type query.");
            }

            const string suggestedApproach =
                "Model the unified result set yourself: define a single result DTO (title, url, excerpt, type) " +
                "and a small service that either queries a shared Graph interface/union type once, or issues " +
                "one query per content type in parallel and merges the results into that DTO. Merge order and " +
                "paging then become explicit application concerns - decide up front whether you interleave by " +
                "relevance, or present per-type groups, and page over the merged list rather than relying on " +
                "per-query skip/limit.";

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                "Separate Graph queries per content type, or a query against a shared union/interface type",
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName },
                suggestedApproach: suggestedApproach);
        }
    }
}
