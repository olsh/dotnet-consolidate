using System.Collections.Generic;
using System.IO;

namespace DotNet.Consolidate.Services;

public class SolutionFileProvider : ISolutionFileProvider
{
    private readonly ILogger _logger;

    public SolutionFileProvider(ILogger logger)
    {
        _logger = logger;
    }

    public ICollection<string> FindSolutionsInCurrentDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionPaths = Directory.GetFiles(currentDirectory, "*.sln");

        _logger.Message($"{solutionPaths.Length} solution files found in {currentDirectory}.");

        return solutionPaths;
    }
}
