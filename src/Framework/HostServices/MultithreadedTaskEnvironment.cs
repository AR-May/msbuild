// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework.PathHelpers;

namespace Microsoft.Build.Framework.HostServices
{
    /// <summary>
    /// Implementation of TaskExecutionContext that virtualizes environment variables and current directory
    /// for use in thread nodes where tasks may be executed in parallel.
    /// </summary>
    internal class MultithreadedTaskEnvironment : TaskEnvironment
    {
        private readonly Dictionary<string, string> _environmentVariables;
        private AbsolutePath _currentDirectory;

        /// <summary>
        /// Initializes a new instance of the VirtualizedTaskExecutionContext class
        /// with the specified working directory and optional environment variables.
        /// </summary>
        /// <param name="currentDirectory">The initial working directory.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to use. 
        /// If not provided, the current environment variables are used.</param>
        public MultithreadedTaskEnvironment(
            string currentDirectory,
            Dictionary<string, string> environmentVariables)
            : base()
        {
            _environmentVariables = environmentVariables;
            _currentDirectory = new AbsolutePath(currentDirectory);
        }

        public override AbsolutePath ProjectCurrentDirectory
        {
            get => _currentDirectory;
            internal set => _currentDirectory = value;
        }

        /// <summary>
        /// Converts a relative or absolute path to an absolute path.
        /// This function resolves paths relative to ProjectDirectory.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>An absolute path.</returns>
        public override AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(path, ProjectCurrentDirectory);
        }

        /// <summary>
        /// Gets the value of a virtualized environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, or null if it doesn't exist.</returns>
        public override string? GetEnvironmentVariable(string name)
        {
            return _environmentVariables.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// Gets all virtualized environment variables.
        /// </summary>
        /// <returns>A read-only dictionary of environment variables.</returns>
        public override IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _environmentVariables;
        }

        /// <summary>
        /// Sets a virtualized environment variable.
        /// This does not affect the actual process's environment variables.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value to set, or null to remove the environment variable.</param>
        public override void SetEnvironmentVariable(string name, string? value)
        {
            if (value == null)
            {
                _environmentVariables.Remove(name);
            }
            else
            {
                _environmentVariables[name] = value;
            }
        }

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// This mirrors the behavior of CommunicationsUtilities.SetEnvironment but operates on this TaskEnvironment.
        /// </summary>
        /// <param name="newEnvironment">The new environment variables to set.</param>
        internal override void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            // Simply replace the entire environment dictionary
            _environmentVariables.Clear();
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                _environmentVariables[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Creates a ProcessStartInfo configured for the virtualized environment.
        /// </summary>
        /// <returns>A ProcessStartInfo object with environment variables set according to this context.</returns>
        public override ProcessStartInfo GetProcessStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = ProjectCurrentDirectory.Path
            };

            // Set environment variables
            foreach (var kvp in _environmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            return startInfo;
        }
    }
}
