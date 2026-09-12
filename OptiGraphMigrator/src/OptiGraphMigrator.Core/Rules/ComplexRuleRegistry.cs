using System.Collections.Generic;
using System.Linq;
using OptiGraphMigrator.Core.Rules.Complex;

namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Aggregates every hand-written <see cref="IMigrationRule"/> so analysers and the CLI do
    /// not need to know the concrete rule types.
    /// </summary>
    public static class ComplexRuleRegistry
    {
        /// <summary>
        /// The two configuration-style rules that fire directly on a single invocation and are
        /// evaluated outside the fluent chain walker (they are not part of a query chain).
        /// </summary>
        public static ClientConventionsRule ClientConventions { get; } = new ClientConventionsRule();

        /// <summary>See <see cref="ClientConventions"/>.</summary>
        public static ContentLoaderFindHelperRule ContentLoaderHelper { get; } = new ContentLoaderFindHelperRule();

        /// <summary>See <see cref="ClientConventions"/>.</summary>
        public static IndexingApiRule IndexingApi { get; } = new IndexingApiRule();

        /// <summary>
        /// Rules evaluated per segment of a resolved <see cref="Analysis.FindChain"/> by
        /// <see cref="MigrationRuleEngine"/>.
        /// </summary>
        public static IReadOnlyList<IMigrationRule> ChainRules { get; } = new List<IMigrationRule>
        {
            new FilterBuilderCompositionRule(),
            new UnifiedSearchMultiTypeRule(),
            new StatisticsTrackingRule(),
            new CustomScoringRule(),
            new SynonymsRule(),
            new InMemoryPredicateFilterRule(),
            new ReflectionDrivenQueryRule(),
            new FieldFilterOperatorRule(),
            new StaticClientAccessorRule(),
        };

        /// <summary>Every hand-written rule, chain-based and standalone alike.</summary>
        public static IReadOnlyList<IMigrationRule> All { get; } = ChainRules
            .Concat(new IMigrationRule[] { ClientConventions, ContentLoaderHelper, IndexingApi })
            .ToList();
    }
}
