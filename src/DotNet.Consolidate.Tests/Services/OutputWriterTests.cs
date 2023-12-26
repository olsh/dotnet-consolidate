using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;
using DotNet.Consolidate.Tests.Helpers;

using Xunit;

namespace DotNet.Consolidate.Tests.Services
{
    public class OutputWriterTests
    {
        [Theory]
        [InlineData(OutputFormat.Json)]
        [InlineData(OutputFormat.Text)]
        public void OutputWriter_correctly_writes_results(OutputFormat outputFormat)
        {
            var options = new Options(new List<string>(), new List<string>(), new List<string>(), string.Empty, true, true, outputFormat);
            var outputWriter = OutputWriterHelper.GetOutputWriter(options);
            var results = GetResults(options);

            using var mem = new System.IO.MemoryStream();
            outputWriter.WriteAnalysisResults(results, options, mem);
            mem.Seek(0L, System.IO.SeekOrigin.Begin);

            Assert.NotEqual(0L, mem.Length);
            switch (outputFormat)
            {
                case OutputFormat.Json:
                    var reader = new Utf8JsonReader(mem.ToArray());
                    var parsed = JsonObject.Parse(ref reader);
                    Assert.NotNull(parsed);
                    var packages = parsed["Solutions"].AsArray().First()["NonConsolidatedPackages"].AsArray();
                    Assert.NotNull(packages);
                    Assert.True(packages.Count >= 1);
                    Assert.Equal("myid", packages.First()[nameof(AnalysisResult.NuGetPackageId)].ToString());
                    break;
                case OutputFormat.Text:
                    // Not really a good parsable format, but we can at least check if it's not empty
                    break;
            }
        }

        private Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> GetResults(Options options)
        {
            Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> solutionResults = new Dictionary<SolutionInfo, IEnumerable<AnalysisResult>>();
            var analyzer = new PackagesAnalyzer();

            var info = new ProjectInfo("Test", "Test", new List<NuGetPackageInfo>()
            {
                new ("myid", new Models.Version("1.1.0"), NuGetPackageReferenceType.Direct),
                new ("myid", new Models.Version("1.0.1.0"), NuGetPackageReferenceType.Inherited)
            });
            var projectInfos = new List<ProjectInfo> { info };

            solutionResults.Add(new SolutionInfo("Test.sln", new Onion.SolutionParser.Parser.Model.Solution(), projectInfos, Array.Empty<DirectoryBuildPropsInfo>()), analyzer.FindNonConsolidatedPackages(projectInfos, options));
            return solutionResults;
        }
    }
}
