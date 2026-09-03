using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

namespace DotNet.Consolidate
{
    internal static class Program
    {
        private static (bool IsSuccess, Dictionary<string, string> Properties) TryParseGlobalProperties(
            ICollection<string>? values,
            ILogger logger)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return (true, properties);
            }

            foreach (var value in values)
            {
                var separatorIndex = value.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    logger.Message($"The property `{value}` is not in the expected Name=Value format.");

                    return (false, properties);
                }

                properties[value.Substring(0, separatorIndex)] = value.Substring(separatorIndex + 1);
            }

            return (true, properties);
        }

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

        // ReSharper disable once CognitiveComplexity
        private static void Consolidate(Options options)
        {
            var logger = new Logger();
            if (options.ExcludedPackageIds?.Any() == true && options.PackageIds?.Any() == true)
            {
                logger.Message("There is no sense to provide both `-p` and `-e` arguments at the same time.");
                Environment.ExitCode = 1;

                return;
            }

            var (isSuccess, globalProperties) = TryParseGlobalProperties(options.GlobalProperties, logger);
            if (!isSuccess)
            {
                Environment.ExitCode = 1;

                return;
            }

            var solutionInfoProvider = new SolutionInfoProvider(
                new ProjectParser(logger, globalProperties),
                logger,
                options.ReadDirectoryBuildProps);

            ICollection<string> solutions;
            if (options.Solutions?.Any() == true)
            {
                solutions = options.Solutions;
            }
            else
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                solutions = Directory.GetFiles(currentDirectory, "*.sln", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(currentDirectory, "*.slnx", SearchOption.TopDirectoryOnly))
                    .ToList();
                if (!solutions.Any())
                {
                    logger.Message($"No solution files were found in {currentDirectory}");
                }
            }

            var solutionsInfo = solutionInfoProvider.GetSolutionsInfo(solutions);

            var packagesAnalyzer = new PackagesAnalyzer();

            foreach (var solutionInfo in solutionsInfo)
            {
                logger.Message($"Analyzing packages in {solutionInfo.SolutionFile}");
                if (!solutionInfo.IsParsedWithoutIssues)
                {
                    logger.Message(
                        $"Solution {solutionInfo.SolutionFile} wasn't parsed correctly, the results may be invalid");

                    Environment.ExitCode = 1;
                }

                var nonConsolidatedPackages =
                    packagesAnalyzer.FindNonConsolidatedPackages(solutionInfo.ProjectInfos, options);
                logger.WriteAnalysisResults(nonConsolidatedPackages, solutionInfo, options);
                if (nonConsolidatedPackages.Any())
                {
                    Environment.ExitCode = 1;
                }
            }
        }
    }
}
