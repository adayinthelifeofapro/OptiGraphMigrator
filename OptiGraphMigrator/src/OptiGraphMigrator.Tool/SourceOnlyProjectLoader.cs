using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace OptiGraphMigrator.Tool
{
    /// <summary>
    /// Fallback loader used when <c>MSBuildWorkspace</c> cannot load a project at all (typical
    /// for legacy CMS 11 style non-SDK projects on machines without a full MSBuild toolset, or
    /// when packages have never been restored). Builds an <see cref="AdhocWorkspace"/> project
    /// directly from the <c>.cs</c> files on disk, with no external metadata references beyond the
    /// BCL, so the syntax-only analyzer fallback (see <c>FindSymbolIndex.IsSyntacticOnly</c>) can
    /// still find Find usage by name.
    /// </summary>
    /// <remarks>
    /// Results produced from a project loaded this way are heuristic: without real references,
    /// the compiler cannot resolve symbols, so matching falls back to syntactic name comparison
    /// and may produce false positives or miss overload-specific nuances.
    /// </remarks>
    public static class SourceOnlyProjectLoader
    {
        public static IReadOnlyList<Project> Load(string path)
        {
            var directory = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();

            var files = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(f))
                    .ToList()
                : new List<string>();

            if (files.Count == 0)
            {
                return Array.Empty<Project>();
            }

            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var projectName = Path.GetFileNameWithoutExtension(path);

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            };

            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: references);

            var project = workspace.AddProject(projectInfo);

            foreach (var file in files)
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }

                workspace.AddDocument(DocumentInfo.Create(
                    DocumentId.CreateNewId(project.Id),
                    Path.GetFileName(file),
                    filePath: file,
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create()))));
            }

            var loadedProject = workspace.CurrentSolution.GetProject(projectId);
            return loadedProject is null ? Array.Empty<Project>() : new[] { loadedProject };
        }

        private static bool IsExcluded(string filePath)
        {
            var normalized = filePath.Replace('\\', '/');
            return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
