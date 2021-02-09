// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceRequest : INodePacket
    {
        private string[] _allowedAssemblyExtensions;

        private string[] _allowedRelatedFileExtensions;

        private string _appConfigFile;

        // ITaskItem[] 
        private TaskParameter _assemblies;

        // ITaskItem[]
        private TaskParameter _assemblyFiles;

        private bool _autoUnify;

        private string[] _candidateAssemblyFiles;

        private bool _copyLocalDependenciesWhenParentReferenceInGac;

        private bool _doNotCopyLocalIfInGac;

        private bool _findDependencies;

        private bool _findDependenciesOfExternallyResolvedReferences;

        private bool _findRelatedFiles;

        private bool _findSatellites;

        private bool _findSerializationAssemblies;

        // ITaskItem[]
        private TaskParameter _fullFrameworkAssemblyTables;

        private string[] _fullFrameworkFolders;

        private string[] _fullTargetFrameworkSubsetNames;

        private bool _ignoreDefaultInstalledAssemblySubsetTables;

        private bool _ignoreDefaultInstalledAssemblyTables;

        private bool _ignoreTargetFrameworkAttributeVersionMismatch;

        private bool _ignoreVersionForFrameworkReferences;

        // ITaskItem[]
        private TaskParameter _installedAssemblySubsetTables;

        // ITaskItem[]
        private TaskParameter _installedAssemblyTables;

        private string[] _latestTargetFrameworkDirectories;

        private string _profileName;

        // ITaskItem[]
        private TaskParameter _resolvedSDKReferences;

        private string[] _searchPaths;

        private bool _silent;

        private string _stateFile;

        private bool _supportsBindingRedirectGeneration;

        private string _targetedRuntimeVersion;

        private string[] _targetFrameworkDirectories;

        private string _targetFrameworkMoniker;

        private string _targetFrameworkMonikerDisplayName;

        private string[] _targetFrameworkSubsets;

        private string _targetFrameworkVersion;

        private string _targetProcessorArchitecture;

        private bool _unresolveFrameworkAssembliesFromHigherFrameworks;

        private bool _useResolveAssemblyReferenceService;

        private string _warnOrErrorOnTargetArchitectureMismatch;

        private string _currentPath;

        private string _assemblyInformationCacheOutputPath;

        // ITaskItem[]
        private TaskParameter _assemblyInformationCachePaths;

        public ResolveAssemblyReferenceRequest() { }

        public string[] AllowedAssemblyExtensions => _allowedAssemblyExtensions;

        public string[] AllowedRelatedFileExtensions => _allowedRelatedFileExtensions;

        public string AppConfigFile => _appConfigFile;

        public ITaskItem[] Assemblies => (ITaskItem[])_assemblies.WrappedParameter;

        public ITaskItem[] AssemblyFiles => (ITaskItem[])_assemblyFiles.WrappedParameter;

        public bool AutoUnify => _autoUnify;

        public string[] CandidateAssemblyFiles => _candidateAssemblyFiles;

        public bool CopyLocalDependenciesWhenParentReferenceInGac => _copyLocalDependenciesWhenParentReferenceInGac;

        public bool DoNotCopyLocalIfInGac => _doNotCopyLocalIfInGac;

        public bool FindDependencies => _findDependencies;

        public bool FindDependenciesOfExternallyResolvedReferences => _findDependenciesOfExternallyResolvedReferences;

        public bool FindRelatedFiles => _findRelatedFiles;

        public bool FindSatellites => _findSatellites;

        public bool FindSerializationAssemblies => _findSerializationAssemblies;

        public ITaskItem[] FullFrameworkAssemblyTables => (ITaskItem[])_fullFrameworkAssemblyTables.WrappedParameter;

        public string[] FullFrameworkFolders => _fullFrameworkFolders;

        public string[] FullTargetFrameworkSubsetNames => _fullTargetFrameworkSubsetNames;

        public bool IgnoreDefaultInstalledAssemblySubsetTables => _ignoreDefaultInstalledAssemblySubsetTables;

        public bool IgnoreDefaultInstalledAssemblyTables => _ignoreDefaultInstalledAssemblyTables;

        public bool IgnoreTargetFrameworkAttributeVersionMismatch => _ignoreTargetFrameworkAttributeVersionMismatch;

        public bool IgnoreVersionForFrameworkReferences => _ignoreVersionForFrameworkReferences;

        public ITaskItem[] InstalledAssemblySubsetTables => (ITaskItem[])_installedAssemblySubsetTables.WrappedParameter;

        public ITaskItem[] InstalledAssemblyTables => (ITaskItem[])_installedAssemblyTables.WrappedParameter;

        public string[] LatestTargetFrameworkDirectories => _latestTargetFrameworkDirectories;

        public string ProfileName => _profileName;

        public ITaskItem[] ResolvedSDKReferences => (ITaskItem[])_resolvedSDKReferences.WrappedParameter;

        public string[] SearchPaths => _searchPaths;

        public bool Silent => _silent;

        public string StateFile => _stateFile;

        public bool SupportsBindingRedirectGeneration => _supportsBindingRedirectGeneration;

        public string TargetedRuntimeVersion => _targetedRuntimeVersion;

        public string[] TargetFrameworkDirectories => _targetFrameworkDirectories;

        public string TargetFrameworkMoniker => _targetFrameworkMoniker;

        public string TargetFrameworkMonikerDisplayName => _targetFrameworkMonikerDisplayName;

        public string[] TargetFrameworkSubsets => _targetFrameworkSubsets;

        public string TargetFrameworkVersion => _targetFrameworkVersion;

        public string TargetProcessorArchitecture => _targetProcessorArchitecture;

        public bool UnresolveFrameworkAssembliesFromHigherFrameworks => _unresolveFrameworkAssembliesFromHigherFrameworks;

        public bool UseResolveAssemblyReferenceService => _useResolveAssemblyReferenceService;

        public string WarnOrErrorOnTargetArchitectureMismatch => _warnOrErrorOnTargetArchitectureMismatch;

        public string CurrentPath => _currentPath;

        public string AssemblyInformationCacheOutputPath => _assemblyInformationCacheOutputPath;

        public ITaskItem[] AssemblyInformationCachePaths => (ITaskItem[])_assemblyInformationCachePaths.WrappedParameter;

        NodePacketType INodePacket.Type => NodePacketType.ResolveAssemblyReferenceRequest;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ResolveAssemblyReferenceRequest(ITranslator translator)
        {
            Translate(translator);
        }


        #region INodePacket Members

        /// <summary>
        /// Reads/writes this packet
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _allowedAssemblyExtensions);
            translator.Translate(ref _allowedRelatedFileExtensions);
            translator.Translate(ref _appConfigFile);
            translator.Translate(ref _assemblies);
            translator.Translate(ref _assemblyFiles);
            translator.Translate(ref _autoUnify);
            translator.Translate(ref _candidateAssemblyFiles);
            translator.Translate(ref _copyLocalDependenciesWhenParentReferenceInGac);
            translator.Translate(ref _doNotCopyLocalIfInGac);
            translator.Translate(ref _findDependencies);
            translator.Translate(ref _findDependenciesOfExternallyResolvedReferences);
            translator.Translate(ref _findRelatedFiles);
            translator.Translate(ref _findSatellites);
            translator.Translate(ref _findSerializationAssemblies);
            translator.Translate(ref _fullFrameworkAssemblyTables);
            translator.Translate(ref _fullFrameworkFolders);
            translator.Translate(ref _fullTargetFrameworkSubsetNames);
            translator.Translate(ref _ignoreDefaultInstalledAssemblySubsetTables);
            translator.Translate(ref _ignoreDefaultInstalledAssemblyTables);
            translator.Translate(ref _ignoreTargetFrameworkAttributeVersionMismatch);
            translator.Translate(ref _ignoreVersionForFrameworkReferences);
            translator.Translate(ref _installedAssemblySubsetTables);
            translator.Translate(ref _installedAssemblyTables);
            translator.Translate(ref _latestTargetFrameworkDirectories);
            translator.Translate(ref _profileName);
            translator.Translate(ref _resolvedSDKReferences);
            translator.Translate(ref _searchPaths);
            translator.Translate(ref _silent);
            translator.Translate(ref _stateFile);
            translator.Translate(ref _supportsBindingRedirectGeneration);
            translator.Translate(ref _targetedRuntimeVersion);
            translator.Translate(ref _targetFrameworkDirectories);
            translator.Translate(ref _targetFrameworkMoniker);
            translator.Translate(ref _targetFrameworkMonikerDisplayName);
            translator.Translate(ref _targetFrameworkSubsets);
            translator.Translate(ref _targetFrameworkVersion);
            translator.Translate(ref _targetProcessorArchitecture);
            translator.Translate(ref _unresolveFrameworkAssembliesFromHigherFrameworks);
            translator.Translate(ref _useResolveAssemblyReferenceService);
            translator.Translate(ref _warnOrErrorOnTargetArchitectureMismatch);
            translator.Translate(ref _currentPath);
            translator.Translate(ref _assemblyInformationCacheOutputPath);
            translator.Translate(ref _assemblyInformationCachePaths);
    }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ResolveAssemblyReferenceRequest(translator);
        }

        #endregion
    }
}
