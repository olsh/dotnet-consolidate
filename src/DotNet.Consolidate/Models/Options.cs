using System.Collections.Generic;

using CommandLine;

namespace DotNet.Consolidate.Models
{
    public class Options
    {
        public Options(
            ICollection<string>? solutions,
            ICollection<string>? packageIds,
            ICollection<string>? excludedPackageIds,
            string excludedPackageVersionsRegex,
            bool readDirectoryBuildProps,
            bool reportOverridenDirectoryBuildProps,
            ICollection<string>? globalProperties,
            OutputFormat format)
        {
            Solutions = solutions;
            PackageIds = packageIds;
            ExcludedPackageIds = excludedPackageIds;
            ExcludedPackageVersionsRegex = excludedPackageVersionsRegex;
            ReadDirectoryBuildProps = readDirectoryBuildProps;
            ReportOverridenDirectoryBuildProps = reportOverridenDirectoryBuildProps;
            GlobalProperties = globalProperties;
            Format = format;
        }

        [Option(
            's',
            "solutions",
            Required = false,
            HelpText =
                "Target solutions for checking. If not specified, all solutions in the working directory will be analyzed.")]
        public ICollection<string>? Solutions { get; }

        [Option('p', "packageIds", Required = false, HelpText = "Package IDs for checking.")]
        public ICollection<string>? PackageIds { get; }

        [Option('e', "excluded", Required = false, HelpText = "Package IDs that will be skipped during checking.")]
        public ICollection<string>? ExcludedPackageIds { get; }

        [Option(
            "excludedVersionsRegex",
            Required = false,
            HelpText = "A regular expression to match package versions that will be skipped during checking.")]
        public string ExcludedPackageVersionsRegex { get; }

        [Option(
            'd',
            "directoryBuildProps",
            Required = false,
            Default = true,
            HelpText = "Take Directory.Build.props files into account")]
        public bool ReadDirectoryBuildProps { get; }

        [Option(
            'o',
            "reportOverridenDirectoryBuildProps",
            Required = false,
            Default = true,
            HelpText = "Report when csproj overrides a Directory.Build.props")]
        public bool ReportOverridenDirectoryBuildProps { get; }

        [Option(
            "property",
            Required = false,
            HelpText =
                "MSBuild properties in the Name=Value format, used when evaluating the conditions in project files, e.g. --property NuGetBuild=true Configuration=Release")]
        public ICollection<string>? GlobalProperties { get; }

        [Option(
            'f',
            "format",
            Required = false,
            Default = OutputFormat.Text,
            HelpText =
                "Output format, Text or Json. Json prints a single JSON document to stdout and suppresses progress messages.")]
        public OutputFormat Format { get; }
    }
}
