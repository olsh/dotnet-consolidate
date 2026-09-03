using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Turns parsed solutions into the <see cref="SolutionAnalysisResult"/> the output writers report on.
    /// </summary>
    /// <remarks>
    /// Static, like the other stateless services here. It lives outside <c>Program</c> because <c>Program</c> is
    /// <c>internal</c> with <c>private</c> members and there is no <c>InternalsVisibleTo</c>, so nothing left in
    /// there can be asserted directly — and the pooled path in particular has enough of its own rules to be worth
    /// testing. Keeping both paths in one class is also what stops them from drifting apart.
    /// </remarks>
    public static class SolutionAnalyzer
    {
        /// <summary>
        /// Analyzes one solution on its own, the way the tool does without <c>-c</c>.
        /// </summary>
        public static SolutionAnalysisResult Analyze(SolutionInfo solutionInfo, Options options)
        {
            return Analyze(
                new[] { solutionInfo.SolutionFile },
                solutionInfo.IsParsedWithoutIssues,
                solutionInfo.ProjectInfos,
                options);
        }

        /// <summary>
        /// The analysis itself, over projects that have already been gathered.
        /// </summary>
        /// <remarks>
        /// Independent of <see cref="SolutionInfo"/> so it can be driven with hand-built projects, the same way
        /// <see cref="PackagesAnalyzer"/> is. Every filter is applied by the analyzer to whatever projects it is
        /// handed, so <c>-p</c>, <c>-e</c> and <c>--excludedVersionsRegex</c> need no case of their own here —
        /// and a <c>-p</c> ID referenced by only one of several pooled solutions is correctly not reported as
        /// missing.
        /// </remarks>
        public static SolutionAnalysisResult Analyze(
            IReadOnlyCollection<string> solutionFiles,
            bool isParsedWithoutIssues,
            ICollection<ProjectInfo> projectInfos,
            Options options)
        {
            var requestedPackageIds = options.PackageIds?.ToList() ?? new List<string>();

            var nonConsolidatedPackages = PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, options);

            // The analyzer owns this so it compares IDs exactly the way its `-p`/`-e` filters do — otherwise
            // a package the filter matched could still be reported as missing from the solution.
            var packageIdsNotFoundInSolution =
                PackagesAnalyzer.FindPackageIdsNotInSolution(projectInfos, requestedPackageIds);

            // With `-d false` there are no props files to inherit from, so this is empty either way and needs
            // no case of its own.
            var directoryBuildPropsOverrides = options.ReportOverridenDirectoryBuildProps ?? true
                ? PackagesAnalyzer.FindDirectoryBuildPropsOverrides(projectInfos, options)
                : new List<DirectoryBuildPropsOverride>();

            return new SolutionAnalysisResult(
                solutionFiles,
                isParsedWithoutIssues,
                nonConsolidatedPackages,
                requestedPackageIds,
                packageIdsNotFoundInSolution,
                directoryBuildPropsOverrides);
        }

        /// <summary>
        /// Analyzes every given solution as one set, so a package referenced at different versions by projects in
        /// different solutions is reported — which is the whole point of <c>-c</c>, since neither solution
        /// disagrees with itself.
        /// </summary>
        public static SolutionAnalysisResult AnalyzeAcrossSolutions(
            IReadOnlyCollection<SolutionInfo> solutionInfos,
            Options options)
        {
            return Analyze(
                solutionInfos.Select(solutionInfo => solutionInfo.SolutionFile)
                    .ToList(),

                // One solution that wouldn't parse leaves the pool as incomplete as it leaves its own report.
                solutionInfos.All(solutionInfo => solutionInfo.IsParsedWithoutIssues),
                PoolProjects(solutionInfos.Select(solutionInfo => solutionInfo.ProjectInfos)),
                options);
        }

        /// <summary>
        /// Gathers the projects of several solutions into one list, keeping a project that belongs to more than
        /// one of them only once.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sharing projects between solutions is the case <c>-c</c> exists for, and every solution is parsed on
        /// its own: the same project reached from two solutions comes back as two <see cref="ProjectInfo"/>
        /// instances. Reporting it twice would be noise at best, but the two are not even guaranteed to agree —
        /// <see cref="SolutionInfoProvider"/> walks for <c>Directory.Build.props</c> from each solution's own
        /// directory, so a project can inherit different versions depending on which solution found it, and
        /// keeping both copies would invent a discrepancy no build actually has.
        /// </para>
        /// <para>
        /// The first solution on the command line therefore wins. Identity is the resolved
        /// <see cref="ProjectInfo.ProjectFile"/>: resolving is what makes two spellings of the same file compare
        /// equal, which they otherwise wouldn't, since one solution can reach a project through <c>..</c> where
        /// another names it directly. The name alone won't do — a directory may hold more than one project, and
        /// merging those would drop references that are really there.
        /// </para>
        /// <para>
        /// Paths are compared the way <see cref="PathUtils"/> compares them, ordinally and case-insensitively on
        /// every platform, and the cost is the same one recorded there: on a case-sensitive file system two
        /// projects whose paths differ only in casing are taken for one, and the second is left out of the pool.
        /// Comparing case-sensitively there would trade that for the failure this method exists to prevent — the
        /// casing a project is written with is the solution file's to choose, and two solutions naming the same
        /// shared file differently would stop being recognised as one project. Whichever way it is decided it
        /// belongs to <see cref="PathUtils"/>, for every path comparison at once, not to this method alone.
        /// </para>
        /// <para>
        /// <see cref="Enumerable.DistinctBy{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey},IEqualityComparer{TKey})"/>
        /// keeps the first of each identity in encounter order, which is both halves of what is needed: the first
        /// solution on the command line wins, and the consolidation report names a package by the casing of the
        /// first project that references it, so the output stays reproducible.
        /// </para>
        /// </remarks>
        public static List<ProjectInfo> PoolProjects(IEnumerable<ICollection<ProjectInfo>> projectInfos)
        {
            return projectInfos.SelectMany(projects => projects)
                .DistinctBy(GetProjectIdentity, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <remarks>
        /// A project built by hand rather than read from a solution has no file to be identified by, so it falls
        /// back to where it sits and what it is called. <see cref="Path.Combine(string, string)"/> rather than
        /// concatenation, so the fallback can't collide with a real project file path.
        /// </remarks>
        private static string GetProjectIdentity(ProjectInfo projectInfo)
        {
            var directory = PathUtils.ResolveDirectory(projectInfo.ProjectDirectory);

            return projectInfo.ProjectFile == null
                ? Path.Combine(directory, projectInfo.ProjectName)
                : Path.GetFullPath(projectInfo.ProjectFile);
        }
    }
}
