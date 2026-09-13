using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags <c>Filter(...)</c> predicates whose lambda body invokes arbitrary .NET code rather
    /// than composing Find's translatable filter DSL. These predicates run in memory against
    /// materialised data (or fail outright), so no Graph 'where' clause can reproduce them.
    /// </summary>
    /// <remarks>
    /// This overrides the declarative <c>OGM001</c> "exact" mapping for the subset of Filter
    /// calls it recognises as non-translatable; the analyser prefers the most specific match.
    /// </remarks>
    public sealed class InMemoryPredicateFilterRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM204";

        /// <inheritdoc />
        public string Title => "In-memory Filter() predicate has no clean Graph translation";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Filtering;

        /// <inheritdoc />
        public string MessageFormat => "Find '{0}(...)' predicate calls arbitrary code and has no clean Graph SDK translation";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            if (!context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) ||
                !string.Equals(method.Name, "Filter", System.StringComparison.Ordinal))
            {
                return false;
            }

            var lambda = context.Segment.FirstLambda;
            return lambda is not null && ContainsUntranslatableCall(lambda, context.SemanticModel, context.Symbols);
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
                "This Filter() predicate calls arbitrary .NET code (a method not part of Find's filter DSL). " +
                "Such predicates only work because Find can fall back to evaluating them outside the index; " +
                "Optimizely Graph 'where' clauses can only express conditions the index itself can evaluate, " +
                "so this logic must be redesigned as either an indexed field comparison or a post-query filter " +
                "applied to the Graph results in application code.",
            };

            const string suggestedApproach =
                "Decide which of the two shapes this predicate really is. If the computed value is stable per " +
                "content item, precompute it into a property on the content type so it gets indexed, then " +
                "express the condition as a plain Graph 'where' clause over that field - this keeps filtering " +
                "and paging server-side. If the value genuinely depends on per-request state (current user, " +
                "request culture, permissions), fetch a superset from Graph and apply the predicate with LINQ " +
                "to the returned results, but move paging into application code too, since skip/limit applied " +
                "before an in-memory filter will return short or uneven pages.";

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

        private static bool ContainsUntranslatableCall(
            LambdaExpressionSyntax lambda,
            SemanticModel semanticModel,
            Core.Analysis.FindSymbolIndex symbols)
        {
            foreach (var invocation in lambda.DescendantNodes())
            {
                if (invocation is not InvocationExpressionSyntax invocationSyntax)
                {
                    continue;
                }

                var symbol = semanticModel.GetSymbolInfo(invocationSyntax).Symbol as IMethodSymbol;
                if (symbol is null)
                {
                    // Unresolved call inside the predicate; treat conservatively as untranslatable.
                    return true;
                }

                if (symbols.IsDeclaredInFindNamespace(symbol.ContainingType))
                {
                    // Part of Find's own filter DSL (e.g. Match, GreaterThan) - translatable.
                    continue;
                }

                if (symbol.ContainingNamespace?.ToDisplayString() == "System.Linq" ||
                    (symbol.ContainingType?.Name == "String" && symbol.ContainingType.ContainingNamespace?.Name == "System"))
                {
                    // Common LINQ / string helpers used inside otherwise-simple predicates are still
                    // arbitrary from the index's point of view, but treated as translatable string
                    // pattern matches (StartsWith/Contains) which Graph 'where' clauses do support.
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
