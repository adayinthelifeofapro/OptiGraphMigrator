using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags <c>Filter(...)</c> predicates whose lambda body uses field-level operators
    /// (<c>Prefix</c>, <c>GreaterThan</c>, <c>LessThan</c>, <c>Exists</c>) that translate to
    /// Graph 'where' comparisons with subtly different semantics (case sensitivity, null
    /// handling, inclusive/exclusive bounds).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="InMemoryPredicateFilterRule"/>, the operators this rule recognises are
    /// part of Find's own filter DSL and do translate to Graph; the goal here is to surface a
    /// caveat alongside the declarative OGM001 "exact" mapping rather than to block translation.
    /// </remarks>
    public sealed class FieldFilterOperatorRule : IMigrationRule
    {
        private static readonly Dictionary<string, string> OperatorCaveats = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Prefix"] = "Find's Prefix() prefix match may use different case-sensitivity/analysis rules than Graph's 'where' string prefix comparison; verify the field's Graph analyzer configuration produces equivalent results.",
            ["GreaterThan"] = "Confirm whether Find's GreaterThan() is inclusive or exclusive for the field's data type and match it against the correct Graph 'where' comparison operator (gt vs gte).",
            ["LessThan"] = "Confirm whether Find's LessThan() is inclusive or exclusive for the field's data type and match it against the correct Graph 'where' comparison operator (lt vs lte).",
            ["Exists"] = "Find's Exists() treats missing and null field values the same way; verify the equivalent Graph 'where' null-check clause matches this behavior for the target field.",
            ["In"] = "Find's In() matches against a set of values; confirm the equivalent Graph 'where' 'in' comparison uses the same equality semantics (e.g. case sensitivity) as the Find field operator.",
            ["InRange"] = "Confirm whether Find's InRange() bounds are inclusive or exclusive for the field's data type and match them against the correct Graph 'where' range operators (gte/lte vs gt/lt).",
        };

        /// <inheritdoc />
        public string Id => "OGM207";

        /// <inheritdoc />
        public string Title => "Filter() field operator has subtly different Graph 'where' semantics";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Filtering;

        /// <inheritdoc />
        public string MessageFormat => "Find '{0}(...)' predicate uses a field operator that requires manual review of Graph 'where' semantics";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            if (!context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) ||
                !string.Equals(method.Name, "Filter", StringComparison.Ordinal))
            {
                return false;
            }

            var lambda = context.Segment.FirstLambda;
            return lambda is not null && FindReviewedOperator(lambda, context.SemanticModel) is not null;
        }

        /// <inheritdoc />
        public RuleMatch? Evaluate(RuleMatchContext context)
        {
            var lambda = context.Segment.FirstLambda;
            if (lambda is null)
            {
                return null;
            }

            var operatorName = FindReviewedOperator(lambda, context.SemanticModel);
            if (operatorName is null)
            {
                return null;
            }

            var caveats = new[] { OperatorCaveats[operatorName] };

            const string suggestedApproach =
                "Confirm the Graph field's type and analyzer before settling on an operator: match/contains " +
                "behave differently on analysed text fields than on keyword-style fields, so pick the operator " +
                "whose semantics match the intent (exact identity, prefix, or full text) and cover the " +
                "behaviour with an integration test over representative content rather than assuming parity " +
                "with Find.";

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                ".Where(x => <field comparison>)",
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName },
                suggestedApproach: suggestedApproach);
        }

        private static string? FindReviewedOperator(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
        {
            foreach (var invocation in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var name = memberAccess.Name.Identifier.Text;
                if (!OperatorCaveats.ContainsKey(name))
                {
                    continue;
                }

                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol?.ContainingNamespace?.ToDisplayString()?.StartsWith("EPiServer.Find", StringComparison.Ordinal) == true)
                {
                    return name;
                }
            }

            return null;
        }
    }
}
