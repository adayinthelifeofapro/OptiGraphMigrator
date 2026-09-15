using System;
using System.Linq;
using EPiServer;
using EPiServer.Find;
using EPiServer.Find.Cms;

namespace SampleFindSolutionCms11
{
    public class Article
    {
        public string Title { get; set; }

        public DateTime PublishDate { get; set; }
    }

    /// <summary>
    /// Legacy (CMS 11 style) Find usage. This project intentionally has no restored
    /// EPiServer.Find/EPiServer.CMS.Core packages on disk (see the HintPath references in
    /// SampleFindSolutionCms11.csproj), so MSBuild/Roslyn cannot resolve the Find symbols here.
    /// It exists to exercise OptiGraphMigrator's source-only heuristic scan fallback, which
    /// recognises Find usage syntactically via the `using EPiServer.Find*` directives and
    /// well-known method/type names rather than semantic symbol resolution.
    /// </summary>
    public class LegacyFindUsageSamples
    {
        public void RunQuery()
        {
            var client = SearchClient.Instance;

            var result = client.Search<Article>()
                .Filter(a => a.PublishDate < DateTime.Now)
                .FilterForVisitor()
                .OrderByDescending(a => a.PublishDate)
                .Take(10)
                .GetPagesResult();

            Console.WriteLine(result.Count());
        }
    }
}
