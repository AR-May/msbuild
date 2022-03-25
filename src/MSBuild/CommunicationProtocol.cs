using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Microsoft.Build.Client
{
    internal class EntryNodeCommand
    {
        /// <summary>
        /// The startup directory
        /// </summary>
        private readonly string _commandLine;

        /// <summary>
        /// The startup directory
        /// </summary>
        private readonly string _startupDirectory;

        /// <summary>
        /// The process environment.
        /// </summary>
        private readonly IDictionary<string, string> _buildProcessEnvironment;

        /// <summary>
        /// The culture
        /// </summary>
        private readonly CultureInfo _culture;

        /// <summary>
        /// The UI culture.
        /// </summary>
        private readonly CultureInfo _uiCulture;

        public EntryNodeCommand(string commandLine, string startupDirectory, IDictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            _commandLine = commandLine;
            _startupDirectory = startupDirectory;
            _buildProcessEnvironment = buildProcessEnvironment;
            _culture = culture;
            _uiCulture = uiCulture;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.EntryNodeCommand
        /// </summary>
        public byte PacketType => 0xF0;

        #endregion

        /// <summary>
        /// The startup directory
        /// </summary>
        public string CommandLine => _commandLine;

        /// <summary>
        /// The startup directory
        /// </summary>
        public string StartupDirectory => _startupDirectory;

        /// <summary>
        /// The process environment.
        /// </summary>
        public IDictionary<string, string> BuildProcessEnvironment => _buildProcessEnvironment;

        /// <summary>
        /// The culture
        /// </summary>
        public CultureInfo CultureName => _culture;

        /// <summary>
        /// The UI culture.
        /// </summary>
        public CultureInfo UICulture => _uiCulture;

        public void WriteToStream(Stream outputStream)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // header
            bw.Write((byte)PacketType);
            bw.Write((int)0);
            int headerSize = (int)ms.Position;

            // body
            bw.Write(_commandLine);
            bw.Write(_startupDirectory);
            bw.Write((int)_buildProcessEnvironment.Count);
            foreach (var pair in _buildProcessEnvironment)
            {
                bw.Write(pair.Key);
                bw.Write(pair.Value);
            }
            bw.Write(_culture.Name);
            bw.Write(_uiCulture.Name);

            int bodySize = (int)ms.Position - headerSize;

            ms.Position = 1;
            ms.WriteByte((byte)bodySize);
            ms.WriteByte((byte)(bodySize >> 8));
            ms.WriteByte((byte)(bodySize >> 16));
            ms.WriteByte((byte)(bodySize >> 24));

            // copy packet message bytes into stream
            var bytes = ms.GetBuffer();
            outputStream.Write(bytes, 0, headerSize + bodySize);
        }
    }

    internal class EntryNodeResponse
    {
        private int _exitCode;
        private string _exitType;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private EntryNodeResponse()
        {
            _exitCode = 0;
            _exitType = "";
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.EntryNodeResponse
        /// </summary>
        public const byte PacketType = 0xF1;

        #endregion

        public int ExitCode => _exitCode;

        public string ExitType => _exitType;

        public static EntryNodeResponse DeserializeFromStream(Stream inputStream)
        {
            EntryNodeResponse response = new EntryNodeResponse();

            using var br = new BinaryReader(inputStream);

            response._exitCode = br.ReadInt32();
            response._exitType = br.ReadString();

            return response;
        }
    }

    internal class EntryNodeConsoleWrite
    {
        private string _text;
        private byte _outputType;

        public string Text => _text;

        /// <summary>
        /// 1 = stdout, 2 = stderr
        /// </summary>
        public byte OutputType => _outputType;

        private EntryNodeConsoleWrite()
        {
            _text = "";
            _outputType = 0;
    }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.EntryNodeInfo
        /// </summary>
        public const byte PacketType = 0xF2;

        #endregion

        public static EntryNodeConsoleWrite DeserializeFromStream(Stream inputStream)
        {
            EntryNodeConsoleWrite consoleWrite = new EntryNodeConsoleWrite();

            using var br = new BinaryReader(inputStream);

            consoleWrite._text = br.ReadString();
            consoleWrite._outputType = br.ReadByte();

            return consoleWrite;
        }
    }
}
