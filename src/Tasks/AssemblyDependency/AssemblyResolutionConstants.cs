// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Constants used for assembly resolution.
    /// </summary>
    internal static class AssemblyResolutionConstants
    {
        /// <summary>
        /// Special hintpath indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string hintPathSentinel = "{hintpathfromitem}";

        /// <summary>
        /// Special AssemblyFolders indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersSentinel = "{assemblyfolders}";

        /// <summary>
        /// Special CandidateAssemblyFiles indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string candidateAssemblyFilesSentinel = "{candidateassemblyfiles}";

        /// <summary>
        /// Special GAC indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string gacSentinel = "{gac}";

        /// <summary>
        /// Special Framework directory indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string frameworkPathSentinel = "{targetframeworkdirectory}";

        /// <summary>
        /// Special SearchPath indicator that means: match against the assembly item's Include as
        /// if it were a file.
        /// </summary>
        public const string rawFileNameSentinel = "{rawfilename}";

        /// <summary>
        /// Special AssemblyFoldersEx indicator.  May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersExSentinel = "{registry:";

        /// <summary>
        /// Special AssemblyFoldersFromConfig indicator.  May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersFromConfigSentinel = "{assemblyfoldersfromconfig:";

        public static bool IsAssemblyResolutionConstant(string searchPath)
        {
            if (String.Equals(searchPath, AssemblyResolutionConstants.hintPathSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Equals(searchPath, AssemblyResolutionConstants.frameworkPathSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Equals(searchPath, AssemblyResolutionConstants.rawFileNameSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Equals(searchPath, AssemblyResolutionConstants.candidateAssemblyFilesSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            // TODO: not sure about that "#if FEATURE_GAC", do we need it?
#if FEATURE_GAC
            else if (String.Equals(searchPath, AssemblyResolutionConstants.gacSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
#endif
            else if (String.Equals(searchPath, AssemblyResolutionConstants.assemblyFoldersSentinel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
#if FEATURE_WIN32_REGISTRY
            // Check for AssemblyFoldersEx sentinel.
            else if (0 == String.Compare(searchPath, 0, AssemblyResolutionConstants.assemblyFoldersExSentinel, 0, AssemblyResolutionConstants.assemblyFoldersExSentinel.Length, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
#endif
            else if (0 == String.Compare(searchPath, 0, AssemblyResolutionConstants.assemblyFoldersFromConfigSentinel, 0, AssemblyResolutionConstants.assemblyFoldersFromConfigSentinel.Length, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }
}
