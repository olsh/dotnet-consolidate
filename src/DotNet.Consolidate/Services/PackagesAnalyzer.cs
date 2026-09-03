using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DotNet.Consolidate.Models;

using Version = DotNet.Consolidate.Models.Version;

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

            var nonConsolidatedPackages = FilterByPackageId(
                analysisResults.Values.Where(r => r.ContainsDifferentPackagesVersions),
                p => p.NuGetPackageId,
                options);

            if (!string.IsNullOrEmpty(options.ExcludedPackageVersionsRegex))
            {
                nonConsolidatedPackages = nonConsolidatedPackages
                    .Where(p => !p.PackageVersions.Any(version =>
                        IsExcludedVersion(version.NuGetPackageVersion, options)));
            }

            return nonConsolidatedPackages.ToList();
        }

        /// <summary>
        /// The packages a project declares itself while also inheriting them from a <c>Directory.Build.props</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The signal is already in the model: <see cref="SolutionInfoProvider"/> appends the props packages to
        /// the project without de-duplicating against its own references, so an overriding project simply holds
        /// both a <see cref="NuGetPackageReferenceType.Direct"/> and a
        /// <see cref="NuGetPackageReferenceType.Inherited"/> entry for the same ID.
        /// </para>
        /// <para>
        /// Note that this only sees the duplicate <c>Include</c> form. A csproj that overrides with
        /// <c>&lt;PackageReference Update="..." /&gt;</c> is invisible, because <see cref="ProjectEvaluator"/>
        /// doesn't read <c>Update</c> at all.
        /// </para>
        /// </remarks>
        public List<DirectoryBuildPropsOverride> FindDirectoryBuildPropsOverrides(
            ICollection<ProjectInfo> projectInfos,
            Options options)
        {
            var overrides = new List<DirectoryBuildPropsOverride>();
            foreach (var projectInfo in projectInfos)
            {
                // A lookup rather than a dictionary: nothing stops a props file from declaring an ID twice.
                var inheritedPackages = projectInfo.Packages
                    .Where(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited)
                    .ToLookup(p => p.Id, PackageIdComparer);
                if (!inheritedPackages.Any())
                {
                    continue;
                }

                var directPackages = projectInfo.Packages
                    .Where(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct);

                foreach (var directPackage in directPackages)
                {
                    foreach (var inheritedPackage in inheritedPackages[directPackage.Id])
                    {
                        overrides.Add(
                            new DirectoryBuildPropsOverride(
                                projectInfo.ProjectName,
                                directPackage.Id,
                                directPackage.Version,
                                inheritedPackage.Version,
                                projectInfo.DirectoryBuildPropsFile ?? string.Empty));
                    }
                }
            }

            // Filtered the same way as the consolidation report, so `-p Serilog` doesn't leave override noise
            // for every other package behind.
            var reportedOverrides = FilterByPackageId(overrides, o => o.PackageId, options);
            if (!string.IsNullOrEmpty(options.ExcludedPackageVersionsRegex))
            {
                reportedOverrides = reportedOverrides
                    .Where(o => !IsExcludedVersion(o.ProjectVersion, options)
                                && !IsExcludedVersion(o.DirectoryBuildPropsVersion, options));
            }

            return reportedOverrides.ToList();
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

        /// <summary>
        /// Applies the <c>-p</c> and <c>-e</c> package ID filters, in that order.
        /// </summary>
        /// <remarks>
        /// Shared by every report so the two filters can't drift apart, and so both go through
        /// <see cref="PackageIdComparer"/> the way the class comment promises.
        /// </remarks>
        private static IEnumerable<T> FilterByPackageId<T>(
            IEnumerable<T> items,
            Func<T, string> packageId,
            Options options)
        {
            if (options.PackageIds?.Any() == true)
            {
                var requestedPackageIds = new HashSet<string>(options.PackageIds, PackageIdComparer);
                items = items.Where(item => requestedPackageIds.Contains(packageId(item)))
                    .ToList();
            }

            if (options.ExcludedPackageIds?.Any() == true)
            {
                var excludedPackageIds = new HashSet<string>(options.ExcludedPackageIds, PackageIdComparer);
                items = items.Where(item => !excludedPackageIds.Contains(packageId(item)))
                    .ToList();
            }

            return items;
        }

        private static bool IsExcludedVersion(Version version, Options options)
        {
            return Regex.IsMatch(
                version.OriginalValue,
                options.ExcludedPackageVersionsRegex,
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100));
        }
    }
}
