using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Renders the analysis results. Separate from <see cref="ILogger"/>, which only reports what happened
    /// along the way.
    /// </summary>
    public interface IOutputWriter
    {
        void WriteAnalysisResults(SolutionAnalysisResult result);

        /// <summary>
        /// Signals that no further results are coming.
        /// </summary>
        /// <remarks>
        /// A format that has to emit a single document for the whole run, such as JSON, buffers the results and
        /// writes them here. Streaming formats write as they go and do nothing at this point.
        /// </remarks>
        void Flush();
    }
}
