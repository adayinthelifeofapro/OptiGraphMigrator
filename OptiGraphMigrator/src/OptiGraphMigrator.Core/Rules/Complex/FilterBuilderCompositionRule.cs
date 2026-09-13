using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags boolean composition performed through a custom <c>IFilterBuilder</c> (for example
    /// <c>filterBuilder.And(...)</c> / <c>.Or(...)</c> / <c>.Not(...)</c>).
    /// </summary>
    /// <remarks>
    /// A mapping to a Graph 'where' clause exists, but the operator precedence and grouping of
    /// hand-rolled filter builders is easy to get subtly wrong, so this is reported as a caveat
    /// rather than auto-fixed.
    /// </remarks>
    public sealed class FilterBuilderCompositionRule : IMigrationRule
    {
        private static readonly HashSet<string> CompositionMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "And",
            "Or",
            "Not",
            "MatchAll",
            "MatchNone",
        };

        /// <inheritdoc />
        public string Id => "OGM101";

        /// <inheritdoc />
        public string Title => "Custom IFilterBuilder composition requires manual review";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Filtering;

        /// <inheritdoc />
        public string MessageFormat => "Find filter builder '{0}(...)' can be translated to a Graph 'where' clause, but requires manual review";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            if (!CompositionMethodNames.Contains(context.Segment.MethodName))
            {
                return false;
            }

            return context.Symbols.IsAssignableTo(context.Segment.ReceiverType, context.Symbols.FilterBuilderType);
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
                "Custom IFilterBuilder composition (And/Or/Not) must be re-expressed as a single Graph 'where' " +
                "clause; nested boolean grouping and precedence should be re-verified against the Graph schema.",
            };

            const string suggestedApproach =
                "Rebuild the boolean tree explicitly with the Graph SDK's And/Or/Not builders, keeping each " +
                "original grouping as its own nested block rather than flattening it - Find's builder chains " +
                "and Graph's AND/OR arrays differ in how they imply precedence. Extract the composition into a " +
                "small method so it can be unit tested against a few representative filter combinations.";

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                ".Where(x => <composed boolean predicate>)",
                caveats,
                graphQlSnippet: "where: { AND: [ /* composed conditions */ ] }",
                messageArguments: new object?[] { context.Segment.MethodName },
                suggestedApproach: suggestedApproach);
        }
    }
}
