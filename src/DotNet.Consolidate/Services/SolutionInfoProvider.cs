using System;
using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Constants;
using DotNet.Consolidate.Models;

using Onion.SolutionParser.Parser;
using Onion.SolutionParser.Parser.Model;

namespace DotNet.Consolidate.Services
{
    public class SolutionInfoProvider
    {
        private readonly IProjectParser _projectParser;

        private readonly ILogger _logger;

        public SolutionInfoProvider(IProjectParser projectParser, ILogger logger)
        {
            _projectParser = projectParser;
            _logger = logger;
        }

        public List<SolutionInfo> GetSolutionsInfo(ICollection<string> solutionsPath)
        {
            var solutionInfos = new List<SolutionInfo>();
            foreach (var solutionFile in solutionsPath)
            {
                var (isSuccessParsing, solutionInfo) = TryGetSolutionInfo(solutionFile);
                if (!isSuccessParsing || solutionInfo == null)
                {
                    solutionInfos.Add(new SolutionInfo(solutionFile, solutionInfo, new List<ProjectInfo>()));

                    continue;
                }

                var projectsInfo = TryGetProjectsInfo(solutionFile, solutionInfo);
                solutionInfos.Add(new SolutionInfo(solutionFile, solutionInfo,  projectsInfo));
            }

            return solutionInfos;
        }

        private (bool isSuccessParsing, ISolution? solution) TryGetSolutionInfo(string filePath)
        {
            ISolution? solution;
            try
            {
                filePath = PathUtils.EnsureSystemSeparator(filePath);
                var solutionDirectory = Path.GetDirectoryName(filePath);
                if (solutionDirectory == null)
                {
                    _logger.Message($"Solution directory wasn't found for file {filePath}");

                    return (false, null);
                }

                if (!File.Exists(filePath))
                {
                    _logger.Message($"Solution file {filePath} doesn't exists");

                    return (false, null);
                }

                solution = SolutionParser.Parse(filePath);
            }
            catch (Exception e)
            {
                _logger.Message($"Unable to get solution info for {filePath}\r\n {e}");

                return (false, null);
            }

            return (true, solution);
        }

        // ReSharper disable once CognitiveComplexity
        private List<ProjectInfo> TryGetProjectsInfo(string filePath, ISolution solution)
        {
            var projectInfos = new List<ProjectInfo>();

            var solutionDirectory = Path.GetDirectoryName(PathUtils.EnsureSystemSeparator(filePath));
            if (solutionDirectory == null)
            {
                _logger.Message($"Solution directory wasn't found for file {filePath}");

                return projectInfos;
            }

            foreach (var project in solution.Projects)
            {
                try
                {
                    if (project.TypeGuid == ProjectTypeGuids.SolutionFolder)
                    {
                        continue;
                    }

                    // Solution files use the windows path separator '\' by default,
                    // so we must convert to system path separator to work on posix systems.
                    var projectFilePath = PathUtils.EnsureSystemSeparator(Path.Combine(solutionDirectory, project.Path));
                    var projectDirectory = Path.GetDirectoryName(projectFilePath);
                    if (projectDirectory == null)
                    {
                        _logger.Message($"Project directory wasn't found for project {project.Name}");

                        return projectInfos;
                    }

                    var packageConfigPath =
                        PathUtils.EnsureSystemSeparator(Path.Combine(projectDirectory, "packages.config"));
                    if (File.Exists(packageConfigPath))
                    {
                        var packages = _projectParser.ParsePackageConfig(packageConfigPath);
                        projectInfos.Add(new ProjectInfo(project.Name, packages));
                    }
                    else if (File.Exists(projectFilePath))
                    {
                        var packages = _projectParser.ParseProjectFile(projectFilePath);
                        projectInfos.Add(new ProjectInfo(project.Name, packages));
                    }
                    else
                    {
                        _logger.Message($"Unable to find project or package.config file for project {project.Path}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Message($"Unable to get project info for {filePath}\r\n {e}");
                }
            }

            return projectInfos;
        }
    }
}
