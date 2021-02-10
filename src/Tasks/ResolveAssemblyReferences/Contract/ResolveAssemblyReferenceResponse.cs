// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceResponse : INodePacket
    {
        private TaskParameter _copyLocalFiles;

        public string _dependsOnNETStandard;

        public string _dependsOnSystemRuntime;

        private TaskParameter _filesWritten;

        private TaskParameter _relatedFiles;

        private TaskParameter _resolvedDependencyFiles;

        private TaskParameter _resolvedFiles;

        private TaskParameter _satelliteFiles;

        private TaskParameter _scatterFiles;

        private TaskParameter _serializationAssemblyFiles;

        private TaskParameter _suggestedRedirects;

        public ResolveAssemblyReferenceResponse()
        {
        }

        public ITaskItem[] CopyLocalFiles => (ITaskItem[])_copyLocalFiles.WrappedParameter;

        public string DependsOnNETStandard => _dependsOnNETStandard;

        public string DependsOnSystemRuntime => _dependsOnSystemRuntime;

        public ITaskItem[] FilesWritten => (ITaskItem[])_filesWritten.WrappedParameter;

        public ITaskItem[] RelatedFiles => (ITaskItem[])_relatedFiles.WrappedParameter;

        public ITaskItem[] ResolvedDependencyFiles => (ITaskItem[])_resolvedDependencyFiles.WrappedParameter;

        public ITaskItem[] ResolvedFiles => (ITaskItem[])_resolvedFiles.WrappedParameter;

        public ITaskItem[] SatelliteFiles => (ITaskItem[])_satelliteFiles.WrappedParameter;

        public ITaskItem[] ScatterFiles => (ITaskItem[])_scatterFiles.WrappedParameter;

        public ITaskItem[] SerializationAssemblyFiles => (ITaskItem[])_serializationAssemblyFiles.WrappedParameter;

        public ITaskItem[] SuggestedRedirects => (ITaskItem[])_suggestedRedirects.WrappedParameter;

        NodePacketType INodePacket.Type => NodePacketType.ResolveAssemblyReferenceResponse;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ResolveAssemblyReferenceResponse(ITranslator translator)
        {
            Translate(translator);
        }

        #region INodePacket Members

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _copyLocalFiles);
            translator.Translate(ref _dependsOnNETStandard);
            translator.Translate(ref _dependsOnSystemRuntime);
            translator.Translate(ref _filesWritten);
            translator.Translate(ref _relatedFiles);
            translator.Translate(ref _resolvedDependencyFiles);
            translator.Translate(ref _resolvedFiles);
            translator.Translate(ref _satelliteFiles);
            translator.Translate(ref _scatterFiles);
            translator.Translate(ref _serializationAssemblyFiles);
            translator.Translate(ref _suggestedRedirects);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ResolveAssemblyReferenceResponse(translator);
        }

        #endregion
    }
}
