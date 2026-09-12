namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags <c>UsingSynonyms</c>, a Find query-time behaviour with no Optimizely Graph
    /// equivalent (Graph has no configurable synonym expansion at query time).
    /// </summary>
    public sealed class SynonymsRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM203";

        /// <inheritdoc />
        public string Title => "Query-time synonym expansion has no Graph equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Query;

        /// <inheritdoc />
        public string MessageFormat => "Find '{0}(...)' has no clean Graph SDK translation";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   string.Equals(method.Name, "UsingSynonyms", System.StringComparison.Ordinal);
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
                "UsingSynonyms() enables Find's configured synonym dictionary for this query. Optimizely Graph " +
                "has no equivalent query-time synonym expansion; synonym behaviour must be reproduced by " +
                "expanding search terms in application code before the query is issued, or accepted as a " +
                "relevance regression.",
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
