using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Writes the whole run as a single JSON document.
    /// </summary>
    /// <remarks>
    /// The results are buffered until <see cref="Flush"/> because several solutions have to end up in one document.
    /// </remarks>
    public class JsonOutputWriter : IOutputWriter
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,

            // The default encoder escapes characters that are only dangerous inside HTML, which turns the
            // backticks and quotes in our messages into \u00XX noise. This output is piped to a shell, never
            // embedded in a page.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly TextWriter _output;

        private readonly IReadOnlyCollection<string> _warnings;

        private readonly List<JsonSolutionReport> _solutions = new List<JsonSolutionReport>();

        /// <param name="output">The stream the document is written to.</param>
        /// <param name="warnings">
        /// The collection the collecting logger fills. It is read at <see cref="Flush"/> time, so the messages
        /// logged while the solutions are being analyzed make it into the document.
        /// </param>
        public JsonOutputWriter(TextWriter output, IReadOnlyCollection<string> warnings)
        {
            _output = output;
            _warnings = warnings;
        }

        public void WriteAnalysisResults(SolutionAnalysisResult result)
        {
            var packages = result.NonConsolidatedPackages
                .Select(package => new JsonPackageReport(
                    package.NuGetPackageId,
                    package.PackageVersions
                        .OrderBy(p => p.NuGetPackageVersion)
                        .ThenBy(p => p.ProjectName)
                        .Select(p => new JsonPackageVersionReport(p.ProjectName, p.NuGetPackageVersion.OriginalValue))
                        .ToList()))
                .ToList();

            // Ordered the same way as the text report, so the two agree on what "first" means.
            var directoryBuildPropsOverrides = result.DirectoryBuildPropsOverrides
                .OrderBy(o => o.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ProjectName)
                .ThenBy(o => o.ProjectVersion)
                .Select(o => new JsonDirectoryBuildPropsOverrideReport(
                    o.PackageId,
                    o.ProjectName,
                    o.ProjectVersion.OriginalValue,
                    o.DirectoryBuildPropsVersion.OriginalValue,
                    o.DirectoryBuildPropsFile))
                .ToList();

            _solutions.Add(
                new JsonSolutionReport(
                    result.SolutionFile,
                    result.SolutionFiles,
                    result.IsParsedWithoutIssues,
                    result.PackageIdsNotFoundInSolution.ToList(),
                    packages,
                    directoryBuildPropsOverrides));
        }

        public void Flush()
        {
            var report = new JsonReport(_warnings.ToList(), _solutions);

            _output.WriteLine(JsonSerializer.Serialize(report, SerializerOptions));
        }
    }
}
