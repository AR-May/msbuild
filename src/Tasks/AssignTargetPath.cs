// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Create a new list of items that have &lt;TargetPath&gt; attributes if none was present in
    /// the input.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class AssignTargetPath : TaskExtension, IMultiThreadableTask
    {
        #region Properties

        /// <summary>
        /// Gets or sets the task execution environment for thread-safe path resolution.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; }

        /// <summary>
        /// The folder to make the links relative to.
        /// </summary>
        [Required]
        public string RootFolder { get; set; }

        /// <summary>
        /// The incoming list of files.
        /// </summary>
        public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The resulting list of files.
        /// </summary>
        /// <value></value>
        [Output]
        public ITaskItem[] AssignedFiles { get; private set; }

        #endregion

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            AssignedFiles = new ITaskItem[Files.Length];

            if (Files.Length > 0)
            {
                // Compose a file in the root folder.
                // NOTE: RootFolder may or may not have a trailing slash.
                // Ensure trailing slash otherwise c:\bin appears to match part of c:\bin2\foo
                AbsolutePath fullRootPath = TaskEnvironment.GetAbsolutePath(FileUtilities.EnsureTrailingSlash(RootFolder));

                string currentDirectory = TaskEnvironment.ProjectDirectory.Value;

                // check if the root folder is the same as the current directory
                // NOTE: the path returned from Directory.GetCurrentDirectory()
                // does not have a trailing slash, but fullRootPath does
                bool isRootFolderSameAsCurrentDirectory =
                    ((fullRootPath.Value.Length - 1 /* exclude trailing slash */) == currentDirectory.Length)
                &&
                    (String.Compare(
                        fullRootPath, 0,
                        currentDirectory, 0,
                        fullRootPath.Value.Length - 1 /* don't compare trailing slash */,
                        StringComparison.OrdinalIgnoreCase) == 0);

                for (int i = 0; i < Files.Length; ++i)
                {
                    AssignedFiles[i] = new TaskItem(Files[i]);

                    // If TargetPath is already set, it takes priority.
                    // https://github.com/dotnet/msbuild/issues/2795
                    string targetPath = Files[i].GetMetadata(ItemMetadataNames.targetPath);

                    // If TargetPath not already set, fall back to default behavior.
                    if (string.IsNullOrEmpty(targetPath))
                    {
                        targetPath = Files[i].GetMetadata(ItemMetadataNames.link);
                    }

                    if (string.IsNullOrEmpty(targetPath))
                    {
                        if (// if the file path is relative
                            !Path.IsPathRooted(Files[i].ItemSpec) &&
                            // if the file path doesn't contain any relative specifiers
                            !Files[i].ItemSpec.Contains("." + Path.DirectorySeparatorChar) &&
                            // if the file path is already relative to the root folder
                            isRootFolderSameAsCurrentDirectory)
                        {
                            // then just use the file path as-is
                            // PERF NOTE: we do this to avoid calling Path.GetFullPath() below,
                            // because that method consumes a lot of memory, esp. when we have
                            // a lot of items coming through this task
                            targetPath = Files[i].ItemSpec;
                        }
                        else
                        {
                            // PERF WARNING: GetAbsolutePath is expensive in terms of memory;
                            // we should avoid calling it whenever possible
                            string itemSpecFullFileNamePath = TaskEnvironment.GetAbsolutePath(Files[i].ItemSpec).Value;

                            if (String.Compare(fullRootPath, 0, itemSpecFullFileNamePath, 0, fullRootPath.Value.Length, StringComparison.CurrentCultureIgnoreCase) == 0)
                            {
                                // The item spec file is in the "cone" of the RootFolder. Return the relative path from the cone root.
                                targetPath = itemSpecFullFileNamePath.Substring(fullRootPath.Value.Length);
                            }
                            else
                            {
                                // The item spec file is not in the "cone" of the RootFolder. Return the filename only.
                                targetPath = Path.GetFileName(Files[i].ItemSpec);
                            }
                        }
                    }

                    AssignedFiles[i].SetMetadata(ItemMetadataNames.targetPath, EscapingUtilities.Escape(targetPath));
                }
            }

            return true;
        }
    }
}
