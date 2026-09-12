using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Xunit;

namespace OptiGraphMigrator.Tool.Tests
{
    /// <summary>
    /// Ensures MSBuildLocator is registered exactly once before any test touches
    /// Microsoft.CodeAnalysis.Workspaces.MSBuild (directly or transitively via ScanEngine).
    /// </summary>
    public sealed class MsBuildLocatorFixture
    {
        public MsBuildLocatorFixture()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                var instance = instances
                    .OrderByDescending(i => i.Version)
                    .FirstOrDefault();

                if (instance is not null)
                {
                    MSBuildLocator.RegisterInstance(instance);
                }
                else
                {
                    var dotnetRoot = FindDotnetSdkRoot();
                    if (dotnetRoot is not null)
                    {
                        MSBuildLocator.RegisterMSBuildPath(dotnetRoot);
                    }
                    else
                    {
                        MSBuildLocator.RegisterDefaults();
                    }
                }
            }
        }

        private static string? FindDotnetSdkRoot()
        {
            var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var dotnetExe = processPath is not null && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? processPath
                : null;

            var dotnetRoot = dotnetExe is not null
                ? Path.GetDirectoryName(dotnetExe)
                : Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? @"C:\Program Files\dotnet";

            if (dotnetRoot is null)
            {
                return null;
            }

            var sdkDirectory = Path.Combine(dotnetRoot, "sdk");
            if (!Directory.Exists(sdkDirectory))
            {
                return null;
            }

            var latestSdk = new DirectoryInfo(sdkDirectory)
                .GetDirectories()
                .Where(d => File.Exists(Path.Combine(d.FullName, "MSBuild.dll")))
                .OrderByDescending(d => d.Name)
                .FirstOrDefault();

            return latestSdk?.FullName;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class MsBuildLocatorCollection : ICollectionFixture<MsBuildLocatorFixture>
    {
        public const string Name = "MSBuildLocator collection";
    }

    internal static class SamplePaths
    {
        public static string SampleSolutionCsproj
        {
            get
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OptiGraphMigrator.slnx")))
                {
                    directory = directory.Parent;
                }

                if (directory is null)
                {
                    throw new InvalidOperationException("Could not locate OptiGraphMigrator.slnx from test base directory.");
                }

                return Path.Combine(directory.FullName, "samples", "SampleFindSolution", "SampleFindSolution.csproj");
            }
        }
    }
}
