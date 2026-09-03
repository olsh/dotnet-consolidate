using System;
using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class OutputWriterFactoryTests
{
    [Fact]
    public void Text_format_creates_a_text_writer()
    {
        var writer = OutputWriterFactory.Create(OutputFormat.Text, new StringWriter(), new List<string>());

        Assert.IsType<TextOutputWriter>(writer);
    }

    [Fact]
    public void Json_format_creates_a_json_writer()
    {
        var writer = OutputWriterFactory.Create(OutputFormat.Json, new StringWriter(), new List<string>());

        Assert.IsType<JsonOutputWriter>(writer);
    }

    [Fact]
    public void Unknown_format_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(() => OutputWriterFactory.Create(
            (OutputFormat)42,
            new StringWriter(),
            new List<string>()));
    }
}
