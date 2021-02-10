// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceResult : INodePacket
    {

        private bool _taskResult;

        private ResolveAssemblyReferenceResponse _response;

        private List<BuildEventArgs> _buildEvents;

        public ResolveAssemblyReferenceResult()
        {
        }

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceResponse response)
        {
            _taskResult = taskResult;
            _response = response;
            _buildEvents = null;
        }

        public bool TaskResult => _taskResult;

        public ResolveAssemblyReferenceResponse Response => _response;

        public List<BuildEventArgs> BuildEvents => _buildEvents;

        public NodePacketType Type => NodePacketType.ResolveAssemblyReferenceResult;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ResolveAssemblyReferenceResult(ITranslator translator)
        {
            Translate(translator);
        }

        #region INodePacket Members

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _taskResult);
            translator.Translate(ref _response);
            // TODO: serialization for _buildEvents.
            //translator.Translate(ref _buildEvents);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ResolveAssemblyReferenceResult(translator);
        }

        #endregion
    }
}
