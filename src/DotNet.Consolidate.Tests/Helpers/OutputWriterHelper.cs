using System;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;
using DotNet.Consolidate.Services.OutputWriters;

namespace DotNet.Consolidate.Tests.Helpers
{
    public class OutputWriterHelper
    {
        public static IOutputWriter GetOutputWriter(Options options)
        {
            switch (options.OutputFormat)
            {
                case OutputFormat.Json:
                    return new JsonOutputWriter();
                case OutputFormat.Text:
                    return new TextOutputWriter();
                default:
                    throw new Exception($"Output format {options.OutputFormat} is not supported");
            }
        }
    }
}
