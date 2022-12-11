using System;
using System.Collections.Generic;
using System.Linq;

using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

namespace DotNet.Consolidate
{
    internal static class Program
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
                .WithNotParsed(HandleParseError);
        }

        private static void Consolidate(Options options)
        {
            var logger = new Logger();
            if (options.ExcludedPackageIds?.Any() == true && options.PackageIds?.Any() == true)
            {
                logger.Message("There is no sense to provide both `-p` and `-e` argument at the same time.");
                Environment.ExitCode = 1;

                return;
            }

            var solutionInfoProvider = new SolutionInfoProvider(new ProjectParser(), logger);
            var solutionsInfo = solutionInfoProvider.GetSolutionsInfo(options.Solutions);

            var packagesAnalyzer = new PackagesAnalyzer();

            foreach (var solutionInfo in solutionsInfo)
            {
                logger.Message($"Analyzing packages in {solutionInfo.SolutionFile}");

                var nonConsolidatedPackages = packagesAnalyzer.FindNonConsolidatedPackages(solutionInfo.ProjectInfos, options);
                logger.WriteAnalysisResults(nonConsolidatedPackages, solutionInfo, options);
                if (nonConsolidatedPackages.Any())
                {
                    Environment.ExitCode = 1;
                }
            }
        }
    }
}
