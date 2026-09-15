; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
OGM001 | OptiGraphMigrator.Filtering | Warning | Filter(...) maps to Graph where(...)
OGM002 | OptiGraphMigrator.Query | Warning | For(text) maps to Graph query(...)
OGM003 | OptiGraphMigrator.Query | Warning | OrderBy(...) maps to Graph orderBy(...)
OGM004 | OptiGraphMigrator.Query | Warning | OrderByDescending(...) maps to Graph orderBy(...)
OGM005 | OptiGraphMigrator.Query | Warning | ThenBy(...) maps to Graph orderBy(...) secondary sort
OGM006 | OptiGraphMigrator.Query | Info | Skip(...) maps to Graph skip(...)
OGM007 | OptiGraphMigrator.Query | Info | Take(...) maps to Graph limit(...)
OGM008 | OptiGraphMigrator.Projection | Warning | Select(...) projection maps to Graph selection set
OGM009 | OptiGraphMigrator.Statistics | Warning | Track() analytics call has no Graph SDK equivalent
OGM010 | OptiGraphMigrator.Query | Warning | StaticallyCacheFor(...) maps to Graph client-side caching
OGM011 | OptiGraphMigrator.Filtering | Warning | FilterForVisitor() personalization maps to Graph visitor group filtering
OGM012 | OptiGraphMigrator.Query | Info | GetContentResult() maps to a Graph query execution
OGM013 | OptiGraphMigrator.Projection | Warning | GetFacets(...) maps to Graph facets query
OGM014 | OptiGraphMigrator.Projection | Warning | Highlight(...) maps to Graph highlight selection
OGM015 | OptiGraphMigrator.Filtering | Warning | Range(...) maps to Graph range filter
OGM016 | OptiGraphMigrator.Query | Warning | Language(...) maps to Graph language selector
OGM017 | OptiGraphMigrator.Query | Info | TotalMatching() maps to Graph total count field
OGM018 | OptiGraphMigrator.Query | Error | BestBets(...) has no direct Graph equivalent
OGM019 | OptiGraphMigrator.Query | Error | DidYouMean() has no direct Graph equivalent
OGM020 | OptiGraphMigrator.Query | Error | Autocomplete() has no direct Graph equivalent
OGM021 | OptiGraphMigrator.Filtering | Error | DistanceFrom(...) geo/spatial filter has no direct Graph equivalent
OGM022 | OptiGraphMigrator.Query | Info | GetContentResultAsync() maps to an async Graph query execution
OGM023 | OptiGraphMigrator.Query | Error | MoreLikeThis(...) similarity query has no direct Graph equivalent
OGM024 | OptiGraphMigrator.Query | Warning | Search<T>(CultureInfo) language-scoped query root requires manual review
OGM025 | OptiGraphMigrator.Query | Error | RemoveDuplicates() deduplication has no direct Graph equivalent
OGM026 | OptiGraphMigrator.Query | Error | MinScore(...) relevance threshold has no direct Graph equivalent
OGM027 | OptiGraphMigrator.Filtering | Warning | MatchContained(...) content-tree ancestor filter maps to Graph ancestor 'where' clause
OGM028 | OptiGraphMigrator.Filtering | Warning | ForVisitorGroup(...) personalization maps to Graph visitor group filtering
OGM029 | OptiGraphMigrator.Query | Error | AverageOf(...) statistical aggregation has no direct Graph equivalent
OGM030 | OptiGraphMigrator.Query | Error | SumOf(...) statistical aggregation has no direct Graph equivalent
OGM031 | OptiGraphMigrator.Query | Error | MaximumOf(...) statistical aggregation has no direct Graph equivalent
OGM032 | OptiGraphMigrator.Query | Error | MinimumOf(...) statistical aggregation has no direct Graph equivalent
OGM033 | OptiGraphMigrator.Query | Warning | Fuzzy(...) fuzzy-matching query modifier requires manual review
OGM034 | OptiGraphMigrator.Projection | Warning | TermsFacetFor/RangeFacetFor facet-builder detail requires manual review
OGM108 | OptiGraphMigrator.Configuration | Warning | SearchClient.Instance static singleton has no Graph SDK equivalent
OGM101 | OptiGraphMigrator.Filtering | Warning | Custom IFilterBuilder composition requires manual review
OGM102 | OptiGraphMigrator.UnifiedSearch | Warning | UnifiedSearch has no single-query Graph equivalent
OGM103 | OptiGraphMigrator.Configuration | Warning | Find indexing conventions must move to Graph schema configuration
OGM104 | OptiGraphMigrator.ContentLoading | Warning | Find-backed IContentLoader helper should move to an explicit Graph query
OGM107 | OptiGraphMigrator.Configuration | Error | Find push-based indexing calls have no Graph SDK equivalent
OGM201 | OptiGraphMigrator.Statistics | Warning | Search statistics and popularity tracking have no Graph equivalent
OGM202 | OptiGraphMigrator.Query | Warning | Custom relevance scoring has no Graph equivalent
OGM203 | OptiGraphMigrator.Query | Warning | Query-time synonym expansion has no Graph equivalent
OGM204 | OptiGraphMigrator.Filtering | Warning | In-memory Filter() predicate has no clean Graph translation
OGM205 | OptiGraphMigrator.Query | Warning | Reflection-driven query construction has no clean Graph translation
OGM207 | OptiGraphMigrator.Filtering | Warning | Filter() field operator has subtly different Graph 'where' semantics
OGM901 | OptiGraphMigrator.Infrastructure | Info | Find query chain could not be fully resolved
OGM902 | OptiGraphMigrator.Heuristic | Info | Possible Find usage detected heuristically
OGM035 | OptiGraphMigrator.Filtering | Warning | FilterOnCurrentSite() maps to Graph site-scoped where(...)
OGM036 | OptiGraphMigrator.Projection | Warning | FindPageData/FindContentData projection has no direct Graph equivalent
OGM037 | OptiGraphMigrator.Filtering | Warning | FilterOnLanguages(...) maps to Graph language where(...)
OGM038 | OptiGraphMigrator.Filtering | Warning | PublishedInLanguage(...) maps to Graph language where(...)
OGM039 | OptiGraphMigrator.Filtering | Warning | ExcludeDeleted() maps to Graph deletion-status where(...)
