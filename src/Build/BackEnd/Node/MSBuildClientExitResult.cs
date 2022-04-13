﻿namespace Microsoft.Build.Experimental.Client
{
    /// <summary>
    /// Enumeration of the various ways in which the MSBuildClient execution can exit.
    /// </summary>
    public class MSBuildClientExitResult
    {
        /// <summary>
        /// The MSBuild client exit type.
        /// Covers different ways MSBuild client execution can finish.
        /// Build errors are not included. The client could finish successfully and the build at the same time could result in a build error.
        /// </summary>
        public MSBuildClientExitType MSBuildClientExitType { get; set; }

        /// <summary>
        /// The build exit type. Possible values: MSBuildApp.ExitType serialized into a string.
        /// This field is null if MSBuild client execution was not successful.
        /// </summary>
        public string? MSBuildAppExitTypeString { get; set; }

        public MSBuildClientExitResult() { }
    }
}
