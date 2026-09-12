using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using OptiGraphMigrator.Core.Analysis;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.Analyzers
{
    /// <summary>
    /// Reports every Search &amp; Navigation fluent query chain (<c>IClient.Search&lt;T&gt;()</c>,
    /// <c>UnifiedSearch()</c>, etc.), mapping each recognised segment to its Optimizely Graph
    /// equivalent, and flagging chains whose terminal call could not be resolved.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FindUsageAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> AllDescriptors = BuildDescriptors();

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => AllDescriptors;

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var symbols = FindSymbolIndex.Create(compilationStartContext.Compilation);
                if (!symbols.IsFindAssemblyReferenced)
                {
                    return;
                }

                var engine = new MigrationRuleEngine(RuleCatalogueLoader.Default);

                compilationStartContext.RegisterSyntaxNodeAction(
                    nodeContext => Analyze(nodeContext, symbols, engine),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context, FindSymbolIndex symbols, MigrationRuleEngine engine)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Only start chain resolution from the outermost invocation; otherwise every
            // segment of the same chain would independently re-resolve and re-report it.
            if (!ReferenceEquals(FindChainWalker.GetOutermostInvocation(invocation), invocation))
            {
                return;
            }

            // Skip fragments whose result is stashed in a variable that gets extended by a later
            // statement (e.g. `query = query.Foo();`); the later statement's chain resolution
            // walks back through this one, so reporting here too would duplicate diagnostics or
            // flag an intermediate, incomplete fragment as unresolved.
            if (FindChainWalker.IsSupersededByLaterVariableUsage(invocation, context.SemanticModel, context.CancellationToken))
            {
                return;
            }

            var chain = FindChainWalker.TryResolve(invocation, context.SemanticModel, symbols, context.CancellationToken);
            if (chain is null)
            {
                return;
            }

            if (chain.Root is not null && chain.Terminal is null)
            {
                var location = chain.Span?.GetLocation() ?? invocation.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnresolvedChain, location));
            }

            var translations = engine.EvaluateChain(chain, context.SemanticModel, symbols, context.CancellationToken);

            foreach (var translation in translations)
            {
                var match = translation.Match;
                if (match is null)
                {
                    continue;
                }

                var descriptor = ResolveDescriptor(match.RuleId);
                if (descriptor is null)
                {
                    continue;
                }

                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add("RuleId", match.RuleId);
                properties.Add("GraphEquivalent", match.GraphEquivalent);
                properties.Add("GraphQlSnippet", match.GraphQlSnippet ?? string.Empty);
                properties.Add("Translatability", match.Translatability.ToString());

                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    match.Location,
                    properties.ToImmutable(),
                    match.MessageArguments.ToArray()));
            }
        }

        private static DiagnosticDescriptor? ResolveDescriptor(string ruleId)
        {
            var declarativeRule = RuleCatalogueLoader.Default.GetById(ruleId);
            if (declarativeRule is not null)
            {
                return DiagnosticDescriptors.GetOrCreate(declarativeRule);
            }

            var complexRule = ComplexRuleRegistry.All.FirstOrDefault(r => r.Id == ruleId);
            return complexRule is not null ? DiagnosticDescriptors.GetOrCreate(complexRule) : null;
        }

        private static ImmutableArray<DiagnosticDescriptor> BuildDescriptors()
        {
            var builder = ImmutableArray.CreateBuilder<DiagnosticDescriptor>();

            foreach (var rule in RuleCatalogueLoader.Default.Rules)
            {
                builder.Add(DiagnosticDescriptors.GetOrCreate(rule));
            }

            foreach (var rule in ComplexRuleRegistry.All)
            {
                builder.Add(DiagnosticDescriptors.GetOrCreate(rule));
            }

            builder.Add(DiagnosticDescriptors.UnresolvedChain);

            return builder.ToImmutable();
        }
    }
}
