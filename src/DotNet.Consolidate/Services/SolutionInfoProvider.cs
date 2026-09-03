using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using DotNet.Consolidate.Models;

using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

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
                    solutionInfos.Add(
                        new SolutionInfo(
                            solutionFile,
                            solutionInfo,
                            new List<ProjectInfo>(),
                            new List<DirectoryBuildPropsInfo>()));

                    continue;
                }

                var projectsInfo = TryGetProjectsInfo(solutionFile, solutionInfo);
                var directoryBuildPropsInfos = _readDirectoryBuildProps
                    ? TryGetDirectoryBuildPropsInfo(new FileInfo(solutionFile).Directory)
                    : new List<DirectoryBuildPropsInfo>();
                ApplyInheritedPackages(projectsInfo, directoryBuildPropsInfos);
                solutionInfos.Add(new SolutionInfo(solutionFile, solutionInfo, projectsInfo, directoryBuildPropsInfos));
            }

            return solutionInfos;
        }

        /// <remarks>
        /// <para>
        /// NOTE: This does not support chained Directory.Build.props (Import directive).
        /// </para>
        /// <para>
        /// The props versions are appended as they were declared. A project's own <c>Update</c>/<c>Remove</c>
        /// is applied later, by <see cref="PackagesAnalyzer.GetEffectivePackages"/>, so that the version this
        /// file put there is still available to the <c>-o</c> report.
        /// </para>
        /// </remarks>
        private static void ApplyInheritedPackages(
            ICollection<ProjectInfo> projectsInfo,
            ICollection<DirectoryBuildPropsInfo> directoryBuildPropsInfos)
        {
            if (!projectsInfo.Any())
            {
                return;
            }

            if (!directoryBuildPropsInfos.Any())
            {
                return;
            }

            // Props directories come from a directory walk and are always absolute, while project directories
            // follow the solution path given on the command line. Both sides are resolved before matching, so
            // that a relative `-s` doesn't silently leave every project inheriting nothing.
            var candidates = directoryBuildPropsInfos
                .Where(dbp => !string.IsNullOrEmpty(dbp.DirectoryName))
                .Select(dbp => new { Props = dbp, Directory = PathUtils.ResolveDirectory(dbp.DirectoryName) })
                .OrderByDescending(dbp => dbp.Directory.Length)
                .ToList();

            foreach (var projectInfo in projectsInfo)
            {
                var projectDirectory = PathUtils.ResolveDirectory(projectInfo.ProjectDirectory);
                var directoryBuildProps = candidates
                    .FirstOrDefault(dbp => PathUtils.IsSameOrUnderDirectory(projectDirectory, dbp.Directory))
                    ?.Props;

                if (directoryBuildProps != null)
                {
                    projectInfo.DirectoryBuildPropsFile =
                        Path.Combine(directoryBuildProps.DirectoryName, directoryBuildProps.FileName);

                    foreach (var packageReference in directoryBuildProps.Packages)
                    {
                        projectInfo.Packages.Add(
                            new NuGetPackageInfo(
                                packageReference.Id,
                                packageReference.Version,
                                NuGetPackageReferenceType.Inherited));
                    }
                }
            }
        }

        private (bool isSuccessParsing, SolutionModel? solution) TryGetSolutionInfo(string filePath)
        {
            SolutionModel solution;
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

                var serializer = SolutionSerializers.GetSerializerByMoniker(filePath);
                if (serializer == null)
                {
                    _logger.Message($"Unsupported solution file format {filePath}");

                    return (false, null);
                }

                solution = serializer.OpenAsync(filePath, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception e)
            {
                _logger.Message($"Unable to get solution info for {filePath}\r\n {e}");

                return (false, null);
            }

            return (true, solution);
        }

        // ReSharper disable once CognitiveComplexity
        private List<ProjectInfo> TryGetProjectsInfo(string filePath, SolutionModel solution)
        {
            var projectInfos = new List<ProjectInfo>();

            var solutionDirectory = Path.GetDirectoryName(PathUtils.EnsureSystemSeparator(filePath));
            if (solutionDirectory == null)
            {
                _logger.Message($"Solution directory wasn't found for file {filePath}");

                return projectInfos;
            }

            foreach (var project in solution.SolutionProjects)
            {
                try
                {
                    // Solution files store project paths with '\' (.sln) or '/' (.slnx),
                    // so we must convert to system path separator to work on posix systems.
                    var projectFilePath =
                        PathUtils.EnsureSystemSeparator(Path.Combine(solutionDirectory, project.FilePath));
                    var projectDirectory = Path.GetDirectoryName(projectFilePath);
                    if (projectDirectory == null)
                    {
                        _logger.Message($"Project directory wasn't found for project {project.ActualDisplayName}");

                        return projectInfos;
                    }

                    var packageConfigPath =
                        PathUtils.EnsureSystemSeparator(Path.Combine(projectDirectory, "packages.config"));
                    if (File.Exists(packageConfigPath))
                    {
                        var packages = _projectParser.ParsePackageConfig(packageConfigPath);
                        projectInfos.Add(
                            new ProjectInfo(project.ActualDisplayName, projectDirectory, packages)
                            {
                                ProjectFile = projectFilePath
                            });
                    }
                    else if (File.Exists(projectFilePath))
                    {
                        var evaluation = _projectParser.ParseProjectFile(projectFilePath);
                        projectInfos.Add(
                            new ProjectInfo(
                                project.ActualDisplayName,
                                projectDirectory,
                                evaluation.Packages,
                                evaluation.PackageUpdates,
                                evaluation.RemovedPackageIds)
                            {
                                ProjectFile = projectFilePath
                            });
                    }
                    else
                    {
                        projectInfos.Add(
                            new ProjectInfo(project.ActualDisplayName, projectDirectory, new List<NuGetPackageInfo>())
                            {
                                ProjectFile = projectFilePath
                            });
                        _logger.Message($"Unable to find package.config file for project {project.FilePath}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Message($"Unable to get project info for {project.FilePath}\r\n {e}");
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
                    // Only the packages: an `Update` or a `Remove` left over by the props file itself is
                    // dropped on purpose. It would have had to match an item declared before it, and a props
                    // file is imported ahead of everything — including the project files that inherit from it,
                    // which it therefore cannot reach.
                    var packages = _projectParser.ParseProjectFile(fileInfo.FullName)
                        .Packages;

                    directoryBuildPropsInfo.Add(
                        new DirectoryBuildPropsInfo(
                            fileInfo.Name,
                            fileInfo.Directory?.FullName ?? string.Empty,
                            packages));
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
