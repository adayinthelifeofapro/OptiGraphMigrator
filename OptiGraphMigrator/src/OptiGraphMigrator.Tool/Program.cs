using System;
using System.Threading.Tasks;
using Microsoft.Build.Locator;

namespace OptiGraphMigrator.Tool
{
    /// <summary>
    /// Entry point for the <c>optigraph-migrate</c> global tool: scans a solution or project
    /// for Optimizely Search and Navigation (Find) usage and reports the Optimizely Graph
    /// migration path.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Must run before any Microsoft.CodeAnalysis.Workspaces.MSBuild (or MSBuild)
            // type is touched, directly or transitively. Inspects the scan target (plain
            // XML/text only) to decide between the default .NET SDK MSBuild and a full
            // Visual Studio/Build Tools instance capable of loading legacy (CMS 11 style)
            // non-SDK projects.
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildBootstrapper.RegisterFor(args, Console.Error);
            }

            return await CommandLineApp.RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
        }
    }
}
