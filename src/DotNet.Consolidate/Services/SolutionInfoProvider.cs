using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        private readonly bool _readDirectoryBuildProps;

        public SolutionInfoProvider(IProjectParser projectParser, ILogger logger, bool readDirectoryBuildProps)
        {
            _projectParser = projectParser;
            _logger = logger;
            _readDirectoryBuildProps = readDirectoryBuildProps;
        }

        public List<SolutionInfo> GetSolutionsInfo(ICollection<string> solutionsPath)
        {
            var solutionInfos = new List<SolutionInfo>();
            foreach (var solutionFile in solutionsPath)
            {
                var (isSuccessParsing, solutionInfo) = TryGetSolutionInfo(solutionFile);
                if (!isSuccessParsing || solutionInfo == null)
                {
                    solutionInfos.Add(new SolutionInfo(solutionFile, solutionInfo, new List<ProjectInfo>(), new List<DirectoryBuildPropsInfo>()));

                    continue;
                }

                var projectsInfo = TryGetProjectsInfo(solutionFile, solutionInfo);
                var directoryBuildPropsInfos = _readDirectoryBuildProps
                    ? TryGetDirectoryBuildPropsInfo(new FileInfo(solutionFile).Directory)
                    : new List<DirectoryBuildPropsInfo>();
                ApplyInheritedPackages(projectsInfo, directoryBuildPropsInfos);
                solutionInfos.Add(new SolutionInfo(solutionFile, solutionInfo,  projectsInfo, directoryBuildPropsInfos));
            }

            return solutionInfos;
        }

        private static void ApplyInheritedPackages(ICollection<ProjectInfo> projectsInfo, ICollection<DirectoryBuildPropsInfo> directoryBuildPropsInfos)
        {
            if (!projectsInfo.Any())
            {
                return;
            }

            if (!directoryBuildPropsInfos.Any())
            {
                return;
            }

            foreach (var projectInfo in projectsInfo)
            {
                var directoryBuildProps = directoryBuildPropsInfos
                    .Where(dbp => !string.IsNullOrEmpty(dbp.DirectoryName))
                    .OrderByDescending(dbp => dbp.DirectoryName.Length)
                    .FirstOrDefault(dbp => projectInfo.ProjectDirectory.StartsWith(dbp.DirectoryName));

                if (directoryBuildProps != null)
                {
                    foreach (var packageReference in directoryBuildProps.Packages)
                    {
                        projectInfo.Packages.Add(new NuGetPackageInfo(packageReference.Id, packageReference.Version, NuGetPackageReferenceType.Inherited));
                    }
                }
            }
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
                        projectInfos.Add(new ProjectInfo(project.Name, projectDirectory, packages));
                    }
                    else if (File.Exists(projectFilePath))
                    {
                        var packages = _projectParser.ParseProjectFile(projectFilePath);
                        projectInfos.Add(new ProjectInfo(project.Name, projectDirectory, packages));
                    }
                    else
                    {
                        projectInfos.Add(new ProjectInfo(project.Name, projectDirectory, new List<NuGetPackageInfo>()));
                        _logger.Message($"Unable to find package.config file for project {project.Path}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Message($"Unable to get project info for {project.Path}\r\n {e}");
                }
            }

            return projectInfos;
        }

        // ReSharper disable once CognitiveComplexity
        private List<DirectoryBuildPropsInfo> TryGetDirectoryBuildPropsInfo(DirectoryInfo? path)
        {
            var directoryBuildPropsInfo = new List<DirectoryBuildPropsInfo>();

            if (path == null)
            {
                return directoryBuildPropsInfo;
            }

            var directorySearchOptions = new EnumerationOptions()
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false,
            };

            foreach (var fileInfo in path.GetFiles("Directory.Build.props", directorySearchOptions))
            {
                try
                {
                    var packages = _projectParser.ParseProjectFile(fileInfo.FullName);

                    directoryBuildPropsInfo.Add(new DirectoryBuildPropsInfo(fileInfo.Name,  fileInfo.Directory?.FullName ?? string.Empty, packages));
                }
                catch (Exception e)
                {
                    _logger.Message($"Unable to get Directory Build props info for {path.FullName}\r\n {e}");
                }
            }

            var subPaths = path.GetDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var subPath in subPaths)
            {
                directoryBuildPropsInfo.AddRange(TryGetDirectoryBuildPropsInfo(subPath));
            }

            return directoryBuildPropsInfo;
        }
    }
}
