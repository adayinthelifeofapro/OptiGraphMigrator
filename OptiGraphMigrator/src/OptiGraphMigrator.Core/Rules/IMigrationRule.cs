using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using OptiGraphMigrator.Core.Analysis;

namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Everything a rule needs to decide whether it applies to a particular construct.
    /// </summary>
    public sealed class RuleMatchContext
    {
        /// <summary>Creates a context.</summary>
        public RuleMatchContext(
            SemanticModel semanticModel,
            FindSymbolIndex symbols,
            FindChain chain,
            FindChainSegment segment,
            CancellationToken cancellationToken)
        {
            SemanticModel = semanticModel;
            Symbols = symbols;
            Chain = chain;
            Segment = segment;
            CancellationToken = cancellationToken;
        }

        /// <summary>Semantic model for the file under analysis.</summary>
        public SemanticModel SemanticModel { get; }

        /// <summary>Cached Find symbol lookups for the current compilation.</summary>
        public FindSymbolIndex Symbols { get; }

        /// <summary>The full fluent chain the segment belongs to.</summary>
        public FindChain Chain { get; }

        /// <summary>The segment currently being evaluated.</summary>
        public FindChainSegment Segment { get; }

        /// <summary>Cancellation for long running analysis.</summary>
        public CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// The outcome of a rule matching a construct.
    /// </summary>
    public sealed class RuleMatch
    {
        /// <summary>Creates a match.</summary>
        public RuleMatch(
            string ruleId,
            Location location,
            Translatability translatability,
            string graphEquivalent,
            IReadOnlyList<string> caveats,
            string? graphQlSnippet = null,
            IReadOnlyList<object?>? messageArguments = null)
        {
            RuleId = ruleId;
            Location = location;
            Translatability = translatability;
            GraphEquivalent = graphEquivalent;
            Caveats = caveats;
            GraphQlSnippet = graphQlSnippet;
            MessageArguments = messageArguments ?? new object?[0];
        }

        /// <summary>Id of the rule that produced this match.</summary>
        public string RuleId { get; }

        /// <summary>Where to report the diagnostic.</summary>
        public Location Location { get; }

        /// <summary>How cleanly this specific occurrence translates.</summary>
        public Translatability Translatability { get; }

        /// <summary>Graph SDK equivalent source text for this occurrence.</summary>
        public string GraphEquivalent { get; }

        /// <summary>Occurrence specific caveats, including any inherited from the rule.</summary>
        public IReadOnlyList<string> Caveats { get; }

        /// <summary>GraphQL fragment contributed by this occurrence, when one could be produced.</summary>
        public string? GraphQlSnippet { get; }

        /// <summary>Arguments substituted into the rule's message format.</summary>
        public IReadOnlyList<object?> MessageArguments { get; }
    }

    /// <summary>
    /// A hand written rule for patterns the declarative catalogue cannot express.
    /// </summary>
    public interface IMigrationRule
    {
        /// <summary>Stable diagnostic id this rule reports under.</summary>
        string Id { get; }

        /// <summary>Short human readable title, used to build the diagnostic descriptor.</summary>
        string Title { get; }

        /// <summary>Reporting severity.</summary>
        RuleSeverity Severity { get; }

        /// <summary>Logical grouping used by the reports.</summary>
        RuleCategory Category { get; }

        /// <summary>Message template shown in the diagnostic. <c>{0}</c> is the matched method name.</summary>
        string MessageFormat { get; }

        /// <summary>
        /// Cheap pre-filter. Returning false lets the engine skip the segment without
        /// building any further state.
        /// </summary>
        bool AppliesTo(RuleMatchContext context);

        /// <summary>
        /// Evaluates the segment. Returns <c>null</c> when the rule ultimately does not apply.
        /// </summary>
        RuleMatch? Evaluate(RuleMatchContext context);
    }
}
