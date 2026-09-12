using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags Find-backed <c>IContentLoader</c> helper extensions (for example
    /// <c>contentLoader.Search&lt;T&gt;()</c> style helpers contributed by
    /// <c>EPiServer.Find.Cms</c>) that quietly route through Find under a familiar
    /// <c>IContentLoader</c>-shaped API.
    /// </summary>
    public sealed class ContentLoaderFindHelperRule : IMigrationRule
    {
        private static readonly HashSet<string> HelperMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Search",
            "FindByContentType",
            "FindPagesWithCriteria",
            "GetChildrenWithFind",
        };

        /// <inheritdoc />
        public string Id => "OGM104";

        /// <inheritdoc />
        public string Title => "Find-backed IContentLoader helper should move to an explicit Graph query";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.ContentLoading;

        /// <inheritdoc />
        public string MessageFormat => "'{0}(...)' is an IContentLoader helper backed by Find; migrate the call site to the Graph SDK client";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;

            if (context.Symbols.ContentLoaderType is null ||
                !context.Symbols.IsAssignableTo(context.Segment.ReceiverType, context.Symbols.ContentLoaderType))
            {
                return false;
            }

            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   HelperMethodNames.Contains(method.Name);
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
                "This call is an IContentLoader-shaped extension that is actually backed by Find under the " +
                "covers. Callers relying on IContentLoader's usual (non-index, always-consistent) semantics " +
                "should be aware Graph query results reflect the index's eventual consistency, and the call " +
                "site should be migrated to an explicit Graph client query instead of an IContentLoader call.",
            };

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                "Inject and query the Graph SDK client directly instead of this IContentLoader helper",
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName });
        }
    }
}
