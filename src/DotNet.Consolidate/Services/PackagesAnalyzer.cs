using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public class PackagesAnalyzer
    {
        // NuGet package IDs are case-insensitive, so `Serilog` and `serilog` are the same package — when
        // grouping, when filtering with `-p`/`-e`, and when deciding a `-p` ID isn't in the solution. Every
        // package ID comparison in the tool goes through this comparer so those three can't drift apart.
        private static readonly StringComparer PackageIdComparer = StringComparer.OrdinalIgnoreCase;

        public List<AnalysisResult> FindNonConsolidatedPackages(ICollection<ProjectInfo> projectInfos, Options options)
        {
            // The casing of the first project that references a package is the one that gets reported.
            var analysisResults = new Dictionary<string, AnalysisResult>(PackageIdComparer);
            foreach (var projectInfo in projectInfos)
            {
                foreach (var packageInfo in projectInfo.Packages)
                {
                    if (!analysisResults.TryGetValue(packageInfo.Id, out var analysisResult))
                    {
                        analysisResult = new AnalysisResult(packageInfo.Id);
                        analysisResults.Add(packageInfo.Id, analysisResult);
                    }

                    analysisResult.PackageVersions.Add(
                        new ProjectNuGetPackageVersion(projectInfo.ProjectName, packageInfo.Version));
                }
            }

            var nonConsolidatedPackages = analysisResults.Values.Where(r => r.ContainsDifferentPackagesVersions);
            if (options.PackageIds?.Any() == true)
            {
                var requestedPackageIds = new HashSet<string>(options.PackageIds, PackageIdComparer);
                nonConsolidatedPackages = nonConsolidatedPackages
                    .Where(p => requestedPackageIds.Contains(p.NuGetPackageId))
                    .ToList();
            }

            if (options.ExcludedPackageIds?.Any() == true)
            {
                var excludedPackageIds = new HashSet<string>(options.ExcludedPackageIds, PackageIdComparer);
                nonConsolidatedPackages = nonConsolidatedPackages
                    .Where(p => !excludedPackageIds.Contains(p.NuGetPackageId))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(options.ExcludedPackageVersionsRegex))
            {
                nonConsolidatedPackages = nonConsolidatedPackages
                    .Where(p => !p.PackageVersions.Any(version =>
                        Regex.IsMatch(
                            version.NuGetPackageVersion.OriginalValue,
                            options.ExcludedPackageVersionsRegex,
                            RegexOptions.None,
                            TimeSpan.FromMilliseconds(100))));
            }

            return nonConsolidatedPackages.ToList();
        }

        /// <summary>
        /// The <c>-p</c> package IDs that no project in the solution references.
        /// </summary>
        /// <remarks>
        /// Lives next to the filters above so it uses the same <see cref="PackageIdComparer"/>: an ID the
        /// filters accept must never be reported as missing, and the other way round.
        /// </remarks>
        /// <returns>The requested IDs as the user typed them, so the report echoes back their casing.</returns>
        public List<string> FindPackageIdsNotInSolution(
            ICollection<ProjectInfo> projectInfos,
            IReadOnlyCollection<string> requestedPackageIds)
        {
            var solutionPackageIds = new HashSet<string>(
                projectInfos.SelectMany(projectInfo => projectInfo.Packages.Select(package => package.Id)),
                PackageIdComparer);

            return requestedPackageIds.Where(id => !solutionPackageIds.Contains(id))
                .ToList();
        }
    }
}
