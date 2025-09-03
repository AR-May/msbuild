﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Read a list of items from a file.
    /// </summary>
    public class ReadLinesFromFile : TaskExtension, IConcurrentTask
    {
        /// <summary>
        /// Execution context used when task is supposed to run concurrently in multiple threads.
        /// If null hosting process do not run this task concurrently and set it execution context on process level.
        /// </summary>
        private TaskExecutionContext _executionContext;

        /// <summary>
        /// File to read lines from.
        /// </summary>
        [Required]
        public ITaskItem File { get; set; }

        /// <summary>
        /// Receives lines from file.
        /// </summary>
        [Output]
        public ITaskItem[] Lines { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool success = true;
            if (File != null)
            {
                string filePath = _executionContext.GetFullPath(File.ItemSpec);
                if (FileSystems.Default.FileExists(filePath))
                {
                    try
                    {
                        string[] textLines = System.IO.File.ReadAllLines(filePath);

                        var nonEmptyLines = new List<ITaskItem>();
                        char[] charsToTrim = { '\0', ' ', '\t' };

                        foreach (string textLine in textLines)
                        {
                            // A customer has given us a project with a FileList.txt file containing
                            // a line full of '\0' characters.  We don't know how these characters
                            // got in there, but when we try to read the file back in, we fail
                            // miserably.  Here, we Trim to protect us from this situation.
                            string trimmedTextLine = textLine.Trim(charsToTrim);
                            if (trimmedTextLine.Length > 0)
                            {
                                // The lines were written to the file in unescaped form, so we need to escape them
                                // before passing them to the TaskItem.
                                nonEmptyLines.Add(new TaskItem(EscapingUtilities.Escape(trimmedTextLine)));
                            }
                        }

                        Lines = nonEmptyLines.ToArray();
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        Log.LogErrorWithCodeFromResources("ReadLinesFromFile.ErrorOrWarning", File.ItemSpec, e.Message);
                        success = false;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Configure the task for concurrent execution
        /// </summary>
        void IConcurrentTask.ConfigureForConcurrentExecution(TaskExecutionContext executionContext)
        {
            _executionContext = executionContext;
        }
    }
}
