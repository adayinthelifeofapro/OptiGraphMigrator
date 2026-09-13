using System;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags Find's push-based indexing API (<c>Index</c>, <c>UpdateIndex</c>, <c>DeleteIndex</c>)
    /// called directly on <c>IClient</c>. Optimizely Graph does not accept application-driven index
    /// writes; content is synchronised into the index by the CMS content sync pipeline instead.
    /// </summary>
    /// <remarks>
    /// Evaluated standalone (like <see cref="ClientConventionsRule"/>) since these calls are made
    /// directly on <c>IClient</c> rather than through a resolvable query chain.
    /// </remarks>
    public sealed class IndexingApiRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM107";

        /// <inheritdoc />
        public string Title => "Find push-based indexing calls have no Graph SDK equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Configuration;

        /// <inheritdoc />
        public string MessageFormat => "Find indexing call '{0}(...)' has no Graph SDK equivalent; content indexing is managed by the CMS content sync pipeline instead";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;

            if (context.Symbols.ClientType is not null &&
                context.Symbols.IsAssignableTo(context.Segment.ReceiverType, context.Symbols.ClientType) &&
                (string.Equals(method.Name, "Index", StringComparison.Ordinal) ||
                 string.Equals(method.Name, "UpdateIndex", StringComparison.Ordinal) ||
                 string.Equals(method.Name, "DeleteIndex", StringComparison.Ordinal)))
            {
                return true;
            }

            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   (string.Equals(method.Name, "Index", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "UpdateIndex", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "DeleteIndex", StringComparison.Ordinal));
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
                "Optimizely Graph has no application-facing API for pushing, updating or deleting individual " +
                "index entries. Content is kept in sync with the index automatically by the CMS content sync " +
                "pipeline (or a scheduled/manual re-index), so direct calls to Find's Index()/UpdateIndex()/" +
                "DeleteIndex() should be removed rather than translated.",
            };

            const string suggestedApproach =
                "Delete the call and let the CMS content sync pipeline own indexing. If the content being " +
                "pushed here is not CMS content, model it as a Graph content/source type and populate it via " +
                "the Graph Content Ingestion (HTTP) API from a scheduled job or background service instead of " +
                "an inline per-request call. If the call exists purely to force freshness after an edit, rely " +
                "on the sync job instead and design the calling code to tolerate the index's eventual " +
                "consistency (for example read straight from IContentLoader/IContentRepository when you need " +
                "read-your-own-write semantics).";

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
