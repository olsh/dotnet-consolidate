using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services.OutputWriters
{
    public class JsonOutputWriter : IOutputWriter
    {
        public void WriteAnalysisResults(Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> nonConsolidatedPackages, Options options, Stream stream)
        {
            JsonSerializerOptions serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
                {
                    new JsonStringEnumConverter() // use names and not values of enums
                }
            };

            var result = new
            {
                Solutions = nonConsolidatedPackages.Select(kv => new
                {
                    Solution = kv.Key,
                    NonConsolidatedPackages = kv.Value.ToArray()
                }).ToArray()
            };

            JsonSerializer.Serialize(stream, result, serializerOptions);
        }
    }
}
