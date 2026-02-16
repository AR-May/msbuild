// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides protection for critical system environment variables that must remain 
    /// consistent to ensure thread safety and proper functioning of MSBuild components.
    /// </summary>
    internal static class ProtectedEnvironmentVariables
    {
        /// <summary>
        /// The string comparer to use for environment variable name comparisons, based on OS file system case sensitivity.
        /// </summary>
        private static readonly StringComparer s_environmentVariableComparer = NativeMethods.IsFileSystemCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Set of specific environment variable names that are protected from modification.
        /// </summary>
        private static readonly Lazy<FrozenSet<string>> s_protectedVariables = new Lazy<FrozenSet<string>>(() =>
            FrozenSet.ToFrozenSet(new[]
            {
                // .NET Framework path resolution environment variables used by ToolLocationHelper
                "COMPLUS_INSTALLROOT",
                "COMPLUS_VERSION",
                
                // Reference assembly root path override used by ToolLocationHelper
                "ReferenceAssemblyRoot",
                
                // 64-bit program files directory used by ToolLocationHelper
                "ProgramW6432"
            }, s_environmentVariableComparer));

        /// <summary>
        /// Validates that the specified environment variable is not protected from modification.
        /// Certain system environment variables must remain consistent across threads and
        /// processes to ensure proper MSBuild functionality.
        /// </summary>
        /// <param name="name">The name of the environment variable to check.</param>
        /// <exception cref="ArgumentException">Thrown when attempting to modify a protected environment variable.</exception>
        internal static void ValidateEnvironmentVariableIsNotProtected(string name)
        {
            // Fast path: check if it's a protected variable
            if (IsProtectedVariable(name))
            {
                throw new ArgumentException(
                    $"Task cannot modify protected environment variable '{name}'.",
                    nameof(name));
            }
        }

        /// <summary>
        /// Checks if the specified environment variable name is protected from modification.
        /// </summary>
        /// <param name="name">The environment variable name to check.</param>
        /// <returns>True if the variable is protected, false otherwise.</returns>
        internal static bool IsProtectedVariable(string name)
        {
            // Fast HashSet lookup for specific protected variables
            if (s_protectedVariables.Value.Contains(name))
            {
                return true;
            }

            // Fast prefix check for MSBUILD variables
            return name.StartsWith("MSBUILD", NativeMethods.IsFileSystemCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }
    }
}
