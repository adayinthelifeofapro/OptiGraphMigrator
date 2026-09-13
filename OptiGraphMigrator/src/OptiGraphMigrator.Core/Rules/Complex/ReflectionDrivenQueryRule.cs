using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags query construction driven by reflection (<c>typeof</c>, <c>MethodInfo</c>,
    /// <c>Expression.Call</c>/<c>Expression.Lambda</c> built dynamically) inside a Find fluent
    /// chain argument. These queries are assembled at runtime in a shape the analyser - and
    /// realistically most migration tooling - cannot statically translate.
    /// </summary>
    public sealed class ReflectionDrivenQueryRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM205";

        /// <inheritdoc />
        public string Title => "Reflection-driven query construction has no clean Graph translation";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Query;

        /// <inheritdoc />
        public string MessageFormat => "Find '{0}(...)' argument is built via reflection/expression trees and has no clean Graph SDK translation";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            if (!context.Symbols.IsDeclaredInFindNamespace(method.ContainingType))
            {
                return false;
            }

            foreach (var argument in context.Segment.Arguments)
            {
                if (ContainsReflectionOrExpressionBuilding(argument.Expression, context.SemanticModel))
                {
                    return true;
                }
            }

            return false;
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
                "This argument builds part of the query dynamically via reflection or runtime expression tree " +
                "construction. Optimizely Graph queries are declarative GraphQL documents; dynamically composed " +
                "query fragments must be re-implemented as an explicit GraphQL query builder rather than " +
                "reflected/expression-tree-driven code.",
            };

            const string suggestedApproach =
                "Replace the reflection/expression-tree composition with explicit, typed query building. " +
                "Enumerate the supported filter options as a small model (an enum or a record per facet), map " +
                "each option to a concrete Graph 'where' clause in a switch, and compose the selected clauses " +
                "with the Graph SDK's And/Or builders - or, if you emit GraphQL text directly, build the " +
                "document from a whitelist of field names and pass user values as GraphQL variables rather " +
                "than string concatenation. Driving field names off typeof(T).GetProperty(...) at runtime has " +
                "no Graph counterpart and also loses compile-time validation against the Graph schema.";

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

        private static bool ContainsReflectionOrExpressionBuilding(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            foreach (var node in expression.DescendantNodesAndSelf())
            {
                switch (node)
                {
                    case TypeOfExpressionSyntax:
                        return true;

                    case InvocationExpressionSyntax invocation:
                        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        var containingType = symbol?.ContainingType?.ToDisplayString();
                        if (containingType is "System.Linq.Expressions.Expression" ||
                            containingType == "System.Reflection.MethodInfo" ||
                            containingType == "System.Type")
                        {
                            return true;
                        }

                        break;

                    case MemberAccessExpressionSyntax memberAccess
                        when memberAccess.Name.Identifier.Text is "GetMethod" or "GetProperty" or "Invoke"
                             && semanticModel.GetSymbolInfo(memberAccess).Symbol is IMethodSymbol m
                             && (m.ContainingType?.ToDisplayString()?.StartsWith("System.Reflection", System.StringComparison.Ordinal) ?? false):
                        return true;
                }
            }

            return false;
        }
    }
}
