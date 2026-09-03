using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// A minimal stand-in for MSBuild's property evaluation: it stores property values, keeps track of the
    /// ones that must not be overwritten (global and reserved properties), and expands <c>$(Name)</c> references.
    /// </summary>
    public class MSBuildProperties
    {
        public const string TargetFrameworkPropertyName = "TargetFramework";

        public const string TargetFrameworksPropertyName = "TargetFrameworks";

        private const string PropertyReferenceStart = "$(";

        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _readOnlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly IReadOnlyDictionary<string, string>? _globalProperties;

        private readonly string? _projectFilePath;

        public MSBuildProperties(IReadOnlyDictionary<string, string>? globalProperties, string? projectFilePath)
        {
            _globalProperties = globalProperties;

            if (!string.IsNullOrWhiteSpace(projectFilePath))
            {
                _projectFilePath = Path.GetFullPath(PathUtils.EnsureSystemSeparator(projectFilePath));
                ProjectDirectory = Path.GetDirectoryName(_projectFilePath);
            }

            // The SDK would supply these through Microsoft.Common.props. We don't follow imports, so we seed them
            // as ordinary properties, which keeps the usual `<Configuration Condition=" '$(Configuration)' == '' ">`
            // pattern a no-op instead of a surprise.
            _values["Configuration"] = "Debug";
            _values["Platform"] = "AnyCPU";

            SetReservedProperties();
            SetGlobalProperties();
        }

        public string? ProjectDirectory { get; }

        public string GetValue(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Assigns a property value, unless the name belongs to a global or reserved property. MSBuild gives those
        /// precedence over anything a project file declares, so writes to them are silently ignored.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The already expanded property value.</param>
        public void SetValue(string name, string value)
        {
            if (_readOnlyNames.Contains(name))
            {
                return;
            }

            _values[name] = value;
        }

        /// <summary>
        /// Expands <c>$(Name)</c> references, replacing unknown properties with an empty string the way MSBuild does.
        /// Constructs we don't understand (property functions, item and metadata references) are left untouched and
        /// reported through <paramref name="hasUnsupportedSyntax"/>.
        /// </summary>
        /// <param name="value">The text to expand.</param>
        /// <param name="hasUnsupportedSyntax">Whether the text contains a construct outside the supported subset.</param>
        /// <returns>The expanded text.</returns>
        public string Expand(string value, out bool hasUnsupportedSyntax)
        {
            return ExpandCore(value, out _, out hasUnsupportedSyntax);
        }

        /// <summary>
        /// Expands <c>$(Name)</c> references, returning <see langword="false"/> when any of them could not be
        /// resolved. Callers that would rather keep the literal text than substitute an empty string use this.
        /// </summary>
        /// <param name="value">The text to expand.</param>
        /// <param name="expanded">The best-effort expansion of <paramref name="value"/>.</param>
        /// <returns><see langword="false"/> when a property reference could not be resolved.</returns>
        public bool TryExpand(string value, out string expanded)
        {
            expanded = ExpandCore(value, out var allResolved, out _);

            return allResolved;
        }

        /// <summary>
        /// Creates a fresh set of properties for one target framework of a multi-targeting project. Only the global
        /// and reserved properties carry over — the project's own property groups have to be re-evaluated, because
        /// they may be conditioned on the target framework.
        /// </summary>
        /// <param name="targetFramework">One entry of the project's <c>TargetFrameworks</c>.</param>
        /// <returns>Properties with <c>TargetFramework</c> pinned to <paramref name="targetFramework"/>.</returns>
        public MSBuildProperties ForTargetFramework(string targetFramework)
        {
            var properties = new MSBuildProperties(_globalProperties, _projectFilePath);
            if (!properties._readOnlyNames.Contains(TargetFrameworkPropertyName))
            {
                properties._values[TargetFrameworkPropertyName] = targetFramework;
                properties._readOnlyNames.Add(TargetFrameworkPropertyName);
            }

            return properties;
        }

        private static int FindClosingParenthesis(string value, int startIndex)
        {
            var depth = 1;
            for (var i = startIndex; i < value.Length; i++)
            {
                switch (value[i])
                {
                    case '(':
                        depth++;

                        break;
                    case ')':
                        depth--;
                        if (depth == 0)
                        {
                            return i;
                        }

                        break;
                }
            }

            return -1;
        }

        private static bool IsSimplePropertyName(string name)
        {
            if (name.Length == 0)
            {
                return false;
            }

            if (!char.IsLetter(name[0]) && name[0] != '_')
            {
                return false;
            }

            return name.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-');
        }

        private void SetReservedProperties()
        {
            SetReadOnlyValue("OS", OperatingSystem.IsWindows() ? "Windows_NT" : "Unix");

            if (_projectFilePath == null || ProjectDirectory == null)
            {
                return;
            }

            SetReadOnlyValue("MSBuildProjectFullPath", _projectFilePath);
            SetReadOnlyValue("MSBuildProjectDirectory", ProjectDirectory);
            SetReadOnlyValue("MSBuildProjectFile", Path.GetFileName(_projectFilePath));
            SetReadOnlyValue("MSBuildProjectName", Path.GetFileNameWithoutExtension(_projectFilePath));
            SetReadOnlyValue("MSBuildProjectExtension", Path.GetExtension(_projectFilePath));

            SetReadOnlyValue("MSBuildThisFileFullPath", _projectFilePath);
            SetReadOnlyValue("MSBuildThisFile", Path.GetFileName(_projectFilePath));
            SetReadOnlyValue("MSBuildThisFileName", Path.GetFileNameWithoutExtension(_projectFilePath));
            SetReadOnlyValue("MSBuildThisFileExtension", Path.GetExtension(_projectFilePath));

            // MSBuild's *Directory properties carry a trailing separator, unlike MSBuildProjectDirectory.
            SetReadOnlyValue("MSBuildThisFileDirectory", ProjectDirectory + Path.DirectorySeparatorChar);
        }

        private void SetGlobalProperties()
        {
            if (_globalProperties == null)
            {
                return;
            }

            foreach (var globalProperty in _globalProperties)
            {
                SetReadOnlyValue(globalProperty.Key, globalProperty.Value);
            }
        }

        private void SetReadOnlyValue(string name, string value)
        {
            _values[name] = value;
            _readOnlyNames.Add(name);
        }

        private string ExpandCore(string value, out bool allResolved, out bool hasUnsupportedSyntax)
        {
            allResolved = true;
            hasUnsupportedSyntax = false;

            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (value.Contains("@(", StringComparison.Ordinal) || value.Contains("%(", StringComparison.Ordinal))
            {
                allResolved = false;
                hasUnsupportedSyntax = true;
            }

            if (!value.Contains(PropertyReferenceStart, StringComparison.Ordinal))
            {
                return value;
            }

            var result = new StringBuilder(value.Length);
            var index = 0;
            while (index < value.Length)
            {
                var start = value.IndexOf(PropertyReferenceStart, index, StringComparison.Ordinal);
                if (start < 0)
                {
                    result.Append(value, index, value.Length - index);

                    break;
                }

                result.Append(value, index, start - index);

                var end = FindClosingParenthesis(value, start + PropertyReferenceStart.Length);
                if (end < 0)
                {
                    // Unbalanced parentheses; leave the remainder as it is rather than guessing.
                    allResolved = false;
                    hasUnsupportedSyntax = true;
                    result.Append(value, start, value.Length - start);

                    break;
                }

                var name = value.Substring(
                    start + PropertyReferenceStart.Length,
                    end - start - PropertyReferenceStart.Length);
                if (!IsSimplePropertyName(name))
                {
                    // A property function, a registry lookup or something else outside the supported subset.
                    allResolved = false;
                    hasUnsupportedSyntax = true;
                    result.Append(value, start, end - start + 1);
                }
                else if (_values.TryGetValue(name, out var propertyValue))
                {
                    result.Append(propertyValue);
                }
                else
                {
                    allResolved = false;
                }

                index = end + 1;
            }

            return result.ToString();
        }
    }
}
