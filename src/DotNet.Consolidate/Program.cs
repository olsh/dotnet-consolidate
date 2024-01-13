using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;
using DotNet.Consolidate.Services.OutputWriters;

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

        private static IOutputWriter GetOutputWriter(Options options)
        {
            switch (options.OutputFormat)
            {
                case OutputFormat.Json:
                    return new JsonOutputWriter();
                case OutputFormat.Text:
                    return new TextOutputWriter();
                default:
                    throw new InvalidOperationException($"Output format {options.OutputFormat} is not supported");
            }
        }

        private static void Main(string[] args)
        {
            Parser parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.CaseSensitive = false;
            });

            parser.ParseArguments<Options>(args)
            .WithParsed(Consolidate)
            .WithNotParsed(HandleParseError);
        }

        // ReSharper disable once CognitiveComplexity
        private static void Consolidate(Options options)
        {
            ILogger logger = new Logger();
            IOutputWriter outputWriter = GetOutputWriter(options);
            Console.OutputEncoding = new System.Text.UTF8Encoding(false); // No BOM UTF8

            if (options.OutputFormat == OutputFormat.Json)
            {
                logger.SupressMessages = true;
            }

            if (options.ExcludedPackageIds?.Any() == true && options.PackageIds?.Any() == true)
            {
                logger.Message("There is no sense to provide both `-p` and `-e` arguments at the same time.");
                Environment.ExitCode = 1;

                return;
            }

            var solutionInfoProvider = new SolutionInfoProvider(new ProjectParser(logger), logger, options.ReadDirectoryBuildProps);

            ICollection<string> solutions;
            if (options.Solutions?.Any() == true)
            {
                solutions = options.Solutions;
            }
            else
            {
                solutions = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln", SearchOption.TopDirectoryOnly);
                if (!solutions.Any())
                {
                    logger.Message($"No solution files were found in {Directory.GetCurrentDirectory()}");
                }
            }

            var solutionsInfo = solutionInfoProvider.GetSolutionsInfo(solutions);

            var packagesAnalyzer = new PackagesAnalyzer();

            Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> solutionResults = new Dictionary<SolutionInfo, IEnumerable<AnalysisResult>>();
            foreach (var solutionInfo in solutionsInfo)
            {
                logger.Message($"Analyzing packages in {solutionInfo.SolutionFile}");
                if (!solutionInfo.IsParsedWithoutIssues)
                {
                    logger.Message($"Solution {solutionInfo.SolutionFile} wasn't parsed correctly, the results may be invalid");

                    Environment.ExitCode = 1;
                }

                List<AnalysisResult> nonConsolidatedPackages = packagesAnalyzer.FindNonConsolidatedPackages(solutionInfo.ProjectInfos, options);
                solutionResults.Add(solutionInfo, nonConsolidatedPackages);
            }

            outputWriter.WriteAnalysisResults(solutionResults, options, Console.OpenStandardOutput());
            if (solutionResults.SelectMany(sln => sln.Value).Any())
            {
                Environment.ExitCode = 1;
            }
        }
    }
}
