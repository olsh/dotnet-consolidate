using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// Everything the output writers need to report on one analysis — a single solution, or several analyzed
    /// together with <c>-c</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately independent of <see cref="SolutionInfo"/> and <see cref="Options"/>: the writers describe a
    /// finished analysis, they don't re-derive it, which also means they can be tested without parsing a solution.
    /// </remarks>
    public class SolutionAnalysisResult
    {
        /// <summary>
        /// One solution.
        /// </summary>
        public SolutionAnalysisResult(
            string solutionFile,
            bool isParsedWithoutIssues,
            IReadOnlyCollection<AnalysisResult> nonConsolidatedPackages,
            IReadOnlyCollection<string> requestedPackageIds,
            IReadOnlyCollection<string> packageIdsNotFoundInSolution,
            IReadOnlyCollection<DirectoryBuildPropsOverride> directoryBuildPropsOverrides)
            : this(
                new[] { solutionFile },
                isParsedWithoutIssues,
                nonConsolidatedPackages,
                requestedPackageIds,
                packageIdsNotFoundInSolution,
                directoryBuildPropsOverrides)
        {
        }

        /// <summary>
        /// Several solutions analyzed as one set.
        /// </summary>
        public SolutionAnalysisResult(
            IReadOnlyCollection<string> solutionFiles,
            bool isParsedWithoutIssues,
            IReadOnlyCollection<AnalysisResult> nonConsolidatedPackages,
            IReadOnlyCollection<string> requestedPackageIds,
            IReadOnlyCollection<string> packageIdsNotFoundInSolution,
            IReadOnlyCollection<DirectoryBuildPropsOverride> directoryBuildPropsOverrides)
        {
            SolutionFiles = solutionFiles;
            SolutionFile = string.Join(", ", solutionFiles);
            IsParsedWithoutIssues = isParsedWithoutIssues;
            NonConsolidatedPackages = nonConsolidatedPackages;
            RequestedPackageIds = requestedPackageIds;
            PackageIdsNotFoundInSolution = packageIdsNotFoundInSolution;
            DirectoryBuildPropsOverrides = directoryBuildPropsOverrides;
        }

        /// <summary>
        /// Gets the solutions this result covers, one entry per solution, so a consumer of the JSON report can
        /// tell them apart.
        /// </summary>
        public IReadOnlyCollection<string> SolutionFiles { get; }

        /// <summary>
        /// Gets the solutions as one label to name them by, which is what the text report prints.
        /// </summary>
        public string SolutionFile { get; }

        /// <summary>
        /// Gets a value indicating whether the solution file and all its projects were parsed correctly.
        /// When <c>false</c>, the results may be incomplete.
        /// </summary>
        public bool IsParsedWithoutIssues { get; }

        public IReadOnlyCollection<AnalysisResult> NonConsolidatedPackages { get; }

        /// <summary>
        /// Gets the package IDs the run was limited to with <c>-p</c>, empty when the option wasn't given.
        /// </summary>
        public IReadOnlyCollection<string> RequestedPackageIds { get; }

        /// <summary>
        /// Gets the <see cref="RequestedPackageIds"/> that no project in the solution references.
        /// </summary>
        public IReadOnlyCollection<string> PackageIdsNotFoundInSolution { get; }

        /// <summary>
        /// Gets the packages a project declares itself while also inheriting them from a
        /// <c>Directory.Build.props</c>, empty when <c>-o</c> is off.
        /// </summary>
        /// <remarks>
        /// Purely informational: unlike the collections above, these never fail the run. The option is on by
        /// default, so exiting non-zero on them would turn green builds red on an upgrade nobody opted into.
        /// </remarks>
        public IReadOnlyCollection<DirectoryBuildPropsOverride> DirectoryBuildPropsOverrides { get; }
    }
}
