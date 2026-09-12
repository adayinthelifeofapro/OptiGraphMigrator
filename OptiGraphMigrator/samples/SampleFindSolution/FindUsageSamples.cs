using System;
using System.Linq;
using EPiServer;
using EPiServer.Find;
using EPiServer.Find.Api.Querying;
using EPiServer.Find.Cms;

namespace SampleFindSolution
{
    public class Article
    {
        public string Title { get; set; }

        public DateTime PublishDate { get; set; }

        public string Body { get; set; }
    }

    /// <summary>
    /// Representative Find usage covering every OptiGraphMigrator rule category: exact
    /// mappings (OGM001-007, OGM012), caveat mappings (OGM008, OGM010, OGM011), blocked
    /// patterns (OGM009), configuration (OGM103), IContentLoader helpers (OGM104), and the
    /// hand-written complex rules (OGM101, OGM102, OGM201-205).
    /// </summary>
    public class FindUsageSamples
    {
        private readonly IClient _client;
        private readonly IContentLoader _contentLoader;

        public FindUsageSamples(IClient client, IContentLoader contentLoader)
        {
            _client = client;
            _contentLoader = contentLoader;
        }

        // OGM001, OGM003, OGM006, OGM007, OGM012: Filter/OrderBy/Skip/Take/GetContentResult chain.
        public void FilterOrderSkipTake()
        {
            var results = _client.Search<Article>()
                .Filter(a => IFilterBuilder.MatchAll())
                .OrderBy(a => a.PublishDate)
                .Skip(10)
                .Take(20)
                .GetContentResult();
        }

        // OGM002: For(text) full text query.
        public void FullTextQuery()
        {
            var results = _client.Search<Article>()
                .For("optimizely graph")
                .GetContentResult();
        }

        // OGM004, OGM005: OrderByDescending + ThenBy secondary sort.
        public void MultiFieldSort()
        {
            var results = _client.Search<Article>()
                .OrderByDescending(a => a.PublishDate)
                .ThenBy(a => a.Title)
                .GetContentResult();
        }

        // OGM008: Select() projection.
        public void ProjectTitles()
        {
            var titles = _client.Search<Article>()
                .Select(a => a.Title)
                .GetContentResult();
        }

        // OGM009: Track() analytics call has no Graph equivalent (blocked).
        public void TrackSearch()
        {
            var results = _client.Search<Article>().GetContentResult();
            _client.Track("optimizely graph", results);
        }

        // OGM010: StaticallyCacheFor() caching has no built-in Graph equivalent.
        public void CachedQuery()
        {
            var results = _client.Search<Article>()
                .StaticallyCacheFor(TimeSpan.FromMinutes(10))
                .GetContentResult();
        }

        // OGM011: FilterForVisitor() personalization requires manual review.
        public void PersonalizedQuery()
        {
            var results = _client.Search<Article>()
                .FilterForVisitor()
                .GetContentResult();
        }

        // OGM101: Composed IFilterBuilder expressions (And/Or/Not).
        public void ComposedFilters()
        {
            var results = _client.Search<Article>()
                .Filter(a => IFilterBuilder.MatchAll().And(IFilterBuilder.MatchNone()).Not())
                .GetContentResult();
        }

        // OGM102: UnifiedSearch across multiple content types.
        public void UnifiedSearchAcrossTypes()
        {
            var results = _client.UnifiedSearch()
                .For<Article>()
                .For<object>();
        }

        // OGM103: Client indexing conventions configuration.
        public void ConfigureConventions()
        {
            _client.Conventions
                .ForInstancesOf<Article>()
                .ShouldIndex(a => true)
                .IncludeField(a => ((Article)a).Body);
        }

        // OGM104: IContentLoader Find-backed helper methods.
        public void ContentLoaderHelpers()
        {
            var byType = _contentLoader.FindByContentType<Article>();
            var withCriteria = _contentLoader.FindPagesWithCriteria<Article>(new object());
            var children = _contentLoader.GetChildrenWithFind<Article>(ContentReference.StartPage);
        }

        // OGM201: Custom scoring (Boost/CustomScore/FunctionScore/InField).
        public void CustomScoring()
        {
            var results = _client.Search<Article>()
                .Boost(a => a.Title, 2.0)
                .CustomScore("recency_script")
                .GetContentResult();
        }

        // OGM202/OGM203: Synonyms configuration on a query.
        public void SynonymSearch()
        {
            var results = _client.Search<Article>()
                .UsingSynonyms("editorial-synonyms")
                .GetContentResult();
        }

        // OGM204: In-memory predicate filter that cannot be pushed down to the index.
        public void InMemoryPredicateFilter()
        {
            var results = _client.Search<Article>()
                .GetContentResult()
                .Cast<Article>()
                .Where(a => ComputeRelevance(a) > 0.5);
        }

        // OGM205: Reflection-driven dynamic query construction.
        public void ReflectionDrivenQuery()
        {
            var articleType = typeof(Article);
            var results = _client.Search<Article>()
                .Filter(a => IFilterBuilder.MatchAll())
                .GetContentResult();
        }

        // OGM013: GetFacets(...) faceted navigation.
        public void FacetedNavigation()
        {
            var results = _client.Search<Article>()
                .GetFacets(a => a.Title)
                .GetContentResult();
        }

        // OGM014: Highlight(...) search result highlighting.
        public void HighlightedResults()
        {
            var results = _client.Search<Article>()
                .Highlight(a => a.Body)
                .GetContentResult();
        }

        // OGM015: Range(...) numeric/date range filter.
        public void RangeFilteredQuery()
        {
            var results = _client.Search<Article>()
                .Range(a => a.PublishDate, DateTime.MinValue, DateTime.MaxValue)
                .GetContentResult();
        }

        // OGM016: Language(...) language-scoped query.
        public void LanguageScopedQuery()
        {
            var results = _client.Search<Article>()
                .Language("en")
                .GetContentResult();
        }

        // OGM017: TotalMatching() total hit count.
        public void TotalHitCount()
        {
            var total = _client.Search<Article>().TotalMatching();
        }

        // OGM018: BestBets(...) editorial promotions (blocked).
        public void EditorialBestBets()
        {
            var results = _client.Search<Article>()
                .BestBets("optimizely graph")
                .GetContentResult();
        }

        // OGM019, OGM020: DidYouMean() / Autocomplete() suggestion APIs (blocked).
        public void SuggestionQueries()
        {
            var suggestion = _client.Search<Article>().DidYouMean("optimizly");
            var autocomplete = _client.Search<Article>().Autocomplete("opti");
        }

        // OGM021: DistanceFrom(...) geo/spatial filter (blocked).
        public void GeoSpatialQuery()
        {
            var results = _client.Search<Article>()
                .DistanceFrom(59.33, 18.06)
                .GetContentResult();
        }

        // OGM022: GetContentResultAsync() async query execution.
        public async System.Threading.Tasks.Task AsyncQueryAsync()
        {
            var results = await _client.Search<Article>()
                .GetContentResultAsync();
        }

        // OGM023: MoreLikeThis(...) content similarity query (blocked).
        public void SimilarArticles(Article seed)
        {
            var results = _client.Search<Article>()
                .MoreLikeThis(seed)
                .GetContentResult();
        }

        // OGM024: Search<T>(CultureInfo) language-scoped query root.
        public void LanguageScopedSearchRoot()
        {
            var results = _client.Search<Article>(new System.Globalization.CultureInfo("en"))
                .GetContentResult();
        }

        // OGM107: Direct push-based indexing calls (blocked).
        public void ManualIndexing(Article article)
        {
            _client.Index(article);
            _client.UpdateIndex(article);
            _client.DeleteIndex(article);
        }

        // OGM207: Field-level filter operators inside Filter() require manual review.
        public void FieldOperatorFilters()
        {
            var results = _client.Search<Article>()
                .Filter(a => EPiServer.Find.Api.Querying.FieldFilterExtensions.StartsWith(a.Title, "optimizely"))
                .GetContentResult();
        }

        // OGM025: RemoveDuplicates() deduplication (blocked).
        public void DeduplicatedQuery()
        {
            var results = _client.Search<Article>()
                .RemoveDuplicates(a => a.Title)
                .GetContentResult();
        }

        // OGM026: MinScore(...) relevance threshold (blocked).
        public void MinRelevanceQuery()
        {
            var results = _client.Search<Article>()
                .MinScore(0.5)
                .GetContentResult();
        }

        // OGM027: MatchContained(...) content-tree ancestor filter requires manual review.
        public void ContentTreeFilteredQuery()
        {
            var results = _client.Search<Article>()
                .Filter(a => EPiServer.Find.Cms.ContentTreeExtensions.MatchContained(_client.Search<Article>(), ContentReference.StartPage))
                .GetContentResult();
        }

        // OGM028: ForVisitorGroup(...) personalization requires manual review.
        public void VisitorGroupQuery()
        {
            var results = _client.Search<Article>()
                .ForVisitorGroup("premium-members")
                .GetContentResult();
        }

        // OGM029-OGM032: Statistical aggregation extensions (blocked).
        public void AggregatedQuery()
        {
            var average = _client.Search<Article>().AverageOf(a => a.Title!.Length);
            var sum = _client.Search<Article>().SumOf(a => a.Title!.Length);
            var max = _client.Search<Article>().MaximumOf(a => a.Title!.Length);
            var min = _client.Search<Article>().MinimumOf(a => a.Title!.Length);
        }

        // OGM033: Fuzzy(...) fuzzy-matching query modifier requires manual review.
        public void FuzzyMatchedQuery()
        {
            var results = _client.Search<Article>()
                .For("optimizly")
                .Fuzzy(0.7)
                .GetContentResult();
        }

        // OGM034: TermsFacetFor/RangeFacetFor facet-builder detail requires manual review.
        public void DetailedFacetedQuery()
        {
            var results = _client.Search<Article>()
                .TermsFacetFor(a => a.Title, 20)
                .GetContentResult();
        }

        // OGM108: SearchClient.Instance static singleton has no Graph SDK equivalent.
        public void StaticSingletonQuery()
        {
            var results = EPiServer.Find.SearchClient.Instance.Search<Article>()
                .GetContentResult();
        }

        // Regression sample: chain built across multiple statements via variable reassignment.
        // Should resolve as a single complete chain (OGM003 + OGM021), not report OGM901.
        public void ConditionallyExtendedQuery(bool applyGeoFilter, double latitude, double longitude)
        {
            var query = _client.Search<Article>();

            if (applyGeoFilter)
            {
                query = query.OrderBy(a => a.Title).DistanceFrom(latitude, longitude);
            }

            var results = query.GetContentResult();
        }

        // Regression sample: chain re-routed through an opaque helper method that reassigns the
        // query variable (a bare call, not a fluent "x.Method(...)" continuation). The walker
        // must not treat the helper call itself as a chain segment, and must keep searching
        // further back for the actual Find root instead of giving up as unresolved.
        public void QueryExtendedThroughHelperMethod()
        {
            var query = _client.Search<Article>()
                .Filter(a => a.Title!.Match("known"))
                .For("optimizely");

            query = ApplyExtraFilter(query);

            var results = query.GetContentResult();
        }

        private static ITypeSearch<Article> ApplyExtraFilter(ITypeSearch<Article> query)
        {
            return query.Filter(a => a.Title!.Match("extra"));
        }

        private static double ComputeRelevance(Article article) => article.Title?.Length ?? 0;
    }
}
