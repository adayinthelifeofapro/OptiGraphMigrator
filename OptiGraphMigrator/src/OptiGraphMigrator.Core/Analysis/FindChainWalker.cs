using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Analysis
{
    /// <summary>
    /// Resolves a Search &amp; Navigation fluent chain from any invocation inside it.
    /// </summary>
    /// <remarks>
    /// Find queries are arbitrarily long interleaved chains, so segments cannot be judged in
    /// isolation. The walker climbs to the outermost invocation of the chain, then walks back
    /// down through the receivers collecting every segment in source order. It also follows
    /// simple local-variable reassignment (<c>query = query.Foo()</c>) so chains split across
    /// multiple statements - a common pattern when a segment is only conditionally applied -
    /// still resolve as a single chain instead of several incomplete fragments.
    /// </remarks>
    public static class FindChainWalker
    {
        /// <summary>
        /// Builds the chain containing <paramref name="invocation"/>, or <c>null</c> when the
        /// invocation is not part of a recognisable Find chain.
        /// </summary>
        public static FindChain? TryResolve(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            FindSymbolIndex symbols,
            CancellationToken cancellationToken)
        {
            var outermost = GetOutermostInvocation(invocation);

            var reversed = new List<FindChainSegment>();
            var current = outermost;
            var visitedVariables = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            while (current is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbol = semanticModel.GetSymbolInfo(current, cancellationToken).Symbol as IMethodSymbol;
                if (symbol is null)
                {
                    // Unresolved symbol (missing reference or broken code). Stop climbing rather
                    // than guessing from syntax; the caller reports OGM901 for these.
                    break;
                }

                var reduced = symbol.ReducedFrom ?? symbol;
                var receiverType = ResolveReceiverType(current, semanticModel, cancellationToken);
                reversed.Add(new FindChainSegment(current, reduced, current.ArgumentList.Arguments, receiverType));

                var receiverInvocation = GetReceiverInvocation(current);
                if (receiverInvocation is null)
                {
                    receiverInvocation = TryFollowVariableReassignment(current, semanticModel, visitedVariables, cancellationToken);
                }

                current = receiverInvocation;
            }

            if (reversed.Count == 0)
            {
                return null;
            }

            // reversed currently runs terminal -> root; flip to source order.
            var segments = new List<FindChainSegment>(reversed.Count);
            for (var i = reversed.Count - 1; i >= 0; i--)
            {
                segments.Add(reversed[i]);
            }

            var root = FindRoot(segments, symbols);
            if (root is null)
            {
                return null;
            }

            var terminal = FindTerminal(segments, symbols);
            var searchedType = ResolveSearchedType(root);

            return new FindChain(segments, root, terminal, searchedType);
        }

        /// <summary>
        /// When <paramref name="invocation"/>'s receiver is a plain local variable (or
        /// parameter) rather than another invocation, looks for the nearest preceding
        /// assignment or declaration of that variable and, if its initializer is itself a Find
        /// invocation, continues climbing from there. Guards against infinite loops by tracking
        /// variables already followed.
        /// </summary>
        private static InvocationExpressionSyntax? TryFollowVariableReassignment(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            HashSet<ISymbol> visitedVariables,
            CancellationToken cancellationToken)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Expression is not IdentifierNameSyntax identifier)
            {
                return null;
            }

            var variableSymbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (variableSymbol is null || !visitedVariables.Add(variableSymbol))
            {
                return null;
            }

            var searchBefore = identifier.SpanStart;

            while (true)
            {
                var initializer = FindNearestInitializer(
                    identifier,
                    variableSymbol,
                    semanticModel,
                    cancellationToken,
                    position => position < searchBefore);

                if (initializer is null)
                {
                    return null;
                }

                // Only continue climbing when the initializer is itself a receiver-based call
                // (e.g. "x.Method(...)"), which can plausibly be another link in the same Find
                // chain. A bare call like "HelperMethod(query, ...)" has no receiver and is
                // opaque to this walker - treating it as a chain segment would corrupt
                // root/terminal detection for the outer chain, so keep searching further back
                // for an earlier assignment instead of following into it.
                if (initializer.Expression is MemberAccessExpressionSyntax)
                {
                    return GetOutermostInvocation(initializer);
                }

                searchBefore = initializer.SpanStart;
            }
        }

        /// <summary>
        /// Finds the enclosing member/lambda/local-function body and scans every assignment or
        /// variable declarator inside it (at any nesting level, so it also finds assignments
        /// inside <c>if</c>/<c>for</c>/etc. blocks) that binds <paramref name="variableSymbol"/>
        /// to an invocation expression, returning the one whose position best satisfies
        /// <paramref name="positionPredicate"/> (closest preceding, or first following).
        /// </summary>
        private static InvocationExpressionSyntax? FindNearestInitializer(
            SyntaxNode anchor,
            ISymbol variableSymbol,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            Func<int, bool> positionPredicate)
        {
            var scope = GetEnclosingBodyScope(anchor);
            if (scope is null)
            {
                return null;
            }

            InvocationExpressionSyntax? best = null;

            foreach (var node in scope.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                InvocationExpressionSyntax? candidate = node switch
                {
                    AssignmentExpressionSyntax { Left: IdentifierNameSyntax target, Right: InvocationExpressionSyntax rightInvocation }
                        when SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(target, cancellationToken).Symbol, variableSymbol) =>
                        rightInvocation,

                    VariableDeclaratorSyntax { Initializer.Value: InvocationExpressionSyntax declaredInvocation } declarator
                        when SymbolEqualityComparer.Default.Equals(semanticModel.GetDeclaredSymbol(declarator, cancellationToken), variableSymbol) =>
                        declaredInvocation,

                    _ => null,
                };

                if (candidate is null || !positionPredicate(candidate.SpanStart))
                {
                    continue;
                }

                if (best is null ||
                    (candidate.SpanStart < anchor.SpanStart
                        ? candidate.SpanStart > best.SpanStart
                        : candidate.SpanStart < best.SpanStart))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Finds the nearest ancestor body suitable for scanning (method, local function, lambda, accessor, etc.).</summary>
        private static SyntaxNode? GetEnclosingBodyScope(SyntaxNode node)
        {
            return node.Ancestors().FirstOrDefault(a =>
                a is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax);
        }

        /// <summary>
        /// True when <paramref name="invocation"/> (assumed to be the outermost invocation of a
        /// statement) is assigned to a local variable that is later re-used as the receiver of
        /// another invocation anywhere in the enclosing method body. In that case the later
        /// invocation's chain resolution will walk back through this one via
        /// <see cref="TryFollowVariableReassignment"/>, so the caller should skip analysing this
        /// invocation directly to avoid duplicate or fragmentary reports.
        /// </summary>
        public static bool IsSupersededByLaterVariableUsage(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var assignedVariable = GetAssignedVariableSymbol(invocation, semanticModel, cancellationToken);
            if (assignedVariable is null)
            {
                return false;
            }

            var scope = GetEnclosingBodyScope(invocation);
            if (scope is null)
            {
                return false;
            }

            foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (identifier.SpanStart <= invocation.SpanStart ||
                    identifier.Parent is not MemberAccessExpressionSyntax memberAccess ||
                    memberAccess.Expression != identifier)
                {
                    continue;
                }

                var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(symbol, assignedVariable))
                {
                    return true;
                }
            }

            return false;
        }

        private static ISymbol? GetAssignedVariableSymbol(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            switch (invocation.Parent)
            {
                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                    return semanticModel.GetDeclaredSymbol(declarator, cancellationToken);

                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } assignment
                    when assignment.Right == invocation:
                    return semanticModel.GetSymbolInfo(target, cancellationToken).Symbol;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Climbs out of nested member access so that <c>a.B().C().D()</c> resolves from any of
        /// B, C or D to the D invocation.
        /// </summary>
        public static InvocationExpressionSyntax GetOutermostInvocation(InvocationExpressionSyntax invocation)
        {
            var result = invocation;

            while (true)
            {
                // result is the receiver of an enclosing member access that is itself invoked.
                if (result.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression == result &&
                    memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
                {
                    result = parentInvocation;
                    continue;
                }

                return result;
            }
        }


        /// <summary>Gets the invocation that produced the receiver of <paramref name="invocation"/>.</summary>
        private static InvocationExpressionSyntax? GetReceiverInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression as InvocationExpressionSyntax;
            }

            return null;
        }

        /// <summary>
        /// Resolves the type of the expression a segment was invoked on. This is needed because
        /// segments store the unreduced extension method symbol, whose <c>ReceiverType</c> is the
        /// declaring static class rather than the extended Find type.
        /// </summary>
        private static ITypeSymbol? ResolveReceiverType(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type;
            }

            return null;
        }

        private static FindChainSegment? FindRoot(IReadOnlyList<FindChainSegment> segments, FindSymbolIndex symbols)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                if (symbols.IsQueryRoot(segments[i].Method))
                {
                    return segments[i];
                }
            }

            // A chain may start from a stored ITypeSearch<T> variable rather than IClient.Search<T>().
            // Treat the first segment invoked on a Find search type as the root.
            for (var i = 0; i < segments.Count; i++)
            {
                if (symbols.IsFindSearchType(segments[i].ReceiverType))
                {
                    return segments[i];
                }
            }

            return null;
        }

        private static FindChainSegment? FindTerminal(IReadOnlyList<FindChainSegment> segments, FindSymbolIndex symbols)
        {
            for (var i = segments.Count - 1; i >= 0; i--)
            {
                if (symbols.IsTerminalCall(segments[i].Method))
                {
                    return segments[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the searched content type from the root call's type arguments, for example
        /// <c>T</c> in <c>client.Search&lt;T&gt;()</c>.
        /// </summary>
        private static ITypeSymbol? ResolveSearchedType(FindChainSegment root)
        {
            if (!root.Method.TypeArguments.IsDefaultOrEmpty)
            {
                return root.Method.TypeArguments[0];
            }

            var receiver = root.ReceiverType as INamedTypeSymbol;
            if (receiver is not null && !receiver.TypeArguments.IsDefaultOrEmpty)
            {
                return receiver.TypeArguments[0];
            }

            var returnType = root.Method.ReturnType as INamedTypeSymbol;
            if (returnType is not null && !returnType.TypeArguments.IsDefaultOrEmpty)
            {
                return returnType.TypeArguments[0];
            }

            return null;
        }
    }
}
