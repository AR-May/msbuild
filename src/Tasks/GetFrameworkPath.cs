// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the paths to the various frameworks versions.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class GetFrameworkPath : TaskExtension
    {
        #region ITask Members

        /// <summary>
        /// Does nothing: getters do all the work
        /// </summary>
        public override bool Execute()
        {
            return true;
        }

        #endregion

        #region Properties

        // PERF NOTE: We cache these values in statics -- although the code we call does this too,
        // it still seems to give an advantage perhaps because there is one less string copy.
        // In a large build, this adds up.
        // PERF NOTE: We also only find paths we are actually asked for (via <Output> tags)
        
#pragma warning disable MSB0011 // These static fields are safe because they're only used for caching ToolLocationHelper results
        private static readonly Lazy<string> s_path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Latest) ?? string.Empty);
        private static readonly Lazy<string> s_version11Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11) ?? string.Empty);
        private static readonly Lazy<string> s_version20Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) ?? string.Empty);
        private static readonly Lazy<string> s_version30Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30) ?? string.Empty);
        private static readonly Lazy<string> s_version35Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35) ?? string.Empty);
        private static readonly Lazy<string> s_version40Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40) ?? string.Empty);
        private static readonly Lazy<string> s_version45Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45) ?? string.Empty);
        private static readonly Lazy<string> s_version451Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version451) ?? string.Empty);
        private static readonly Lazy<string> s_version452Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version452) ?? string.Empty);
        private static readonly Lazy<string> s_version46Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version46) ?? string.Empty);
        private static readonly Lazy<string> s_version461Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version461) ?? string.Empty);
        private static readonly Lazy<string> s_version462Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version462) ?? string.Empty);
        private static readonly Lazy<string> s_version47Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version47) ?? string.Empty);
        private static readonly Lazy<string> s_version471Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version471) ?? string.Empty);
        private static readonly Lazy<string> s_version472Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version472) ?? string.Empty);
        private static readonly Lazy<string> s_version48Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version48) ?? string.Empty);
#pragma warning restore MSB0011

        /// <summary>
        /// Path to the latest framework, whatever version it happens to be
        /// </summary>
        [Output]
        public string Path
        {
            get
            {
                return s_path.Value;
            }
        }

        /// <summary>
        /// Path to the v1.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion11Path
        {
            get
            {
                return s_version11Path.Value;
            }
        }

        /// <summary>
        /// Path to the v2.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion20Path
        {
            get
            {
                return s_version20Path.Value;
            }
        }

        /// <summary>
        /// Path to the v3.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion30Path
        {
            get
            {
                return s_version30Path.Value;
            }
        }

        /// <summary>
        /// Path to the v3.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion35Path
        {
            get
            {
                return s_version35Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion40Path
        {
            get
            {
                return s_version40Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion45Path
        {
            get
            {
                return s_version45Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.5.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion451Path
        {
            get
            {
                return s_version451Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.5.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion452Path
        {
            get
            {
                return s_version452Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.6 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion46Path
        {
            get
            {
                return s_version46Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.6.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion461Path
        {
            get
            {
                return s_version461Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.6.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion462Path
        {
            get
            {
                return s_version462Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.7 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion47Path
        {
            get
            {
                return s_version47Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.7.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion471Path
        {
            get
            {
                return s_version471Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.7.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion472Path
        {
            get
            {
                return s_version472Path.Value;
            }
        }

        /// <summary>
        /// Path to the v4.8 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion48Path
        {
            get
            {
                return s_version48Path.Value;
            }
        }

        #endregion
    }
}
