namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Reporting severity for a migration rule. Kept independent of Roslyn's
    /// <c>DiagnosticSeverity</c> so the catalogue stays portable.
    /// </summary>
    public enum RuleSeverity
    {
        /// <summary>Informational only.</summary>
        Info = 0,

        /// <summary>Requires attention during migration.</summary>
        Warning = 1,

        /// <summary>Blocks migration until redesigned.</summary>
        Error = 2,
    }
}
