// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework.PathHelpers
{
    /// <summary>
    /// Represents a relative file system path.
    /// </summary>
    /// <remarks>
    /// This struct ensures that paths are always in relative form and properly formatted.
    /// </remarks>
    internal readonly struct RelativePath
    {
        /// <summary>
        /// Gets the string representation of this path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Creates a new instance of RelativePath.
        /// </summary>
        /// <param name="path">The relative path string.</param>
        /// <exception cref="ArgumentException">Thrown when the path is null, empty, or is an absolute path.</exception>
        public RelativePath(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Converts this relative path to an absolute path using an optional base path.
        /// </summary>
        /// <param name="basePath">The base path to resolve against. If null, the current directory is used.</param>
        /// <returns>An absolute path.</returns>
        public AbsolutePath ToAbsolutePath(AbsolutePath basePath)
        {
            return new AbsolutePath(Path, basePath);
        }

        /// <summary>
        /// Implicitly converts a RelativePath to a string.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        public static implicit operator string(RelativePath path) => path.Path;
    }
}
