using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public interface IOutputWriter
    {
        void WriteAnalysisResults(Dictionary<SolutionInfo, IEnumerable<AnalysisResult>> nonConsolidatedPackages, Options options, Stream stream);
    }
}
