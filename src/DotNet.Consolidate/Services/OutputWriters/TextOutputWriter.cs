using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services.OutputWriters
{
    public class TextOutputWriter : IOutputWriter
    {
        public void WriteAnalysisResults(Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> nonConsolidatedPackages, Options options, Stream stream)
        {
            using StreamWriter sw = new StreamWriter(stream, Console.OutputEncoding, leaveOpen: true);

            var allNonConsolidatedPackages = nonConsolidatedPackages.SelectMany(x => x.Value).DistinctBy(p => p.NuGetPackageId).ToList();
            if (allNonConsolidatedPackages.Any())
            {
                sw.WriteLine($"Found {allNonConsolidatedPackages.Count} non-consolidated packages in all solutions");
                sw.WriteLine();
            }

            foreach (var slnPackages in nonConsolidatedPackages)
            {
                sw.WriteLine("****************************");
                sw.WriteLine(slnPackages.Key.SolutionFile);
                sw.WriteLine("****************************");

                foreach (var package in slnPackages.Value)
                {
                    sw.WriteLine("----------------------------");
                    sw.WriteLine(package.NuGetPackageId);
                    sw.WriteLine("----------------------------");

                    foreach (var packageVersion in package.PackageVersions.OrderBy(p => p.NuGetPackageVersion).ThenBy(p => p.ProjectName))
                    {
                        sw.WriteLine($"{packageVersion.ProjectName} - {packageVersion.NuGetPackageVersion}");
                    }

                    sw.WriteLine();
                }

                if (options.PackageIds?.Any() == true)
                {
                    var solutionPackageIds = slnPackages.Key.ProjectInfos.SelectMany(x => x.Packages.Select(p => p.Id));
                    var packageIdsInArgumentNotInSolution = options.PackageIds.Where(a => !solutionPackageIds.Contains(a));

                    sw.WriteLine("The following package IDs given for consolidation check were not found in the solution projects:");
                    sw.WriteLine(string.Join(Environment.NewLine, packageIdsInArgumentNotInSolution));
                    sw.WriteLine();
                }

                if (!slnPackages.Value.Any())
                {
                    var packageList = options.PackageIds?.Any() == true ? $"from the list {string.Join(Environment.NewLine, options.PackageIds)} " : string.Empty;
                    sw.WriteLine($"All packages {packageList}in {slnPackages.Key.SolutionFile} are consolidated.");
                }
            }
        }
    }
}
