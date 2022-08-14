using System.IO;

using DotNet.Consolidate.Services;

using FluentAssertions;

using Moq;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class SolutionFileProviderTests
{
    [Fact]
    public void FindSolutionsInCurrentDirectory_FolderWithoutSln_ReturnsEmpty()
    {
        var provider = new SolutionFileProvider(Mock.Of<ILogger>());

        Directory.SetCurrentDirectory(".");
        var solutions = provider.FindSolutionsInCurrentDirectory();

        solutions.Should().BeEmpty();
    }

    [Fact]
    public void FindSolutionsInCurrentDirectory_FolderWithSlnFiles_ReturnsTwoStrings()
    {
        var provider = new SolutionFileProvider(Mock.Of<ILogger>());

        Directory.SetCurrentDirectory("TestData");
        var solutions = provider.FindSolutionsInCurrentDirectory();

        solutions.Should().HaveCount(2);
    }
}
