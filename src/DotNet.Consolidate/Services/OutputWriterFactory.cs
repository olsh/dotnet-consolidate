using System;
using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public static class OutputWriterFactory
    {
        public static IOutputWriter Create(OutputFormat format, TextWriter output, IReadOnlyCollection<string> warnings)
        {
            return format switch
            {
                OutputFormat.Text => new TextOutputWriter(output),
                OutputFormat.Json => new JsonOutputWriter(output, warnings),
                _ => throw new NotSupportedException($"The output format {format} is not supported.")
            };
        }
    }
}
