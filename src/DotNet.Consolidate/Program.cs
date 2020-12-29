using System;
using System.Collections.Generic;
using System.Linq;

using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

namespace DotNet.Consolidate
{
    internal class Program
    {
        private static void HandleParseError(IEnumerable<Error> errors)
        {
            Console.WriteLine("The following parsing errors occurred when parsing the solution file");
            foreach (var error in errors)
            {
                Console.WriteLine("Type {0} StopProcessing {1}", error.Tag, error.StopsProcessing);
            }
        }

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Consolidate)
                .WithNotParsed(errors => HandleParseError(errors));
        }

        private static void Consolidate(Options options)
        {
            var logger = new Logger();
            var solutionInfoProvider = new SolutionInfoProvider(new ProjectParser(), logger);
            var solutionsInfo = solutionInfoProvider.GetSolutionsInfo(options.Solutions);

            var packagesAnalyzer = new PackagesAnalyzer();

            foreach (var solutionInfo in solutionsInfo)
            {
                logger.Message($"Analyzing packages in {solutionInfo.SolutionFile}");

                var packagesDefined = options.PackageIds?.Any() ?? false;
                var nonConsolidatedPackages = packagesAnalyzer.FindNonConsolidatedPackages(solutionInfo.ProjectInfos);
                if (packagesDefined)
                {
                    nonConsolidatedPackages = nonConsolidatedPackages.Where(p => options.PackageIds!.Contains(p.NuGetPackageId)).ToList();
                }

                logger.WriteAnalysisResults(nonConsolidatedPackages);

                if (nonConsolidatedPackages.Any())
                {
                    Environment.ExitCode = 1;
                }
                else
                {
                    var packageList = packagesDefined ? $"from the list {string.Join(Environment.NewLine, options.PackageIds!)} " : string.Empty;
                    logger.Message($"All packages {packagesDefined}in {solutionInfo.SolutionFile} are consolidated.");
                }
            }
        }
    }
}
