// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework.PathHelpers;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides task execution environment including environment variables,
    /// file paths, and process management capabilities to multi-threadable tasks.
    /// </summary>
    public class TaskEnvironment
    {
        /// <summary>
        /// Initializes a new instance of the TaskEnvironment class.
        /// </summary>
        public TaskEnvironment() { }

        /// <summary>
        /// Gets or sets the project directory for the task execution.
        /// </summary>
        public virtual AbsolutePath ProjectCurrentDirectory
        {
            get => throw new NotImplementedException();
            internal set => throw new NotImplementedException();
        }

        /// <summary>
        /// Converts a relative or absolute path string to an absolute path.
        /// This function resolves paths relative to ProjectDirectory.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>An absolute path representation.</returns>
        public virtual AbsolutePath GetAbsolutePath(string path) => throw new NotImplementedException();

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, or null if it does not exist.</returns>
        public virtual string? GetEnvironmentVariable(string name) => throw new NotImplementedException();

        /// <summary>
        /// Gets a dictionary containing all environment variables.
        /// </summary>
        /// <returns>A read-only dictionary of environment variables.</returns>
        public virtual IReadOnlyDictionary<string, string> GetEnvironmentVariables() => throw new NotImplementedException();

        /// <summary>
        /// Sets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value to set, or null to remove the environment variable.</param>
        public virtual void SetEnvironmentVariable(string name, string? value) => throw new NotImplementedException();

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// This mirrors the behavior of CommunicationsUtilities.SetEnvironment but operates on this TaskEnvironment.
        /// </summary>
        /// <param name="newEnvironment">The new environment variables to set.</param>
        internal virtual void SetEnvironment(IDictionary<string, string> newEnvironment) => throw new NotImplementedException();

        /// <summary>
        /// Creates a new ProcessStartInfo configured for the current task execution environment.
        /// </summary>
        /// <returns>A ProcessStartInfo object configured for the current task execution environment.</returns>
        public virtual ProcessStartInfo GetProcessStartInfo() => throw new NotImplementedException();
    }
}
