using System;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Writes the human readable report, one solution at a time.
    /// </summary>
    public class TextOutputWriter : IOutputWriter
    {
        /// <summary>
        /// The rule the report fences each section header with.
        /// </summary>
        private const string HeaderRule = "----------------------------";

        private readonly TextWriter _output;

        public TextOutputWriter(TextWriter output)
        {
            _output = output;
        }

        public void WriteAnalysisResults(SolutionAnalysisResult result)
        {
            if (result.NonConsolidatedPackages.Any())
            {
                _output.WriteLine("Found {0} non-consolidated packages", result.NonConsolidatedPackages.Count);
                _output.WriteLine();
            }

            foreach (var package in result.NonConsolidatedPackages)
            {
                _output.WriteLine(HeaderRule);
                _output.WriteLine(package.NuGetPackageId);
                _output.WriteLine(HeaderRule);

                foreach (var packageVersion in package.PackageVersions.OrderBy(p => p.NuGetPackageVersion)
                             .ThenBy(p => p.ProjectName))
                {
                    _output.WriteLine("{0} - {1}", packageVersion.ProjectName, packageVersion.NuGetPackageVersion);
                }

                _output.WriteLine();
            }

            WriteDirectoryBuildPropsOverrides(result);

            if (result.PackageIdsNotFoundInSolution.Any())
            {
                _output.WriteLine(
                    "The following package IDs given for consolidation check were not found in the solution projects:");
                _output.WriteLine(string.Join(Environment.NewLine, result.PackageIdsNotFoundInSolution));
            }

            // A package that isn't referenced anywhere isn't consolidated, so saying so would contradict the
            // report just written above.
            if (!result.NonConsolidatedPackages.Any() && !result.PackageIdsNotFoundInSolution.Any())
            {
                var packageList = result.RequestedPackageIds.Any()
                    ? $"from the list {string.Join(Environment.NewLine, result.RequestedPackageIds)} "
                    : string.Empty;
                _output.WriteLine($"All packages {packageList}in {result.SolutionFile} are consolidated.");
            }
        }

        public void Flush()
        {
            // The text report is written as each solution is analyzed, so there is nothing left to emit.
        }

        /// <remarks>
        /// Grouped by package ID and shaped like the consolidation report above, so the two read as one report.
        /// It sits before the remaining sections and leaves the "all packages are consolidated" line alone: an
        /// override says nothing about whether the solution's versions agree across projects.
        /// </remarks>
        private void WriteDirectoryBuildPropsOverrides(SolutionAnalysisResult result)
        {
            if (!result.DirectoryBuildPropsOverrides.Any())
            {
                return;
            }

            // The key is the casing of the first project that declared the package, matching how the
            // consolidation report above picks the ID it prints.
            var overridesByPackage = result.DirectoryBuildPropsOverrides
                .GroupBy(o => o.PackageId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            _output.WriteLine("Found {0} Directory.Build.props overrides", result.DirectoryBuildPropsOverrides.Count);
            _output.WriteLine();

            foreach (var package in overridesByPackage)
            {
                _output.WriteLine(HeaderRule);
                _output.WriteLine(package.Key);
                _output.WriteLine(HeaderRule);

                foreach (var packageOverride in package.OrderBy(o => o.ProjectName)
                             .ThenBy(o => o.ProjectVersion))
                {
                    var propsFile = string.IsNullOrEmpty(packageOverride.DirectoryBuildPropsFile)
                        ? string.Empty
                        : $" from {packageOverride.DirectoryBuildPropsFile}";

                    _output.WriteLine(
                        "{0} - {1} overrides {2}{3}",
                        packageOverride.ProjectName,
                        packageOverride.ProjectVersion,
                        packageOverride.DirectoryBuildPropsVersion,
                        propsFile);
                }

                _output.WriteLine();
            }
        }
    }
}
