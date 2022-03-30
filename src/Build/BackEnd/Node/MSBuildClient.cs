using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.OutOfProcServerNode;

namespace Microsoft.Build.Client
{
    // TODO: should it be static? consider that.
    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    /// // TODO: argument/attribute saying that it is experimental API
    public class MSBuildClient : INodePacketFactory, INodePacketHandler
    {
        /// <summary>
        /// Enumeration of the various ways in which the MSBuildClient.exe application can exit.
        /// </summary>
        public enum ExitType
        {
            /// <summary>
            /// The application executed successfully.
            /// </summary>
            Success,
            /// <summary>
            /// There was a syntax error in a command line argument.
            /// </summary>
            SwitchError,
            /// <summary>
            /// A command line argument was not valid.
            /// </summary>
            InitializationError,
            /// <summary>
            /// The build failed.
            /// </summary>
            BuildError,
            /// <summary>
            /// A logger aborted the build.
            /// </summary>
            LoggerAbort,
            /// <summary>
            /// A logger failed unexpectedly.
            /// </summary>
            LoggerFailure,
            /// <summary>
            /// The build stopped unexpectedly, for example,
            /// because a child died or hung.
            /// </summary>
            Unexpected,
            /// <summary>
            /// A project cache failed unexpectedly.
            /// </summary>
            ProjectCacheFailure
        }

        /// <summary>
        /// The endpoint used to talk to the host.
        /// </summary>
        private INodeEndpoint _nodeEndpoint = default!;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private readonly NodePacketFactory _packetFactory;

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        private readonly ConcurrentQueue<INodePacket> _receivedPackets;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private readonly AutoResetEvent _packetReceivedEvent;

        /// <summary>
        /// The event which is set when we should shut down.
        /// </summary>
        private readonly ManualResetEvent _shutdownEvent;

        /// <summary>
        /// The reason we are shutting down.
        /// </summary>
        private NodeEngineShutdownReason _shutdownReason;

        /// <summary>
        /// The exception, if any, which caused shutdown.
        /// </summary>
        private Exception? _shutdownException = null;

        public MSBuildClient()
        {
            _receivedPackets = new ConcurrentQueue<INodePacket>();
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new ManualResetEvent(false);
            _packetFactory = new NodePacketFactory();

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeConsole, ServerNodeConsoleWrite.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeResponse, ServerNodeResponse.FactoryForDeserialization, this);
        }

    public int Execute(string commandLine)
        {
            // TODO: figure out the location.
            // string msBuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

            // string msBuildLocation = @"C:\Users\alinama\work\MSBUILD\msbuild-1\msbuild\artifacts\bin\MSBuild\Debug\net6.0\MSBuild.dll";
            string msBuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

            string[] msBuildServerOptions = new [] {
                "/nologo",
                "/nodemode:8",
                "/nodeReuse:true"
            }.ToArray();

            ProcessStartInfo msBuildServerStartInfo = GetMSBuildServerProcessStartInfo(msBuildLocation,msBuildServerOptions, new Dictionary<string, string>());

            // TODO: remove later. debug.
            StreamWriter sw = new StreamWriter(@"C:\Users\alinama\work\MSBUILD\msbuild-1\client-handshake.txt");
            sw.WriteLine(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: true, is64Bit: EnvironmentUtilities.Is64BitProcess));
            sw.WriteLine(msBuildLocation);
            sw.Close();

            var handshake = new ServerNodeHandshake(
                CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: true, is64Bit: EnvironmentUtilities.Is64BitProcess),
                // CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: enableReuse, lowPriority: lowPriority, is64Bit: EnvironmentUtilities.Is64BitProcess),
                msBuildLocation);

            string pipeName = GetPipeNameOrPath("MSBuildServer-" + handshake.ComputeHash());

            // check if server is running
            string serverRunningMutexName = $@"Global\server-running-{pipeName}";
            string serverBusyMutexName = $@"Global\server-busy-{pipeName}";

            var serverWasAlreadyRunning = ServerNamedMutex.WasOpen(serverRunningMutexName);
            if (!serverWasAlreadyRunning)
            {
                Process msbuildProcess = LaunchNode(msBuildServerStartInfo);
                Console.WriteLine("Server is launched.");
            }

            var serverWasBusy = ServerNamedMutex.WasOpen(serverBusyMutexName);
            if (serverWasBusy)
            {
                Console.WriteLine("Server is busy - that IS unexpected - we shall fallback to former behavior.");
                throw new InvalidOperationException("Server is busy - that IS unexpected - we shall fallback to former behavior. NOT IMPLEMENTED YET");
            }

            int connectTimeout = serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000;

            // Connection to server node.
            _nodeEndpoint = new MSBuildClientEndpoint(pipeName, handshake, connectTimeout);
            _nodeEndpoint.OnLinkStatusChanged += OnLinkStatusChanged;
            _nodeEndpoint.Connect(this);

            // Send the build command to server.

            Dictionary<string, string> envVars = new Dictionary<string, string>();
            var vars = Environment.GetEnvironmentVariables();
            foreach (var key in vars.Keys)
            {
                envVars[(string)key] = "" + (string)vars[key];
            }

            foreach (var pair in msBuildServerStartInfo.Environment)
            {
                envVars[pair.Key] = pair.Value;
            }

            var buildCommand = new ServerNodeBuildCommand(
                commandLine: commandLine,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: envVars,
                CultureInfo.CurrentCulture,
                CultureInfo.CurrentUICulture);

            SendPacket(buildCommand);

            // Wait and process packets from server node.

            int exitCode = 0;
            var waitHandles = new WaitHandle[] { _shutdownEvent, _packetReceivedEvent };
            while (true)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        // TODO: Shutdown handling?
                        return 1;

                    case 1:

                        while (_receivedPackets.TryDequeue(out INodePacket? packet))
                        {
                            if (packet != null)
                            {
                                HandlePacket(packet);
                            }
                        }

                        break;
                }
            }


            // connect to it
            //NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            //nodeStream.Connect(serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000);
            //Console.WriteLine("Client is connected to server.");

            //int[] handshakeComponents = handshake.RetrieveHandshakeComponents();
            //for (int i = 0; i < handshakeComponents.Length; i++)
            //{
            //    CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], pipeName);
            //    WriteIntForHandshake(nodeStream, handshakeComponents[i]);
            //}

            //// This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
            //WriteIntForHandshake(nodeStream, ServerNodeHandshake.EndOfHandshakeSignal);

            //CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);

            //// TODO

            //ReadEndOfHandshakeSignal(nodeStream, timeout: 1000);

            //CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);

            //Dictionary<string, string> envVars = new Dictionary<string, string>();
            //var vars = Environment.GetEnvironmentVariables();
            //foreach (var key in vars.Keys)
            //{
            //    envVars[(string)key] = "" + (string)vars[key];
            //}

            //foreach (var pair in msBuildServerStartInfo.Environment)
            //{
            //    envVars[pair.Key] = pair.Value;
            //}

            //var buildCommand = new EntryNodeCommand(
            //    commandLine: commandLine,
            //    startupDirectory: Directory.GetCurrentDirectory(),
            //    buildProcessEnvironment: envVars,
            //    CultureInfo.CurrentCulture,
            //    CultureInfo.CurrentUICulture);

            //buildCommand.WriteToStream(nodeStream);

            //CommunicationsUtilities.Trace("Build command send...");
            //
            //while (true)
            //{
            //    var packet = ReadPacket(nodeStream);
            //    if (packet is EntryNodeConsoleWrite consoleWrite)
            //    {
            //        switch (consoleWrite.OutputType)
            //        {
            //            case 1:
            //                Console.Write(consoleWrite.Text);
            //                break;
            //            case 2:
            //                Console.Error.Write(consoleWrite.Text);
            //                break;
            //            default:
            //                throw new InvalidOperationException($"Unexpected console output type {consoleWrite.OutputType}");
            //        }
            //    }
            //    else if (packet is EntryNodeResponse response)
            //    {
            //        CommunicationsUtilities.Trace($"Build response received: exit code {response.ExitCode}, exit type '{response.ExitType}'");
            //        exitCode = response.ExitCode;
            //        break;
            //    }
            //    else
            //    {
            //        throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
            //    }
            //}

            return exitCode;
        }

        /// <summary>
        /// Callback for logging packets to be sent.
        /// </summary>
        private void SendPacket(INodePacket packet)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(packet);
            }
        }
        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ServerNodeConsole:
                    HandleServerNodeConsole((ServerNodeConsoleWrite)packet);
                    break;
                case NodePacketType.ServerNodeResponse:
                    HandleServerNodeResponse((ServerNodeResponse)packet);
                    break;
            }
        }

        private void HandleServerNodeResponse(ServerNodeResponse packet) => throw new NotImplementedException();
        private void HandleServerNodeConsole(ServerNodeConsoleWrite packet) => throw new NotImplementedException();

        private static Process LaunchNode(ProcessStartInfo processStartInfo)
        {
            // Redirect the streams of worker nodes so that this 
            // parent doesn't wait on idle worker nodes to close streams
            // after the build is complete.
            processStartInfo.RedirectStandardInput = false;
            processStartInfo.RedirectStandardOutput = false;
            processStartInfo.RedirectStandardError = false;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;

            Process process;
            try
            {
                process = Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("MSBuild server node failed to lunch", ex);
            }

            return process;
        }

        private static ProcessStartInfo GetMSBuildServerProcessStartInfo(string msBuildLocation, string[] arguments, Dictionary<string, string> environmentVariables)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = msBuildLocation,
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false
            };

            foreach (var entry in environmentVariables)
            {
                processInfo.Environment[entry.Key] = entry.Value;
            };

            return processInfo;
        }

        private static string GetPipeNameOrPath(string pipeName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // If we're on a Unix machine then named pipes are implemented using Unix Domain Sockets.
                // Most Unix systems have a maximum path length limit for Unix Domain Sockets, with
                // Mac having a particularly short one. Mac also has a generated temp directory that
                // can be quite long, leaving very little room for the actual pipe name. Fortunately,
                // '/tmp' is mandated by POSIX to always be a valid temp directory, so we can use that
                // instead.
                return Path.Combine("/tmp", pipeName);
            }

            return pipeName;
        }

        /// <summary>
        /// Extension method to write a series of bytes to a stream
        /// </summary>
        internal static void WriteIntForHandshake(PipeStream stream, int value)
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

        internal static void ReadEndOfHandshakeSignal(PipeStream stream, int timeout)
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

#region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type for which the handler should be registered.</param>
        /// <param name="factory">The factory used to create packets.</param>
        /// <param name="handler">The handler for the packets.</param>
        void INodePacketFactory.RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The type of packet for which the handler should be unregistered.</param>
        void INodePacketFactory.UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Deserializes and routes a packer to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator to use as a source for packet data.</param>
        void INodePacketFactory.DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes a packet to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node id from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        void INodePacketFactory.RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion

#region INodePacketHandler Members

        /// <summary>
        /// Called when a packet has been received.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        void INodePacketHandler.PacketReceived(int node, INodePacket packet)
        {
            _receivedPackets.Enqueue(packet);
            _packetReceivedEvent.Set();
        }
#endregion


        /// <summary>
        /// Event handler for the node endpoint's LinkStatusChanged event.
        /// </summary>
        private void OnLinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            switch (status)
            {
                case LinkStatus.ConnectionFailed:
                case LinkStatus.Failed:
                    _shutdownReason = NodeEngineShutdownReason.ConnectionFailed;
                    _shutdownEvent.Set();
                    break;

                case LinkStatus.Inactive:
                    break;

                case LinkStatus.Active:
                    break;

                default:
                    break;
            }
        }
    }
}
