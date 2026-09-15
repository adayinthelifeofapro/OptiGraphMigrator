using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace OptiGraphMigrator.Analyzers.Tests
{
    /// <summary>
    /// Shared test scaffolding that wires the EPiServer.Find stub assembly into every
    /// analyzer/code-fix test so rule detection can be exercised against realistic call shapes.
    /// </summary>
    internal static class FindTestHelper
    {
        public static ReferenceAssemblies ReferenceAssemblies { get; } = ReferenceAssemblies.Net.Net80;

        public static async Task VerifyAnalyzerAsync<TAnalyzer>(string source, params DiagnosticResult[] expected)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            var test = new CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies,
            };

            AddFindReference(test.TestState.AdditionalReferences);
            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        public static async Task VerifyCodeFixAsync<TAnalyzer, TCodeFix>(
            string source,
            string fixedSource,
            params DiagnosticResult[] expected)
            where TAnalyzer : DiagnosticAnalyzer, new()
            where TCodeFix : CodeFixProvider, new()
        {
            var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies,
            };

            AddFindReference(test.TestState.AdditionalReferences);
            AddFindReference(test.FixedState.AdditionalReferences);
            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        public static async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync<TAnalyzer>(string source)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            var compilation = await BuildCompilationAsync(source);
            var analyzer = new TAnalyzer();
            var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

            var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
            return diagnostics;
        }

        /// <summary>
        /// Builds a compilation that has a <c>using EPiServer.Find;</c> directive but no
        /// EPiServer.Find metadata reference at all, mirroring a legacy CMS 11 project whose
        /// packages.config references were never restored. Used to exercise the analyzers'
        /// syntactic-only heuristic fallback path.
        /// </summary>
        public static async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsWithoutFindReferenceAsync<TAnalyzer>(string source)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            var references = new List<MetadataReference>();
            var resolvedAssemblies = await ReferenceAssemblies.ResolveAsync(LanguageNames.CSharp, System.Threading.CancellationToken.None);
            references.AddRange(resolvedAssemblies);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(
                "AnalyzerTestAssemblyNoFindRef",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzer = new TAnalyzer();
            var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

            return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        private static async Task<Compilation> BuildCompilationAsync(string source)
        {
            var references = new List<MetadataReference>();
            var resolvedAssemblies = await ReferenceAssemblies.ResolveAsync(LanguageNames.CSharp, System.Threading.CancellationToken.None);
            references.AddRange(resolvedAssemblies);
            AddFindReference(references);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(
                "AnalyzerTestAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation;
        }

        private static void AddFindReference(ICollection<MetadataReference> references)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(EPiServer.Find.IClient).Assembly.Location));
        }
    }
}
