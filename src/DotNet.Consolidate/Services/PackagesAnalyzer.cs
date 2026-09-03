using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DotNet.Consolidate.Models;

using Version = DotNet.Consolidate.Models.Version;

namespace DotNet.Consolidate.Services
{
    /// <remarks>
    /// Static, like the other stateless services here: the class holds nothing but
    /// <see cref="PackageIdComparer"/>, and the analysis is a pure function of the projects and the options.
    /// </remarks>
    public static class PackagesAnalyzer
    {
        // NuGet package IDs are case-insensitive, so `Serilog` and `serilog` are the same package — when
        // grouping, when filtering with `-p`/`-e`, when deciding a `-p` ID isn't in the solution, and when an
        // `Update`/`Remove` decides which reference it names. Every package ID comparison in the tool goes
        // through this one comparer so they can't drift apart; it lives on the model because `ProjectEvaluator`
        // needs it too.
        private static readonly StringComparer PackageIdComparer = NuGetPackageInfo.IdComparer;

        /// <summary>
        /// The package references a project really restores: the ones it declares, plus the ones it inherits
        /// from a <c>Directory.Build.props</c> after its own <c>Update</c>s and <c>Remove</c>s have been applied
        /// to them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only the <see cref="NuGetPackageReferenceType.Inherited"/> entries are touched. The project's own
        /// references were already resolved against its <c>Update</c>s and <c>Remove</c>s by
        /// <see cref="ProjectEvaluator"/>, in document order — applying them a second time here would change a
        /// reference declared <i>below</i> the update that names it, which MSBuild does not do.
        /// </para>
        /// <para>
        /// An update that matches no inherited package leaves no trace at all, which is the point: an
        /// <c>Update</c> is not a reference and must not be counted as one.
        /// </para>
        /// </remarks>
        public static List<NuGetPackageInfo> GetEffectivePackages(ProjectInfo projectInfo)
        {
            if (projectInfo.PackageUpdates.Count == 0 && projectInfo.RemovedPackageIds.Count == 0)
            {
                return projectInfo.Packages.ToList();
            }

            var removedPackageIds = new HashSet<string>(projectInfo.RemovedPackageIds, PackageIdComparer);

            // A lookup, not a dictionary: a multi-targeting project can update the same ID to a different
            // version per target framework, and both versions are restored.
            var updates = projectInfo.PackageUpdates.ToLookup(u => u.Id, PackageIdComparer);

            var packages = new List<NuGetPackageInfo>();
            foreach (var package in projectInfo.Packages)
            {
                if (package.PackageReferenceType != NuGetPackageReferenceType.Inherited)
                {
                    packages.Add(package);

                    continue;
                }

                if (removedPackageIds.Contains(package.Id))
                {
                    continue;
                }

                var applicableUpdates = updates[package.Id]
                    .ToList();
                if (applicableUpdates.Count == 0)
                {
                    packages.Add(package);

                    continue;
                }

                var versions = new List<Version>();

                // An update that isn't certain to apply — one target framework of several, or a condition that
                // couldn't be evaluated — supersedes nothing, so the inherited version stands beside it.
                if (applicableUpdates.Any(u => !u.ReplacesInheritedVersion))
                {
                    versions.Add(package.Version);
                }

                foreach (var update in applicableUpdates.Where(update => !versions.Contains(update.Version)))
                {
                    versions.Add(update.Version);
                }

                packages.AddRange(
                    versions.Select(version =>
                        new NuGetPackageInfo(package.Id, version, NuGetPackageReferenceType.Inherited)));
            }

            return packages;
        }

        public static List<AnalysisResult> FindNonConsolidatedPackages(
            ICollection<ProjectInfo> projectInfos,
            Options options)
        {
            // The casing of the first project that references a package is the one that gets reported.
            var analysisResults = new Dictionary<string, AnalysisResult>(PackageIdComparer);
            foreach (var projectInfo in projectInfos)
            {
                foreach (var packageInfo in GetEffectivePackages(projectInfo))
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
        /// The packages a project pins itself while also inheriting them from a <c>Directory.Build.props</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two forms, and both are reported. The duplicate <c>Include</c> is already in the model:
        /// <see cref="SolutionInfoProvider"/> appends the props packages to the project without
        /// de-duplicating against its own references, so a project that re-declares one simply holds both a
        /// <see cref="NuGetPackageReferenceType.Direct"/> and a
        /// <see cref="NuGetPackageReferenceType.Inherited"/> entry for the same ID.
        /// </para>
        /// <para>
        /// The other form is <c>&lt;PackageReference Update="…" Version="…" /&gt;</c>, which is the idiomatic
        /// one — a re-declared <c>Include</c> is a duplicate item that NuGet flags as NU1504. It leaves no
        /// second entry to notice, so it is paired from <see cref="ProjectInfo.PackageUpdates"/> instead. A
        /// project can hold both for one ID, in which case only the duplicate <c>Include</c> is reported: the
        /// two would print the same line, since the update has already been applied to the direct entry.
        /// </para>
        /// <para>
        /// A <c>Remove</c> is not an override and is not reported. The package stops being a reference of that
        /// project altogether, which the consolidation report shows on its own.
        /// </para>
        /// </remarks>
        public static List<DirectoryBuildPropsOverride> FindDirectoryBuildPropsOverrides(
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
                    .Where(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct)
                    .ToList();

                overrides.AddRange(FindRedeclaredPackages(projectInfo, directPackages, inheritedPackages));
                overrides.AddRange(FindUpdatedPackages(projectInfo, directPackages, inheritedPackages));
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
        /// The duplicate-<c>Include</c> form: a package the project re-declares itself, which is why it holds
        /// a <see cref="NuGetPackageReferenceType.Direct"/> entry beside the inherited one.
        /// </summary>
        private static IEnumerable<DirectoryBuildPropsOverride> FindRedeclaredPackages(
            ProjectInfo projectInfo,
            IEnumerable<NuGetPackageInfo> directPackages,
            ILookup<string, NuGetPackageInfo> inheritedPackages)
        {
            return directPackages.SelectMany(
                directPackage => inheritedPackages[directPackage.Id],
                (directPackage, inheritedPackage) => new DirectoryBuildPropsOverride(
                    projectInfo.ProjectName,
                    directPackage.Id,
                    directPackage.Version,
                    inheritedPackage.Version,
                    projectInfo.DirectoryBuildPropsFile ?? string.Empty));
        }

        /// <summary>
        /// The <c>Update</c> form, which leaves no second entry to notice and has to be paired from
        /// <see cref="ProjectInfo.PackageUpdates"/> instead.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Skipped when the project also declares the ID <b>at the version the update sets</b>: an
        /// <c>Update</c> that follows the <c>Include</c> it names has by then been applied to that
        /// <see cref="NuGetPackageReferenceType.Direct"/> entry, so the two forms describe one override and
        /// would print the same line.
        /// </para>
        /// <para>
        /// Matching the version too is what makes that safe. An <c>Update</c> placed <i>above</i> its own
        /// <c>Include</c> never reaches it — MSBuild applies an update to the items declared before it — so
        /// the project pins the props version twice, at two different versions, and both are worth reporting.
        /// Skipping on the ID alone hid the second one.
        /// </para>
        /// <para>
        /// An ID the project removes is skipped outright: it isn't referenced at all any more.
        /// </para>
        /// </remarks>
        private static IEnumerable<DirectoryBuildPropsOverride> FindUpdatedPackages(
            ProjectInfo projectInfo,
            IReadOnlyCollection<NuGetPackageInfo> directPackages,
            ILookup<string, NuGetPackageInfo> inheritedPackages)
        {
            var removedPackageIds = new HashSet<string>(projectInfo.RemovedPackageIds, PackageIdComparer);

            return projectInfo.PackageUpdates
                .Where(update => !removedPackageIds.Contains(update.Id)
                                 && !directPackages.Any(package =>
                                     PackageIdComparer.Equals(package.Id, update.Id)
                                     && package.Version == update.Version))
                .SelectMany(
                    update => inheritedPackages[update.Id],
                    (update, inheritedPackage) => new DirectoryBuildPropsOverride(
                        projectInfo.ProjectName,
                        update.Id,
                        update.Version,
                        inheritedPackage.Version,
                        projectInfo.DirectoryBuildPropsFile ?? string.Empty));
        }

        /// <summary>
        /// The <c>-p</c> package IDs that no project in the solution references.
        /// </summary>
        /// <remarks>
        /// Lives next to the filters above so it uses the same <see cref="PackageIdComparer"/>: an ID the
        /// filters accept must never be reported as missing, and the other way round. It reads the effective
        /// packages for the same reason — a package every project removes really is not in the solution.
        /// </remarks>
        /// <returns>The requested IDs as the user typed them, so the report echoes back their casing.</returns>
        public static List<string> FindPackageIdsNotInSolution(
            ICollection<ProjectInfo> projectInfos,
            IReadOnlyCollection<string> requestedPackageIds)
        {
            var solutionPackageIds = new HashSet<string>(
                projectInfos.SelectMany(projectInfo => GetEffectivePackages(projectInfo)
                    .Select(package => package.Id)),
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
