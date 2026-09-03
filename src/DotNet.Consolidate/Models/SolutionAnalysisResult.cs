using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// Everything the output writers need to report on a single solution.
    /// </summary>
    /// <remarks>
    /// Deliberately independent of <see cref="SolutionInfo"/> and <see cref="Options"/>: the writers describe a
    /// finished analysis, they don't re-derive it, which also means they can be tested without parsing a solution.
    /// </remarks>
    public class SolutionAnalysisResult
    {
        public SolutionAnalysisResult(
            string solutionFile,
            bool isParsedWithoutIssues,
            IReadOnlyCollection<AnalysisResult> nonConsolidatedPackages,
            IReadOnlyCollection<string> requestedPackageIds,
            IReadOnlyCollection<string> packageIdsNotFoundInSolution)
        {
            SolutionFile = solutionFile;
            IsParsedWithoutIssues = isParsedWithoutIssues;
            NonConsolidatedPackages = nonConsolidatedPackages;
            RequestedPackageIds = requestedPackageIds;
            PackageIdsNotFoundInSolution = packageIdsNotFoundInSolution;
        }

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
    }
}
