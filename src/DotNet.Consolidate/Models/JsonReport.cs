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
            IReadOnlyCollection<string> solutionFiles,
            bool isParsedWithoutIssues,
            IReadOnlyCollection<string> packageIdsNotFound,
            IReadOnlyCollection<JsonPackageReport> nonConsolidatedPackages,
            IReadOnlyCollection<JsonDirectoryBuildPropsOverrideReport> directoryBuildPropsOverrides)
        {
            SolutionFile = solutionFile;
            SolutionFiles = solutionFiles;
            IsParsedWithoutIssues = isParsedWithoutIssues;
            PackageIdsNotFound = packageIdsNotFound;
            NonConsolidatedPackages = nonConsolidatedPackages;
            DirectoryBuildPropsOverrides = directoryBuildPropsOverrides;
        }

        /// <summary>
        /// Gets the solutions this entry covers, as one label. With <c>-c</c> that is every solution analyzed
        /// together, joined — <see cref="SolutionFiles"/> is the one to read them from.
        /// </summary>
        public string SolutionFile { get; }

        /// <summary>
        /// Gets the solutions this entry covers, one per element. A single-solution run has exactly one.
        /// </summary>
        public IReadOnlyCollection<string> SolutionFiles { get; }

        public bool IsParsedWithoutIssues { get; }

        public IReadOnlyCollection<string> PackageIdsNotFound { get; }

        public IReadOnlyCollection<JsonPackageReport> NonConsolidatedPackages { get; }

        /// <summary>
        /// Gets the packages a project declares itself while also inheriting them from a
        /// <c>Directory.Build.props</c>, empty when <c>-o</c> is off. Informational — they don't affect the
        /// exit code.
        /// </summary>
        public IReadOnlyCollection<JsonDirectoryBuildPropsOverrideReport> DirectoryBuildPropsOverrides { get; }
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

    public class JsonDirectoryBuildPropsOverrideReport
    {
        public JsonDirectoryBuildPropsOverrideReport(
            string packageId,
            string projectName,
            string version,
            string directoryBuildPropsVersion,
            string directoryBuildPropsFile)
        {
            PackageId = packageId;
            ProjectName = projectName;
            Version = version;
            DirectoryBuildPropsVersion = directoryBuildPropsVersion;
            DirectoryBuildPropsFile = directoryBuildPropsFile;
        }

        public string PackageId { get; }

        public string ProjectName { get; }

        /// <summary>
        /// Gets the version the project file declares.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the version the <c>Directory.Build.props</c> declares.
        /// </summary>
        public string DirectoryBuildPropsVersion { get; }

        /// <summary>
        /// Gets the full path of the props file that was overridden, empty when it isn't known.
        /// </summary>
        public string DirectoryBuildPropsFile { get; }
    }
}
