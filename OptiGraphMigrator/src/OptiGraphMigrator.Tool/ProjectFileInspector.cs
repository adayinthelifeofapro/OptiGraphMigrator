using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OptiGraphMigrator.Tool
{
    /// <summary>
    /// Describes what <see cref="ProjectFileInspector"/> found about a project without ever
    /// touching MSBuild or Roslyn workspace types. Used to decide how the scan should load the
    /// solution/project (SDK MSBuild, Visual Studio MSBuild, or source-only fallback) before any
    /// of those assemblies are referenced.
    /// </summary>
    public sealed class ProjectFileInspectionResult
    {
        public bool HasLegacyProject { get; init; }

        public bool HasPackagesConfig { get; init; }

        public string? TargetFrameworkVersion { get; init; }

        public string? FindPackageVersion { get; init; }

        public string? CmsCorePackageVersion { get; init; }

        /// <summary>Best-effort guess at the Optimizely CMS generation ("11", "12+", or null when unknown).</summary>
        public string? DetectedCmsVersion { get; init; }
    }

    /// <summary>
    /// Inspects solution/project files as plain XML/text - never via MSBuild evaluation - so the
    /// result can be used to decide which MSBuild instance to register with
    /// <c>MSBuildLocator</c> before any MSBuild or Roslyn workspace type is loaded.
    /// </summary>
    public static class ProjectFileInspector
    {
        public static ProjectFileInspectionResult Inspect(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new ProjectFileInspectionResult();
            }

            var projectPaths = ResolveProjectPaths(path);

            var hasLegacyProject = false;
            var hasPackagesConfig = false;
            string? targetFrameworkVersion = null;
            string? findVersion = null;
            string? cmsCoreVersion = null;

            foreach (var projectPath in projectPaths)
            {
                if (!File.Exists(projectPath))
                {
                    continue;
                }

                XDocument document;
                try
                {
                    document = XDocument.Load(projectPath);
                }
                catch (Exception)
                {
                    continue;
                }

                var root = document.Root;
                if (root is null)
                {
                    continue;
                }

                var sdkAttribute = root.Attribute("Sdk");
                var isSdkStyle = sdkAttribute is not null;

                var ns = root.Name.Namespace;
                var tfv = root.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;

                if (!isSdkStyle)
                {
                    hasLegacyProject = true;
                }

                if (!string.IsNullOrEmpty(tfv))
                {
                    targetFrameworkVersion ??= tfv;
                }

                var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
                var packagesConfigPath = Path.Combine(projectDirectory, "packages.config");
                if (File.Exists(packagesConfigPath))
                {
                    hasPackagesConfig = true;
                    var (find, cms) = ReadPackagesConfigVersions(packagesConfigPath);
                    findVersion ??= find;
                    cmsCoreVersion ??= cms;
                }

                var (refFind, refCms) = ReadPackageReferenceVersions(root, ns);
                findVersion ??= refFind;
                cmsCoreVersion ??= refCms;
            }

            var detectedCmsVersion = DetectCmsVersion(findVersion, cmsCoreVersion, hasLegacyProject, hasPackagesConfig);

            return new ProjectFileInspectionResult
            {
                HasLegacyProject = hasLegacyProject,
                HasPackagesConfig = hasPackagesConfig,
                TargetFrameworkVersion = targetFrameworkVersion,
                FindPackageVersion = findVersion,
                CmsCorePackageVersion = cmsCoreVersion,
                DetectedCmsVersion = detectedCmsVersion
            };
        }

        private static IEnumerable<string> ResolveProjectPaths(string path)
        {
            var extension = Path.GetExtension(path);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;

            if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(path);
                }
                catch (Exception)
                {
                    yield break;
                }

                foreach (var projectElement in document.Descendants("Project"))
                {
                    var relativePath = projectElement.Attribute("Path")?.Value;
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        yield return Path.GetFullPath(Path.Combine(directory, relativePath));
                    }
                }

                yield break;
            }

            if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(path);
                }
                catch (Exception)
                {
                    yield break;
                }

                foreach (var line in lines)
                {
                    if (!line.StartsWith("Project(", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split(',');
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    var relativePath = parts[1].Trim().Trim('"');
                    if (relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        relativePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return Path.GetFullPath(Path.Combine(directory, relativePath));
                    }
                }

                yield break;
            }

            yield return Path.GetFullPath(path);
        }

        private static (string? Find, string? CmsCore) ReadPackagesConfigVersions(string packagesConfigPath)
        {
            try
            {
                var document = XDocument.Load(packagesConfigPath);
                string? find = null;
                string? cmsCore = null;

                foreach (var package in document.Descendants("package"))
                {
                    var id = package.Attribute("id")?.Value;
                    var version = package.Attribute("version")?.Value;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                    {
                        continue;
                    }

                    if (string.Equals(id, "EPiServer.Find", StringComparison.OrdinalIgnoreCase))
                    {
                        find = version;
                    }
                    else if (string.Equals(id, "EPiServer.CMS.Core", StringComparison.OrdinalIgnoreCase))
                    {
                        cmsCore = version;
                    }
                }

                return (find, cmsCore);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        private static (string? Find, string? CmsCore) ReadPackageReferenceVersions(XElement root, System.Xml.Linq.XNamespace ns)
        {
            string? find = null;
            string? cmsCore = null;

            foreach (var packageReference in root.Descendants(ns + "PackageReference"))
            {
                var id = packageReference.Attribute("Include")?.Value;
                var version = packageReference.Attribute("Version")?.Value
                    ?? packageReference.Element(ns + "Version")?.Value;

                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (string.Equals(id, "EPiServer.Find", StringComparison.OrdinalIgnoreCase))
                {
                    find = version;
                }
                else if (string.Equals(id, "EPiServer.CMS.Core", StringComparison.OrdinalIgnoreCase))
                {
                    cmsCore = version;
                }
            }

            return (find, cmsCore);
        }

        private static string? DetectCmsVersion(string? findVersion, string? cmsCoreVersion, bool hasLegacyProject, bool hasPackagesConfig)
        {
            var version = cmsCoreVersion ?? findVersion;
            if (!string.IsNullOrEmpty(version))
            {
                var majorText = version.Split('.')[0];
                if (int.TryParse(majorText, out var major))
                {
                    // EPiServer.CMS.Core / EPiServer.Find major version 11.x/13.x correspond to CMS 11.
                    return major <= 13 ? "11" : "12+";
                }
            }

            if (hasLegacyProject || hasPackagesConfig)
            {
                return "11";
            }

            return null;
        }
    }
}
