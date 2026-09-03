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

        /// <remarks>
        /// An unusable command line has to fail the run: without the exit code it passes a build with 0, having
        /// analyzed nothing. What is reported, and where, belongs to <see cref="CommandLineErrorReporter"/> — the
        /// arguments are handed to it because the format the caller asked for can only be recovered from them once
        /// the parse has failed.
        /// </remarks>
        private static void HandleParseError(string[] args, IEnumerable<Error> errors)
        {
            if (CommandLineErrorReporter.Report(args, errors, Console.Out))
            {
                Environment.ExitCode = 1;
            }
        }

        private static void Main(string[] args)
        {
            using var parser = CommandLineParserFactory.Create();

            parser.ParseArguments<Options>(args)
                .WithParsed(Consolidate)
                .WithNotParsed(errors => HandleParseError(args, errors));
        }

        // ReSharper disable once CognitiveComplexity
        private static void Consolidate(Options options)
        {
            var collectingLogger = new CollectingLogger();

            // In JSON mode the messages travel inside the document instead of being printed, so stdout stays a
            // single parseable document and nothing is written to stderr.
            ILogger logger = options.Format == OutputFormat.Json ? collectingLogger : new Logger();
            var outputWriter = OutputWriterFactory.Create(options.Format, Console.Out, collectingLogger.Messages);

            if (options.ExcludedPackageIds?.Any() == true && options.PackageIds?.Any() == true)
            {
                logger.Message("There is no sense to provide both `-p` and `-e` arguments at the same time.");
                outputWriter.Flush();
                Environment.ExitCode = 1;

                return;
            }

            var (isSuccess, globalProperties) = TryParseGlobalProperties(options.GlobalProperties, logger);
            if (!isSuccess)
            {
                outputWriter.Flush();
                Environment.ExitCode = 1;

                return;
            }

            var solutionInfoProvider = new SolutionInfoProvider(
                new ProjectParser(logger, globalProperties),
                logger,
                options.ReadDirectoryBuildProps ?? true);

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

            foreach (var solutionInfo in solutionsInfo)
            {
                logger.Progress($"Analyzing packages in {solutionInfo.SolutionFile}");
                if (!solutionInfo.IsParsedWithoutIssues)
                {
                    logger.Message(
                        $"Solution {solutionInfo.SolutionFile} wasn't parsed correctly, the results may be invalid");

                    Environment.ExitCode = 1;
                }

                var nonConsolidatedPackages =
                    PackagesAnalyzer.FindNonConsolidatedPackages(solutionInfo.ProjectInfos, options);
                var analysisResult = CreateAnalysisResult(solutionInfo, nonConsolidatedPackages, options);
                outputWriter.WriteAnalysisResults(analysisResult);

                // A `-p` package that no project references is a failure too: it's usually a typo, and exiting 0
                // would let it pass a build silently.
                if (nonConsolidatedPackages.Any() || analysisResult.PackageIdsNotFoundInSolution.Any())
                {
                    Environment.ExitCode = 1;
                }
            }

            outputWriter.Flush();
        }

        private static SolutionAnalysisResult CreateAnalysisResult(
            SolutionInfo solutionInfo,
            List<AnalysisResult> nonConsolidatedPackages,
            Options options)
        {
            var requestedPackageIds = options.PackageIds?.ToList() ?? new List<string>();

            // The analyzer owns this so it compares IDs exactly the way its `-p`/`-e` filters do — otherwise
            // a package the filter matched could still be reported as missing from the solution.
            var packageIdsNotFoundInSolution =
                PackagesAnalyzer.FindPackageIdsNotInSolution(solutionInfo.ProjectInfos, requestedPackageIds);

            // With `-d false` there are no props files to inherit from, so this is empty either way and needs
            // no case of its own.
            var directoryBuildPropsOverrides = options.ReportOverridenDirectoryBuildProps ?? true
                ? PackagesAnalyzer.FindDirectoryBuildPropsOverrides(solutionInfo.ProjectInfos, options)
                : new List<DirectoryBuildPropsOverride>();

            return new SolutionAnalysisResult(
                solutionInfo.SolutionFile,
                solutionInfo.IsParsedWithoutIssues,
                nonConsolidatedPackages,
                requestedPackageIds,
                packageIdsNotFoundInSolution,
                directoryBuildPropsOverrides);
        }
    }
}
