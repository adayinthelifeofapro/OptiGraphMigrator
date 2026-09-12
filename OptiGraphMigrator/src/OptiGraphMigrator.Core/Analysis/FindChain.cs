using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OptiGraphMigrator.Core.Analysis
{
    /// <summary>
    /// A single link in a Search &amp; Navigation fluent chain, for example <c>.Filter(...)</c>.
    /// </summary>
    public sealed class FindChainSegment
    {
        /// <summary>Creates a segment.</summary>
        public FindChainSegment(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            IReadOnlyList<ArgumentSyntax> arguments,
            ITypeSymbol? receiverType = null)
        {
            Invocation = invocation;
            Method = method;
            Arguments = arguments;
            ReceiverType = receiverType;
        }

        /// <summary>The invocation node for this segment.</summary>
        public InvocationExpressionSyntax Invocation { get; }

        /// <summary>
        /// The resolved method symbol. Extension methods are stored in their unreduced (static)
        /// form so <see cref="ISymbol.ContainingType"/> is the declaring static class, matching
        /// how the rule catalogue expresses containing types.
        /// </summary>
        public IMethodSymbol Method { get; }

        /// <summary>
        /// Type of the expression the method was invoked on, resolved from the receiver syntax.
        /// For extension methods this is the extended type, not the declaring static class.
        /// </summary>
        public ITypeSymbol? ReceiverType { get; }

        /// <summary>Arguments supplied at the call site.</summary>
        public IReadOnlyList<ArgumentSyntax> Arguments { get; }

        /// <summary>Simple name of the invoked method.</summary>
        public string MethodName => Method.Name;

        /// <summary>The lambda passed as the first argument, when present.</summary>
        public LambdaExpressionSyntax? FirstLambda
        {
            get
            {
                for (var i = 0; i < Arguments.Count; i++)
                {
                    if (Arguments[i].Expression is LambdaExpressionSyntax lambda)
                    {
                        return lambda;
                    }
                }

                return null;
            }
        }
    }

    /// <summary>
    /// An ordered Search &amp; Navigation fluent chain, from the query root through to the
    /// terminal materialising call.
    /// </summary>
    public sealed class FindChain
    {
        /// <summary>Creates a chain.</summary>
        public FindChain(
            IReadOnlyList<FindChainSegment> segments,
            FindChainSegment? root,
            FindChainSegment? terminal,
            ITypeSymbol? searchedType)
        {
            Segments = segments;
            Root = root;
            Terminal = terminal;
            SearchedType = searchedType;
        }

        /// <summary>Segments in source order, outermost receiver first.</summary>
        public IReadOnlyList<FindChainSegment> Segments { get; }

        /// <summary>The query root, for example <c>IClient.Search&lt;T&gt;()</c>.</summary>
        public FindChainSegment? Root { get; }

        /// <summary>The materialising call, for example <c>GetContentResult()</c>.</summary>
        public FindChainSegment? Terminal { get; }

        /// <summary>The content type being searched, when it could be resolved.</summary>
        public ITypeSymbol? SearchedType { get; }

        /// <summary>True when both ends of the chain were resolved.</summary>
        public bool IsComplete => Root is not null && Terminal is not null;

        /// <summary>The syntax node spanning the whole chain.</summary>
        public SyntaxNode? Span => Terminal?.Invocation ?? Root?.Invocation;
    }
}
