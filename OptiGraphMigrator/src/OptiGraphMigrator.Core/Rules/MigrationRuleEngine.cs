using System;
using System.Collections.Generic;
using System.Linq;
using OptiGraphMigrator.Core.Analysis;

namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Evaluates a single <see cref="FindChainSegment"/> against both the declarative rule
    /// catalogue and the hand-written complex rules, and returns the single most specific
    /// <see cref="RuleMatch"/>.
    /// </summary>
    /// <remarks>
    /// Complex rules are consulted first because some of them (for example
    /// <see cref="Complex.InMemoryPredicateFilterRule"/>) exist specifically to override a
    /// declarative "exact" mapping for a subset of call shapes the catalogue cannot express.
    /// </remarks>
    public sealed class MigrationRuleEngine
    {
        private readonly RuleCatalogue _catalogue;
        private readonly IReadOnlyList<IMigrationRule> _complexRules;

        /// <summary>Creates an engine over the given catalogue and complex rule set.</summary>
        public MigrationRuleEngine(RuleCatalogue catalogue, IReadOnlyList<IMigrationRule>? complexRules = null)
        {
            _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            _complexRules = complexRules ?? ComplexRuleRegistry.ChainRules;
        }

        /// <summary>
        /// Evaluates every segment of <paramref name="chain"/> and returns the translations in
        /// segment order, one per segment (with a <c>null</c> match where nothing applied).
        /// </summary>
        public IReadOnlyList<Emit.SegmentTranslation> EvaluateChain(
            FindChain chain,
            Microsoft.CodeAnalysis.SemanticModel semanticModel,
            FindSymbolIndex symbols,
            System.Threading.CancellationToken cancellationToken)
        {
            var results = new List<Emit.SegmentTranslation>(chain.Segments.Count);

            foreach (var segment in chain.Segments)
            {
                var context = new RuleMatchContext(semanticModel, symbols, chain, segment, cancellationToken);
                var match = Evaluate(context);
                results.Add(new Emit.SegmentTranslation(segment, match));
            }

            return results;
        }

        /// <summary>Evaluates a single segment, returning the most specific match, if any.</summary>
        public RuleMatch? Evaluate(RuleMatchContext context)
        {
            foreach (var rule in _complexRules)
            {
                if (rule.AppliesTo(context))
                {
                    var match = rule.Evaluate(context);
                    if (match is not null)
                    {
                        return match;
                    }
                }
            }

            return EvaluateDeclarative(context);
        }

        private RuleMatch? EvaluateDeclarative(RuleMatchContext context)
        {
            var method = context.Segment.Method;
            var candidates = _catalogue.GetCandidates(method.Name);

            foreach (var rule in candidates)
            {
                var pattern = rule.FindSymbolPattern;

                if (!context.Symbols.MatchesContainingType(method, pattern.ContainingType, pattern.MatchDerivedTypes))
                {
                    continue;
                }

                var argCount = context.Segment.Arguments.Count;
                if (pattern.MinArguments is int min && argCount < min)
                {
                    continue;
                }

                if (pattern.MaxArguments is int max && argCount > max)
                {
                    continue;
                }

                return BuildMatch(rule, context);
            }

            return null;
        }

        private static RuleMatch BuildMatch(MigrationRule rule, RuleMatchContext context)
        {
            var methodName = context.Segment.MethodName;
            var snippet = string.IsNullOrEmpty(rule.GraphQlSnippetTemplate)
                ? null
                : string.Format(System.Globalization.CultureInfo.InvariantCulture, rule.GraphQlSnippetTemplate, methodName);

            return new RuleMatch(
                rule.Id,
                context.Segment.Invocation.GetLocation(),
                rule.Translatability,
                rule.GraphEquivalent,
                rule.Caveats.ToList(),
                graphQlSnippet: snippet,
                messageArguments: new object?[] { methodName });
        }
    }
}
