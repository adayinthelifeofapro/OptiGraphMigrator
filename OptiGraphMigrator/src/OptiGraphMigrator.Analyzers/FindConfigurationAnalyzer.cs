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
    /// Reports Find indexing convention configuration (<c>ClientConventions</c>,
    /// <c>ShouldIndex</c>, <c>IncludeField</c>, <c>ForInstancesOf</c>) that must move to
    /// Optimizely Graph's schema configuration.
    /// </summary>
    /// <remarks>
    /// Convention calls are evaluated per invocation rather than through the fluent chain
    /// walker: their receiver (<c>ClientConventions</c>) is not one of the query-root types the
    /// walker recognises, so a chain would never resolve for them.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FindConfigurationAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors = ImmutableArray.Create(
            DiagnosticDescriptors.GetOrCreate(ComplexRuleRegistry.ClientConventions));

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

            var context2 = StandaloneRuleContextFactory.TryCreate(invocation, context.SemanticModel, symbols, context.CancellationToken);
            if (context2 is null)
            {
                return;
            }

            var rule = ComplexRuleRegistry.ClientConventions;
            if (!rule.AppliesTo(context2))
            {
                return;
            }

            var match = rule.Evaluate(context2);
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
