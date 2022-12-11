using System.Collections.Generic;

using CommandLine;

namespace DotNet.Consolidate.Models
{
    public class Options
    {
        public Options(ICollection<string> solutions, ICollection<string>? packageIds, ICollection<string>? excludedPackageIds)
        {
            Solutions = solutions;
            PackageIds = packageIds;
            ExcludedPackageIds = excludedPackageIds;
        }

        [Option('p', "packageIds", Required = false, HelpText = "Package IDs for checking.")]
        public ICollection<string>? PackageIds { get; }

        [Option('e', "excluded", Required = false, HelpText = "Package IDs that will be skipped during checking.")]
        public ICollection<string>? ExcludedPackageIds { get; }

        [Option('s', "solutions", Required = true, HelpText = "Target solutions for checking.")]
        public ICollection<string> Solutions { get; }
    }
}
