// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This helper base class provides default functionality for thread-safe tasks.
    /// Tasks that inherit from this class are considered thread-safe and can be executed in parallel.
    /// </summary>
    public abstract class MultiThreadableTask : Task, IMultiThreadableTask
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected MultiThreadableTask()
            : base()
        {
        }

        /// <summary>
        /// This constructor allows derived task classes to register their resources.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        protected MultiThreadableTask(ResourceManager taskResources)
            : base(taskResources)
        {
        }

        /// <summary>
        /// This constructor allows derived task classes to register their resources, as well as provide a prefix for
        /// composing help keywords from string resource names.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix.</param>
        protected MultiThreadableTask(ResourceManager taskResources, string helpKeywordPrefix)
            : base(taskResources, helpKeywordPrefix)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the task environment that provides access to task execution environment.
        /// This property must be set by the MSBuild infrastructure before task execution.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        #endregion
    }
}
