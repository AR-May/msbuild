// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework.PathHelpers;

namespace Microsoft.Build.Framework.HostServices
{
    /// <summary>
    /// Default implementation of TaskExecutionContext that directly interacts with the file system
    /// and environment variables. Implemented as a singleton since it has no instance state.
    /// </summary>
    public class StubTaskEnvironment : TaskEnvironment
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        private static readonly StubTaskEnvironment s_instance = new StubTaskEnvironment();

        /// <summary>
        /// Gets the singleton instance of StubTaskExecutionContext.
        /// </summary>
        public static StubTaskEnvironment Instance => s_instance;

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private StubTaskEnvironment() { }

        /// <summary>
        /// Gets or sets the project directory.
        /// </summary>
        public override AbsolutePath ProjectCurrentDirectory 
        { 
            get => new AbsolutePath(Directory.GetCurrentDirectory(), ignoreRootedCheck: true);
            internal set => Directory.SetCurrentDirectory(value.Path);
        }

        /// <summary>
        /// Converts a relative or absolute path to an absolute path.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>An absolute path.</returns>
        public override AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(Path.GetFullPath(path), ignoreRootedCheck: true);
        }

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, or null if it doesn't exist.</returns>
        public override string? GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        /// <summary>
        /// Gets all environment variables.
        /// </summary>
        /// <returns>A read-only dictionary of environment variables.</returns>
        public override IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            var variables = Environment.GetEnvironmentVariables();
            var result = new Dictionary<string, string>(variables.Count, StringComparer.OrdinalIgnoreCase);

            foreach (string key in variables.Keys)
            {
                if (variables[key] is string value)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Sets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value to set, or null to remove the environment variable.</param>
        public override void SetEnvironmentVariable(string name, string? value)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// This mirrors the behavior of CommunicationsUtilities.SetEnvironment but operates on this TaskEnvironment.
        /// </summary>
        /// <param name="newEnvironment">The new environment variables to set.</param>
        internal override void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            // First, delete all no longer set variables
            IReadOnlyDictionary<string, string> currentEnvironment = GetEnvironmentVariables();
            foreach (KeyValuePair<string, string> entry in currentEnvironment)
            {
                if (!newEnvironment.ContainsKey(entry.Key))
                {
                    SetEnvironmentVariable(entry.Key, null);
                }
            }

            // Then, make sure the new ones have their new values.
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                if (!currentEnvironment.TryGetValue(entry.Key, out string? currentValue) || currentValue != entry.Value)
                {
                    SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Creates a ProcessStartInfo configured for the current environment.
        /// </summary>
        /// <returns>A ProcessStartInfo object.</returns>
        public override ProcessStartInfo GetProcessStartInfo()
        {
            return new ProcessStartInfo();
        }
    }
}
