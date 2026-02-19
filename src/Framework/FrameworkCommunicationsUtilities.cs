// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Utilities for environment variable operations in the Framework project.
    /// Contains environment variable methods needed by MultiProcessTaskEnvironmentDriver.
    /// </summary>
    internal static class FrameworkCommunicationsUtilities
    {
        /// <summary>
        /// Case-insensitive string comparer for environment variable names.
        /// </summary>
        internal static StringComparer EnvironmentVariableComparer => StringComparer.OrdinalIgnoreCase;
        /// <summary>
        /// Sets an environment variable using <see cref="Environment.SetEnvironmentVariable(string,string)" />.
        /// </summary>
        internal static void SetEnvironmentVariable(string name, string value)
            => Environment.SetEnvironmentVariable(name, value);

        /// <summary>
        /// Returns key value pairs of environment variables in a dictionary
        /// with a case-insensitive key comparer.
        /// </summary>
        internal static Dictionary<string, string> GetEnvironmentVariables()
        {
            IDictionary vars = Environment.GetEnvironmentVariables();
            var table = new Dictionary<string, string>(vars.Count, StringComparer.OrdinalIgnoreCase);
            
            foreach (DictionaryEntry entry in vars)
            {
                string key = (string)entry.Key;
                string value = (string)entry.Value;
                table[key] = value;
            }
            
            return table;
        }

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (newEnvironment != null)
            {
                // First, delete all no longer set variables
                IDictionary<string, string> currentEnvironment = GetEnvironmentVariables();
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
                    if (!currentEnvironment.TryGetValue(entry.Key, out string currentValue) || currentValue != entry.Value)
                    {
                        SetEnvironmentVariable(entry.Key, entry.Value);
                    }
                }
            }
        }
    }
}