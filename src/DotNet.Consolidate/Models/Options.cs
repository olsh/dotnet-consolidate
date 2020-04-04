using System.Collections.Generic;

using CommandLine;

namespace DotNet.Consolidate.Models
{
    public class Options
    {
        public Options(ICollection<string> solutions)
        {
            Solutions = solutions;
        }

        [Option('s', "solutions", Required = true, HelpText = "Target solution for consolidation.")]
        public ICollection<string> Solutions { get; }
    }
}
