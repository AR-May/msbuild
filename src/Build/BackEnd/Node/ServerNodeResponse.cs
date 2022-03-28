// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace Microsoft.Build.BackEnd
{
    internal class ServerNodeResponse : INodePacket
    {
        public ServerNodeResponse(int exitCode, string exitType)
        {
            ExitCode = exitCode;
            ExitType = exitType;
        }

        private ServerNodeResponse()
        {
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.ServerNodeBuildCommand
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeResponse;

        #endregion

        public int ExitCode { get; private set; } = default!;

        public string ExitType { get; private set; } = default!;

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var bw = translator.Writer;

                bw.Write(ExitCode);
                bw.Write(ExitType);
            }
            else
            {
                var br = translator.Reader;

                ExitCode = br.ReadInt32();
                ExitType = br.ReadString();
            }
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static ServerNodeResponse FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeResponse packet = new ServerNodeResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
