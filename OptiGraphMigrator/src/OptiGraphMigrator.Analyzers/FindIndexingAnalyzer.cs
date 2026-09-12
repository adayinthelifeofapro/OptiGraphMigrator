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
    /// Reports Find's push-based indexing calls (<c>Index</c>, <c>UpdateIndex</c>,
    /// <c>DeleteIndex</c>) made directly on <c>IClient</c>.
    /// </summary>
    /// <remarks>
    /// Evaluated per invocation rather than through the fluent chain walker, since these calls
    /// are terminal, single-invocation calls on <c>IClient</c> rather than a query chain.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FindIndexingAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors = ImmutableArray.Create(
            DiagnosticDescriptors.GetOrCreate(ComplexRuleRegistry.IndexingApi));

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors;

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

                compilationStartContext.RegisterSyntaxNodeAction(
                    nodeContext => Analyze(nodeContext, symbols),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context, FindSymbolIndex symbols)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var ruleContext = StandaloneRuleContextFactory.TryCreate(invocation, context.SemanticModel, symbols, context.CancellationToken);
            if (ruleContext is null)
            {
                return;
            }

            var rule = ComplexRuleRegistry.IndexingApi;
            if (!rule.AppliesTo(ruleContext))
            {
                return;
            }

            var match = rule.Evaluate(ruleContext);
            if (match is null)
            {
                return;
            }

            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("RuleId", match.RuleId);
            properties.Add("GraphEquivalent", match.GraphEquivalent);
            properties.Add("GraphQlSnippet", match.GraphQlSnippet ?? string.Empty);
            properties.Add("Translatability", match.Translatability.ToString());

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GetOrCreate(rule),
                match.Location,
                properties.ToImmutable(),
                match.MessageArguments.ToArray()));
        }
    }
}
