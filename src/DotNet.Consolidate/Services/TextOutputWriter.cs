using System;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Writes the human readable report, one solution at a time.
    /// </summary>
    public class TextOutputWriter : IOutputWriter
    {
        private readonly TextWriter _output;

        public TextOutputWriter(TextWriter output)
        {
            _output = output;
        }

        public void WriteAnalysisResults(SolutionAnalysisResult result)
        {
            if (result.NonConsolidatedPackages.Any())
            {
                _output.WriteLine("Found {0} non-consolidated packages", result.NonConsolidatedPackages.Count);
                _output.WriteLine();
            }

            foreach (var package in result.NonConsolidatedPackages)
            {
                _output.WriteLine("----------------------------");
                _output.WriteLine(package.NuGetPackageId);
                _output.WriteLine("----------------------------");

                foreach (var packageVersion in package.PackageVersions.OrderBy(p => p.NuGetPackageVersion)
                             .ThenBy(p => p.ProjectName))
                {
                    _output.WriteLine("{0} - {1}", packageVersion.ProjectName, packageVersion.NuGetPackageVersion);
                }

                _output.WriteLine();
            }

            if (result.RequestedPackageIds.Any())
            {
                _output.WriteLine(
                    "The following package IDs given for consolidation check were not found in the solution projects:");
                _output.WriteLine(string.Join(Environment.NewLine, result.PackageIdsNotFoundInSolution));
            }

            if (!result.NonConsolidatedPackages.Any())
            {
                var packageList = result.RequestedPackageIds.Any()
                    ? $"from the list {string.Join(Environment.NewLine, result.RequestedPackageIds)} "
                    : string.Empty;
                _output.WriteLine($"All packages {packageList}in {result.SolutionFile} are consolidated.");
            }
        }

        public void Flush()
        {
            // The text report is written as each solution is analyzed, so there is nothing left to emit.
        }
    }
}
