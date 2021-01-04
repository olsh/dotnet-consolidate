using System.Collections.Generic;

using CommandLine;

namespace DotNet.Consolidate.Models
{
    public class Options
    {
        public Options(ICollection<string> solutions, ICollection<string> packageIds)
        {
            Solutions = solutions;
            PackageIds = packageIds;
        }

        [Option('p', "pacakgeIds", Required = false, HelpText = "Package IDs to check consolidation.")]
        public ICollection<string>? PackageIds { get; }

        [Option('s', "solutions", Required = true, HelpText = "Target solution for consolidation.")]
        public ICollection<string> Solutions { get; }
    }
}
