using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// The shape of the <c>--format json</c> document.
    /// </summary>
    /// <remarks>
    /// These types exist so the JSON stays a deliberate, stable contract. Serializing the domain models directly
    /// would leak internals such as the parsed solution model, and every refactoring would silently become a
    /// breaking change for whoever scripts against the output.
    /// </remarks>
    public class JsonReport
    {
        public JsonReport(IReadOnlyCollection<string> warnings, IReadOnlyCollection<JsonSolutionReport> solutions)
        {
            Warnings = warnings;
            Solutions = solutions;
        }

        /// <summary>
        /// Gets the messages that would have been printed to the console in the text format.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; }

        public IReadOnlyCollection<JsonSolutionReport> Solutions { get; }
    }

    public class JsonSolutionReport
    {
        public JsonSolutionReport(
            string solutionFile,
            bool isParsedWithoutIssues,
            IReadOnlyCollection<string> packageIdsNotFound,
            IReadOnlyCollection<JsonPackageReport> nonConsolidatedPackages)
        {
            SolutionFile = solutionFile;
            IsParsedWithoutIssues = isParsedWithoutIssues;
            PackageIdsNotFound = packageIdsNotFound;
            NonConsolidatedPackages = nonConsolidatedPackages;
        }

        public string SolutionFile { get; }

        public bool IsParsedWithoutIssues { get; }

        public IReadOnlyCollection<string> PackageIdsNotFound { get; }

        public IReadOnlyCollection<JsonPackageReport> NonConsolidatedPackages { get; }
    }

    public class JsonPackageReport
    {
        public JsonPackageReport(string packageId, IReadOnlyCollection<JsonPackageVersionReport> packageVersions)
        {
            PackageId = packageId;
            PackageVersions = packageVersions;
        }

        public string PackageId { get; }

        public IReadOnlyCollection<JsonPackageVersionReport> PackageVersions { get; }
    }

    public class JsonPackageVersionReport
    {
        public JsonPackageVersionReport(string projectName, string version)
        {
            ProjectName = projectName;
            Version = version;
        }

        public string ProjectName { get; }

        public string Version { get; }
    }
}
