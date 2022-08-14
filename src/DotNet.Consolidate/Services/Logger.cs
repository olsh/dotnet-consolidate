using System;
using System.Collections.Generic;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services;

public class Logger : ILogger
{
    public void Message(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteAnalysisResults(List<AnalysisResult> nonConsolidatedPackages, SolutionInfo solutionInfo, Options options)
    {
        if (nonConsolidatedPackages.Any())
        {
            Console.WriteLine("Found {0} non-consolidated packages", nonConsolidatedPackages.Count);
            Console.WriteLine();
        }

        foreach (var package in nonConsolidatedPackages)
        {
            Console.WriteLine("----------------------------");
            Console.WriteLine(package.NuGetPackageId);
            Console.WriteLine("----------------------------");

            foreach (var packageVersion in package.PackageVersions.OrderBy(p => p.NuGetPackageVersion).ThenBy(p => p.ProjectName))
            {
                Console.WriteLine("{0} - {1}", packageVersion.ProjectName, packageVersion.NuGetPackageVersion);
            }

            Console.WriteLine();
        }

        if (options.PackageIds?.Any() == true)
        {
            var solutionPackageIds = solutionInfo.ProjectInfos.SelectMany(x => x.Packages.Select(p => p.Id));
            var packageIdsInArgumentNotInSolution = options.PackageIds.Where(a => !solutionPackageIds.Contains(a));

            Console.WriteLine("The following package IDs given for consolidation check were not found in the solution projects:");
            Console.WriteLine(string.Join(Environment.NewLine, packageIdsInArgumentNotInSolution));
        }

        if (!nonConsolidatedPackages.Any())
        {
            var packageList = options.PackageIds?.Any() == true ? $"from the list {string.Join(Environment.NewLine, options.PackageIds)} " : string.Empty;
            Console.WriteLine($"All packages {packageList}in {solutionInfo.SolutionFile} are consolidated.");
        }
    }
}