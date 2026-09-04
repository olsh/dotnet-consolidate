using System.Collections.Generic;

using CommandLine;

namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// The command line options.
    /// </summary>
    /// <remarks>
    /// CommandLineParser fills the properties one at a time, so adding an option doesn't ripple through every
    /// place that builds an <see cref="Options"/>. The C# initializers repeat the <c>Default</c> values from the
    /// attributes, so an instance built in code behaves like one built from a command line.
    /// <para>
    /// A <c>bool</c> property is a switch to CommandLineParser and can never be given a value, which is why the
    /// toggles that default to <c>true</c> are declared <c>bool?</c>: that makes them scalars, and a scalar is
    /// what lets <c>-d false</c> turn one off. Declaring either as a plain <c>bool</c> silently turns it back
    /// into a switch that can only ever be on. The nullability is that mechanism and nothing more —
    /// <c>Default</c> still supplies <c>true</c> when the option is omitted, so the value is never actually
    /// <c>null</c>. A toggle that defaults to off wants the opposite: it only ever needs turning on, so a plain
    /// <c>bool</c> is right for it, and it keeps the bare form from swallowing the token that follows.
    /// </para>
    /// </remarks>
    public class Options
    {
        [Option(
            's',
            "solutions",
            Required = false,
            HelpText =
                "Target solutions for checking. If not specified, all solutions in the working directory will be analyzed.")]
        public ICollection<string>? Solutions { get; init; }

        /// <remarks>
        /// A plain <c>bool</c>, unlike the toggles below — see the class remarks.
        /// </remarks>
        [Option(
            'c',
            "crossSolution",
            Required = false,
            HelpText =
                "Analyze the given solutions as one set, so a package referenced at different versions by projects in different solutions is reported too.")]
        public bool CrossSolution { get; init; }

        [Option(
            'p',
            "packageIds",
            Required = false,
            HelpText =
                "Package IDs for checking. Wildcards are supported, * for any run of characters and ? for exactly one, e.g. -p MyCompany.*")]
        public ICollection<string>? PackageIds { get; init; }

        [Option(
            'e',
            "excluded",
            Required = false,
            HelpText =
                "Package IDs that will be skipped during checking. Wildcards are supported, e.g. -e MyCompany.*")]
        public ICollection<string>? ExcludedPackageIds { get; init; }

        [Option(
            "excludedVersionsRegex",
            Required = false,
            HelpText = "A regular expression to match package versions that will be skipped during checking.")]
        public string ExcludedPackageVersionsRegex { get; init; } = string.Empty;

        [Option(
            'd',
            "directoryBuildProps",
            Required = false,
            Default = true,
            HelpText = "Take Directory.Build.props files into account, e.g. -d false to ignore them")]
        public bool? ReadDirectoryBuildProps { get; init; } = true;

        [Option(
            'o',
            "reportOverridenDirectoryBuildProps",
            Required = false,
            Default = true,
            HelpText = "Report when a csproj overrides a Directory.Build.props, e.g. -o false to skip it")]
        public bool? ReportOverridenDirectoryBuildProps { get; init; } = true;

        [Option(
            "property",
            Required = false,
            HelpText =
                "MSBuild properties in the Name=Value format, used when evaluating the conditions in project files, e.g. --property NuGetBuild=true Configuration=Release")]
        public ICollection<string>? GlobalProperties { get; init; }

        [Option(
            'f',
            "format",
            Required = false,
            Default = OutputFormat.Text,
            HelpText =
                "Output format, Text or Json. Json prints a single JSON document to stdout and suppresses progress messages.")]
        public OutputFormat Format { get; init; } = OutputFormat.Text;
    }
}
