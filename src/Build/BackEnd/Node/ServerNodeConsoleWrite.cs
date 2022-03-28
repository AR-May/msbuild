// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BackEnd
{
    internal class ServerNodeConsoleWrite : INodePacket
    {
        public string Text { get; private set; } = default!;

        /// <summary>
        /// 1 = stdout, 2 = stderr
        /// </summary>
        public byte OutputType { get; private set; } = default!;

        public ServerNodeConsoleWrite(string text, byte outputType)
        {
            Text = text;
            OutputType = outputType;
        }

        private ServerNodeConsoleWrite()
        {
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeConsole;

        #endregion

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var bw = translator.Writer;

                bw.Write(Text);
                bw.Write(OutputType);
            }
            else
            {
                var br = translator.Reader;

                Text = br.ReadString();
                OutputType = br.ReadByte();
            }
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeConsoleWrite command = new();
            command.Translate(translator);

            return command;
        }

    }
}
