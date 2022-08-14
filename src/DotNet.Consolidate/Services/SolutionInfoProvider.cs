using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Constants;
using DotNet.Consolidate.Models;

using Onion.SolutionParser.Parser;

namespace DotNet.Consolidate.Services;

public class SolutionInfoProvider
{
    private readonly IProjectParser _projectParser;
    private readonly ISolutionFileProvider _solutionFileProvider;

    private readonly ILogger _logger;

    public SolutionInfoProvider(IProjectParser projectParser, ISolutionFileProvider solutionFileProvider, ILogger logger)
    {
        _projectParser = projectParser;
        _solutionFileProvider = solutionFileProvider;
        _logger = logger;
    }

    public List<SolutionInfo> GetSolutionsInfo(ICollection<string>? solutionsPaths)
    {
        if (solutionsPaths is null || !solutionsPaths.Any())
        {
            _logger.Message("No solution given.");
            solutionsPaths = _solutionFileProvider.FindSolutionsInCurrentDirectory();
        }

        var solutionInfos = new List<SolutionInfo>();
        foreach (var path in solutionsPaths)
        {
            var projectsInfo = TryGetProjectsInfo(path);
            solutionInfos.Add(new SolutionInfo(path, projectsInfo));
        }

        return solutionInfos;
    }

    private List<ProjectInfo> TryGetProjectsInfo(string filePath)
    {
        var solutionInfos = new List<ProjectInfo>();

        try
        {
            filePath = PathUtils.EnsureSystemSeparator(filePath);
            var solution = SolutionParser.Parse(filePath);

            var solutionDirectory = Path.GetDirectoryName(filePath);
            if (solutionDirectory == null)
            {
                _logger.Message($"Solution directory wasn't found for file {filePath}");

                return solutionInfos;
            }

            foreach (var project in solution.Projects)
            {
                if (project.TypeGuid == ProjectTypeGuids.SolutionFolder)
                {
                    continue;
                }

                // Solution files use the windows path separator '\' by default,
                // so we must convert to system path separator to work on posix systems.
                var projectFilePath =
                    PathUtils.EnsureSystemSeparator(Path.Combine(solutionDirectory, project.Path));
                var projectDirectory = Path.GetDirectoryName(projectFilePath);
                if (projectDirectory == null)
                {
                    _logger.Message($"Project directory wasn't found for project {project.Name}");

                    return solutionInfos;
                }

                var packageConfigPath =
                    PathUtils.EnsureSystemSeparator(Path.Combine(projectDirectory, "packages.config"));
                if (File.Exists(packageConfigPath))
                {
                    var packages = _projectParser.ParsePackageConfig(packageConfigPath);
                    solutionInfos.Add(new ProjectInfo(project.Name, packages));
                }
                else if (File.Exists(projectFilePath))
                {
                    var packages = _projectParser.ParseProjectFile(projectFilePath);
                    solutionInfos.Add(new ProjectInfo(project.Name, packages));
                }
                else
                {
                    _logger.Message($"Unable to find project or package.config file for project {project.Path}");
                }
            }

            return solutionInfos;
        }
        catch (Exception e)
        {
            _logger.Message($"Unable to get project info for {filePath}\r\n {e}");
        }

        return solutionInfos;
    }
}
