using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using OptiGraphMigrator.CodeFixes;
using Xunit;

namespace OptiGraphMigrator.Analyzers.Tests
{
    public class FindUsageAnalyzerTests
    {
        [Fact]
        public async Task Search_WithoutFindReference_ReportsHeuristicOgm902()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = client.Search<object>().Filter(x => x.Match("a")).GetResult();
                    }
                }
                """;

            var diagnostics = await FindTestHelper.GetAnalyzerDiagnosticsWithoutFindReferenceAsync<FindUsageAnalyzer>(source);

            Assert.Contains(diagnostics, d => d.Id == "OGM902");
        }

        [Fact]
        public async Task Filter_ReportsOgm001AndOffersCodeFix()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = {|#0:client.Search<object>().Filter(x => x.Match("a"))|}.GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Filter");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task For_ReportsOgm002()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = {|#0:client.Search<object>().For("hello")|}.GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("For");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task OrderBy_ReportsOgm003()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = {|#0:client.Search<object>().OrderBy(x => x)|}.GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("OrderBy");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task Boost_ReportsOgm202AsUntranslatable()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = {|#0:client.Search<object>().Boost(x => x, 2.0)|}.GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM202", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Boost");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task Boost_ReportsSuggestedApproachDiagnosticProperty()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = client.Search<object>().Boost(x => x, 2.0).GetResult();
                    }
                }
                """;

            var diagnostics = await FindTestHelper.GetAnalyzerDiagnosticsAsync<FindUsageAnalyzer>(source);

            var diagnostic = Assert.Single(diagnostics, d => d.Id == "OGM202");
            Assert.True(diagnostic.Properties.TryGetValue("SuggestedApproach", out var suggestedApproach));
            Assert.False(string.IsNullOrWhiteSpace(suggestedApproach));
        }

        [Fact]
        public async Task Filter_CodeFixRenamesToWhere()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = {|#0:client.Search<object>().Filter(x => x.Match("a"))|}.GetResult();
                    }
                }
                """;

            const string fixedSource = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var results = client.Search<object>().Where(x => x.Match("a")).GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Filter");

            await FindTestHelper.VerifyCodeFixAsync<FindUsageAnalyzer, FindChainCodeFixProvider>(
                source,
                fixedSource,
                expected);
        }

        [Fact]
        public async Task ChainSplitAcrossStatements_ResolvesAsSingleChain_NoUnresolvedDiagnostic()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client, bool applyFilter)
                    {
                        var query = client.Search<object>();

                        if (applyFilter)
                        {
                            query = {|#0:query.OrderBy(x => x)|};
                        }

                        var results = query.GetResult();
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("OrderBy");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task ChainReassignedThroughHelperMethod_ResolvesAsSingleChain_NoUnresolvedDiagnostic()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client)
                    {
                        var query = {|#0:client.Search<object>().OrderBy(x => x)|};

                        query = ApplyExtra(query);

                        var results = query.GetResult();
                    }

                    private static ITypeSearch<object> ApplyExtra(ITypeSearch<object> query)
                    {
                        return query;
                    }
                }
                """;

            var expected = new DiagnosticResult("OGM003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("OrderBy");

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }

        [Fact]
        public async Task ChainReassignedAcrossSeveralStatements_ResolvesAsSingleChain_NoUnresolvedDiagnostic()
        {
            const string source = """
                using EPiServer.Find;

                public class Sample
                {
                    public void Run(IClient client, bool applyText)
                    {
                        var query = client.Search<object>();

                        if (applyText)
                        {
                            query = {|#0:query.For("hello")|};
                        }

                        query = {|#1:query.OrderBy(x => x)|};
                        query = {|#2:query.Skip(10)|};
                        query = {|#3:query.Take(20)|};

                        var results = query.GetResult();
                    }
                }
                """;

            var expected = new[]
            {
                new DiagnosticResult("OGM002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(0).WithArguments("For"),
                new DiagnosticResult("OGM003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(1).WithArguments("OrderBy"),
                new DiagnosticResult("OGM006", Microsoft.CodeAnalysis.DiagnosticSeverity.Info).WithLocation(2).WithArguments("Skip"),
                new DiagnosticResult("OGM007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info).WithLocation(3).WithArguments("Take"),
            };

            await FindTestHelper.VerifyAnalyzerAsync<FindUsageAnalyzer>(source, expected);
        }
    }
}
