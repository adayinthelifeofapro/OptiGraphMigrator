using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags Find query chains rooted on the <c>SearchClient.Instance</c> static singleton
    /// accessor, rather than an injected <c>IClient</c>.
    /// </summary>
    /// <remarks>
    /// Optimizely Graph's SDK client is designed to be resolved through dependency injection;
    /// it exposes no static singleton equivalent. This is an architectural concern rather than a
    /// per-call API mapping, so it is reported once per chain root instead of once per segment.
    /// </remarks>
    public sealed class StaticClientAccessorRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM108";

        /// <inheritdoc />
        public string Title => "SearchClient.Instance static singleton has no Graph SDK equivalent";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Configuration;

        /// <inheritdoc />
        public string MessageFormat => "Find query rooted on '{0}' uses the static SearchClient.Instance accessor, which has no Graph SDK equivalent";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            if (context.Segment != context.Chain.Root)
            {
                return false;
            }

            return context.Segment.Invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   ContainsStaticSearchClientInstanceAccess(memberAccess.Expression, context);
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
                "Optimizely Graph's SDK client is intended to be resolved via dependency injection " +
                "(constructor injection of the Graph client), unlike Find's static SearchClient.Instance " +
                "singleton accessor. Replace the static access with an injected client instance as part " +
                "of the migration.",
            };

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                "Inject the Graph client instead of accessing a static singleton",
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName });
        }

        private static bool ContainsStaticSearchClientInstanceAccess(ExpressionSyntax expression, RuleMatchContext context)
        {
            if (expression is not MemberAccessExpressionSyntax memberAccess ||
                !string.Equals(memberAccess.Name.Identifier.Text, "Instance", StringComparison.Ordinal))
            {
                return false;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
            var containingType = symbol?.ContainingType;

            return containingType is not null &&
                   string.Equals(containingType.Name, "SearchClient", StringComparison.Ordinal) &&
                   context.Symbols.IsDeclaredInFindNamespace(containingType);
        }
    }
}
