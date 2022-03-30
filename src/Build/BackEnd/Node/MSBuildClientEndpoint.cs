// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    /// //TODO:  NodeEndpointOutOfProcBase has no connect function. do not inherit from it.
    internal class MSBuildClientEndpoint : INodeEndpoint
    {
#region Private Data
        private readonly IHandshake _handshake;

        private readonly string _pipename;

        private int _connectTimeout;

        /// <summary>
        /// The current communication status of the node.
        /// </summary>
        private LinkStatus _status;

        /// <summary>
        /// A way to cache a byte array when writing out packets
        /// </summary>
        private MemoryStream _packetStream;

        /// <summary>
        /// The pipe client used by the nodes.
        /// </summary>
        private NamedPipeClientStream _nodeStream;

        /// <summary>
        /// A binary writer to help write into <see cref="_packetStream"/>
        /// </summary>
        private BinaryWriter _binaryWriter;

#endregion

#region INodeEndpoint Events
        /// <summary>
        /// Raised when the link status has changed.
        /// </summary>
        public event LinkStatusChangedDelegate OnLinkStatusChanged;
#endregion

#region Constructors and Factories
        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        /// <param name="handshake"></param>
        internal MSBuildClientEndpoint(string pipeName, IHandshake handshake, int connectTimeout)
        {
            _handshake = handshake;
            _pipename = pipeName;
            _connectTimeout = connectTimeout;

            _status = LinkStatus.Inactive;

            _packetStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetStream);
        }
#endregion

#region INodeEndpoint Properties
        /// <summary>
        /// Returns the link status of this node.
        /// </summary>
        public LinkStatus LinkStatus
        {
            get { return _status; }
        }
#endregion

        /// <summary>
        /// Updates the current link status if it has changed and notifies any registered delegates.
        /// </summary>
        /// <param name="newStatus">The status the node should now be in.</param>
        protected void ChangeLinkStatus(LinkStatus newStatus)
        {
            ErrorUtilities.VerifyThrow(_status != newStatus, "Attempting to change status to existing status {0}.", _status);
            CommunicationsUtilities.Trace("Changing link status from {0} to {1}", _status.ToString(), newStatus.ToString());
            _status = newStatus;
            RaiseLinkStatusChanged(_status);
        }

        /// <summary>
        /// Invokes the OnLinkStatusChanged event in a thread-safe manner.
        /// </summary>
        /// <param name="newStatus">The new status of the endpoint link.</param>
        private void RaiseLinkStatusChanged(LinkStatus newStatus)
        {
            OnLinkStatusChanged?.Invoke(this, newStatus);
        }

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected IHandshake GetHandshake()
        {
            return _handshake;
        }

        void INodeEndpoint.Connect(INodePacketFactory factory)
        {
            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", _pipename, PipeDirection.InOut, PipeOptions.Asynchronous);

            CommunicationsUtilities.Trace("Client is connecting to server.");
            nodeStream.Connect(_connectTimeout);

            CommunicationsUtilities.Trace("Verifying handshake.");
            int[] handshakeComponents = _handshake.RetrieveHandshakeComponents();
            for (int i = 0; i < handshakeComponents.Length; i++)
            {
                CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], _pipename);
                WriteIntForHandshake(nodeStream, handshakeComponents[i]);
            }

            // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
            WriteIntForHandshake(nodeStream, ServerNodeHandshake.EndOfHandshakeSignal);

            CommunicationsUtilities.Trace("Reading handshake from pipe {0}", _pipename);

            ReadEndOfHandshakeSignal(nodeStream, timeout: 1000);

            CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", _pipename);

            ChangeLinkStatus(LinkStatus.Active);
            _nodeStream = nodeStream;
        }

        void INodeEndpoint.Disconnect()
        {
            ChangeLinkStatus(LinkStatus.Inactive);
        }

        void INodeEndpoint.Listen(INodePacketFactory factory)
        {
            ErrorUtilities.ThrowInternalError("Listen() not valid on the msbuild client endpoint.");
        }

        void INodeEndpoint.SendData(INodePacket packet)
        {
            if (_status == LinkStatus.Active)
            {
                var packetStream = _packetStream;
                packetStream.SetLength(0);

                ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(packetStream);

                packetStream.WriteByte((byte)packet.Type);

                // Pad for packet length
                _binaryWriter.Write(0);

                // Reset the position in the write buffer.
                packet.Translate(writeTranslator);

                int packetStreamLength = (int)packetStream.Position;

                // Now write in the actual packet length
                packetStream.Position = 1;
                _binaryWriter.Write(packetStreamLength - 5);

                _nodeStream.Write(packetStream.GetBuffer(), 0, packetStreamLength);
            }
        }


        /// <summary>
        /// Extension method to write a series of bytes to a stream
        /// </summary>
        private static void WriteIntForHandshake(PipeStream stream, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // We want to read the long and send it from left to right (this means big endian)
            // if we are little endian we need to reverse the array to keep the left to right reading
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        private static void ReadEndOfHandshakeSignal(PipeStream stream, int timeout)
        {
            // Accept only the first byte of the EndOfHandshakeSignal
            int valueRead = ReadIntForHandshake(stream, timeout: 1000);

            if (valueRead != ServerNodeHandshake.EndOfHandshakeSignal)
            {
                CommunicationsUtilities.Trace("Expected end of handshake signal but received {0}. Probably the host is a different MSBuild build.", valueRead);
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Extension method to read a series of bytes from a stream.
        /// If specified, leading byte matches one in the supplied array if any, returns rejection byte and throws IOException.
        /// </summary>
        internal static int ReadIntForHandshake(PipeStream stream, int timeout)
        {
            byte[] bytes = new byte[4];

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Enforce a minimum timeout because the Windows code can pass
                // a timeout of 0 for the connection, but that doesn't work for
                // the actual timeout here.
                timeout = Math.Max(timeout, 50);

                // A legacy MSBuild.exe won't try to connect to MSBuild running
                // in a dotnet host process, so we can read the bytes simply.
                var readTask = stream.ReadAsync(bytes, 0, bytes.Length);

                // Manual timeout here because the timeout passed to Connect() just before
                // calling this method does not apply on UNIX domain socket-based
                // implementations of PipeStream.
                // https://github.com/dotnet/corefx/issues/28791
                if (!readTask.Wait(timeout))
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Did not receive return handshake in {0}ms", timeout));
                }

                readTask.GetAwaiter().GetResult();
            }
            else
            {
                // Legacy approach with an early-abort for connection attempts from ancient MSBuild.exes
                for (int i = 0; i < bytes.Length; i++)
                {
                    int read = stream.ReadByte();

                    if (read == -1)
                    {
                        // We've unexpectly reached end of stream.
                        // We are now in a bad state, disconnect on our end
                        throw new IOException(String.Format(CultureInfo.InvariantCulture, "Unexpected end of stream while reading for handshake"));
                    }

                    bytes[i] = Convert.ToByte(read);
                }
            }

            int result;

            try
            {
                // We want to read the long and send it from left to right (this means big endian)
                // If we are little endian the stream has already been reversed by the sender, we need to reverse it again to get the original number
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                result = BitConverter.ToInt32(bytes, 0 /* start index */);
            }
            catch (ArgumentException ex)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture, "Failed to convert the handshake to big-endian. {0}", ex.Message));
            }

            return result;
        }
    }
}

