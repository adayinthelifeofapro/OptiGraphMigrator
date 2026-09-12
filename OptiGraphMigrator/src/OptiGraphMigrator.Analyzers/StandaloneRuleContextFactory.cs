using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OptiGraphMigrator.Core.Analysis;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.Analyzers
{
    /// <summary>
    /// Builds a single-segment <see cref="RuleMatchContext"/> for rules that are evaluated
    /// against one invocation directly, without needing a resolved fluent
    /// <see cref="FindChain"/> around it.
    /// </summary>
    internal static class StandaloneRuleContextFactory
    {
        /// <summary>
        /// Resolves <paramref name="invocation"/>'s method symbol and receiver type and wraps
        /// them in a degenerate, single-segment chain. Returns <c>null</c> when the invocation's
        /// method symbol could not be resolved.
        /// </summary>
        public static RuleMatchContext? TryCreate(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            FindSymbolIndex symbols,
            System.Threading.CancellationToken cancellationToken)
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (method is null)
            {
                return null;
            }

            var reduced = method.ReducedFrom ?? method;

            ITypeSymbol? receiverType = null;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type;
            }

            var segment = new FindChainSegment(invocation, reduced, invocation.ArgumentList.Arguments, receiverType);
            var chain = new FindChain(new List<FindChainSegment> { segment }, segment, segment, searchedType: null);

            return new RuleMatchContext(semanticModel, symbols, chain, segment, cancellationToken);
        }
    }
}
