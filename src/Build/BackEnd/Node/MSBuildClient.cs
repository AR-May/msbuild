using System;
using System.Collections;
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
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.OutOfProcServerNode;
#nullable enable 

namespace Microsoft.Build.Experimental.Client
{
    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    /// // TODO: Add argument/attribute saying that it is an experimental API
    public class MSBuildClient : INodePacketHandler, INodePacketFactory
    {
        /// <summary>
        /// The build inherits all the environment variables from the client prosess.
        /// This property allows to add extra environment variables or reset some of the existing ones.
        /// </summary>
        public Dictionary<string, string> ServerEnvironmentVariables { get; set; }

        #region Private fields
        /// <summary>
        /// Location of msbuild dll or exe.
        /// </summary>
        private string _msBuildLocation;

        /// <summary>
        /// Location of executable file to launch the server process. That should be either dotnet.exe or MSBuild.exe location.
        /// </summary>
        private string _exeLocation;

        /// <summary>
        /// Location of dll file to launch the server process if needed. Empty if executable is msbuild.exe and not empty if dotnet.exe.
        /// </summary>
        private string _dllLocation;

        /// <summary>
        /// The MSBuild client execution result.
        /// </summary>
        private MSBuildClientExitResult _exitResult;

        /// <summary>
        /// Whether MSBuild server finished the build.
        /// </summary>
        private bool _buildFinished = false;

        /// <summary>
        /// Handshake between server and client.
        /// </summary>
        private ServerNodeHandshake _handshake;

        /// <summary>
        /// The named pipe name for client-server communication.
        /// </summary>
        private string _pipeName;

        /// <summary>
        /// The named pipe stream for client-server communication.
        /// </summary>
        private NamedPipeClientStream _nodeStream;
        #endregion
        
/*
        /// <summary>
        /// The event which is set when we connection failed.
        /// </summary>
        private readonly ManualResetEvent _connectionFailedEvent;

        /// <summary>
        /// The event which is set when we should shut down.
        /// </summary>
        private readonly ManualResetEvent _shutdownEvent;
        
*/

        /// <summary>
        /// The packet factory.
        /// </summary>
        private readonly NodePacketFactory _packetFactory;

        /// <summary>
        /// Per-node shared read buffer.
        /// </summary>
        private SharedReadBuffer _sharedReadBuffer;

        private MemoryStream _packetMemoryStream;
        private BinaryWriter _binaryWriter;

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        private readonly ConcurrentQueue<INodePacket> _receivedPackets;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private readonly AutoResetEvent _packetReceivedEvent;

/*
        #region Message pump


        /// <summary>
        /// Object used as a lock source for the async data
        /// </summary>
        private object _asyncDataMonitor;

        /// <summary>
        /// Set when the asynchronous packet pump should terminate
        /// </summary>
        private AutoResetEvent _terminatePacketPump;

        /// <summary>
        /// The thread which runs the asynchronous packet pump
        /// </summary>
        private Thread _packetPump;
        #endregion
*/

        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        public MSBuildClient(string msbuildLocation, string exeLocation, string dllLocation)
        {
            _msBuildLocation = msbuildLocation;
            _exeLocation = exeLocation;
            _dllLocation = dllLocation;

            ServerEnvironmentVariables = new();
            _exitResult = new();

            _handshake = GetHandshake();
            _pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuildServer-" + _handshake.ComputeHash());
            _nodeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
            );

            _receivedPackets = new ConcurrentQueue<INodePacket>();
            _packetReceivedEvent = new AutoResetEvent(false);
            _packetFactory = new NodePacketFactory();
            _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();
            _packetMemoryStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetMemoryStream);


            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeConsoleWrite, ServerNodeConsoleWrite.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeResponse, ServerNodeResponse.FactoryForDeserialization, this);
        
/*
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new ManualResetEvent(false);
*/
        }

/*        
        /// <summary>
        /// Internal constructor. Used for testing.
        /// </summary>
        internal MSBuildClient(
            ServerNodeHandshake handshake,
            string pipeName,
            NamedPipeClientStream nodeStream,
            string msbuildLocation,
            string exeLocation,
            string dllLocation
        )
        {
            throw new NotImplementedException();
        }
*/

        /// <summary>
        /// Orchestrates the execution of the build on the server, responsible
        /// for client-server communication.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <returns>A value of type MSBuildClientExitResult that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public MSBuildClientExitResult Execute(string commandLine)
        {
            string serverRunningMutexName = $@"Global\server-running-{_pipeName}";
            string serverBusyMutexName = $@"Global\server-busy-{_pipeName}";

            // Start server it if is not running.
            bool serverWasAlreadyRunning = ServerNamedMutex.WasOpen(serverRunningMutexName);
            if (!serverWasAlreadyRunning)
            {
                if (!LaunchMSBuildServer())
                {
                    // Failed to launch MSBuild server.
                    return _exitResult;
                }
            }

            // Check that server is not busy.
            var serverWasBusy = ServerNamedMutex.WasOpen(serverBusyMutexName);
            if (serverWasBusy)
            {
                CommunicationsUtilities.Trace("Server is busy, falling back to former behavior.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                return _exitResult;
            }

            // Connect to server.
            if (!ConnectToServer(serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000))
            {
                CommunicationsUtilities.Trace("Failure to connect to a server.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return _exitResult;
            }

            // Send build command.
            SendBuildCommand(commandLine, _nodeStream);

/*
            InitializeAsyncPacketThread();

            var waitHandles = new WaitHandle[] {_shutdownEvent, _packetReceivedEvent };

            // Get the current directory before doing any work. We need this so we can restore the directory when the node shutsdown.
            while (!_buildFinished)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        CommunicationsUtilities.Trace($"Shutdown.");
                        _exitResult.MSBuildClientExitType = MSBuildClientExitType.Shutdown;
                        return _exitResult;

                    case 1:

                        while (_receivedPackets.TryDequeue(out INodePacket? packet) && (!_buildFinished))
                        {
                            if (packet != null)
                            {
                                try
                                {
                                    HandlePacket(packet);
                                }
                                catch (Exception ex)
                                {
                                    CommunicationsUtilities.Trace($"HandlePacket error: {ex.Message}");
                                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
                                    return _exitResult;
                                }
                            }
                        }

                        break;
                }
            }
*/

            // Read server responses.
            _buildFinished = false;
            while (!_buildFinished)
            {
                INodePacket packet;

                try
                {
                    packet = ReadPacket(_nodeStream);
                }
                catch
                {
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
                    return _exitResult;
                }

                try
                {
                    HandlePacket(packet);
                }
                catch (Exception ex)
                {
                    CommunicationsUtilities.Trace($"HandlePacket error: {ex.Message}");
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
                    return _exitResult;
                }
            }

            CommunicationsUtilities.Trace("Build finished.");
            return _exitResult;
        }

        /// <summary>
        /// Launches MSBuild server.
        /// </summary>
        /// <returns> Whether MSBuild server was started successfully.</returns>
        private bool LaunchMSBuildServer()
        {
            string serverLaunchMutexName = $@"Global\server-launch-{_pipeName}";
            using var serverLaunchMutex = ServerNamedMutex.OpenOrCreateMutex(serverLaunchMutexName, out bool mutexCreatedNew);
            if (!mutexCreatedNew)
            {
                // Some other client process launching a server and setting a build request for it. Fallback to usual msbuild app build.
                CommunicationsUtilities.Trace("Another process launching the msbuild server, falling back to former behavior.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                return false;
            }

            try
            {
                Process msbuildProcess = LaunchNode();
                CommunicationsUtilities.Trace("Server is launched.");
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Failed to launch the msbuild server: {ex.Message}");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.LaunchError;
                return false;
            }

            return true;
        }

        private void SendBuildCommand(string commandLine, NamedPipeClientStream nodeStream)
        {
            ServerNodeBuildCommand buildCommand = GetServerNodeBuildCommand(commandLine);
            WritePacket(_nodeStream, buildCommand);
            CommunicationsUtilities.Trace("Build command send...");
        }

        private ServerNodeBuildCommand GetServerNodeBuildCommand(string commandLine)
        {

            Dictionary<string, string> envVars = new Dictionary<string, string>();
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var key in environmentVariables.Keys)
            {
                envVars[(string)key] = (string) (environmentVariables[key] ?? "");
            }

            foreach (var pair in ServerEnvironmentVariables)
            {
                envVars[pair.Key] = pair.Value;
            }

            return new ServerNodeBuildCommand(
                        commandLine,
                        startupDirectory: Directory.GetCurrentDirectory(),
                        buildProcessEnvironment: envVars,
                        CultureInfo.CurrentCulture,
                        CultureInfo.CurrentUICulture);
        }

        private ServerNodeHandshake GetHandshake()
        {
            // params? lowprio?

            return new ServerNodeHandshake(
                CommunicationsUtilities.GetHandshakeOptions(taskHost: false, is64Bit: EnvironmentUtilities.Is64BitProcess),
                _msBuildLocation
            );
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ServerNodeConsoleWrite:
                    HandleServerNodeConsoleWrite((ServerNodeConsoleWrite)packet);
                    break;
                case NodePacketType.ServerNodeResponse:
                    HandleServerNodeResponse((ServerNodeResponse)packet);
                    break;
                default: throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
            }
        }

        private void HandleServerNodeConsoleWrite(ServerNodeConsoleWrite consoleWrite)
        {
            switch (consoleWrite.OutputType)
            {
                case 1:
                    Console.Write(consoleWrite.Text);
                    break;
                case 2:
                    Console.Error.Write(consoleWrite.Text);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected console output type {consoleWrite.OutputType}");
            }
        }

        private void HandleServerNodeResponse(ServerNodeResponse response)
        {
            CommunicationsUtilities.Trace($"Build response received: exit code {response.ExitCode}, exit type '{response.ExitType}'");
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Success;
            _exitResult.MSBuildAppExitTypeString = response.ExitType;
            _buildFinished = true;
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

        /*
                #region Packet pump
                /// <summary>
                /// Initializes the packet pump thread and the supporting events as well as the packet queue.
                /// </summary>
                private void InitializeAsyncPacketThread()
                {
                    lock (_asyncDataMonitor)
                    {
                        _packetPump = new Thread(PacketPumpProc);
                        _packetPump.IsBackground = true;
                        _packetPump.Name = "MSbuild Client Packet Pump";
                        _terminatePacketPump = new AutoResetEvent(false);
                        _packetPump.Start();
                    }
                }


                /// <summary>
                /// This method handles the asynchronous message pump.  It waits for messages to show up on the queue
                /// and calls FireDataAvailable for each such packet.  It will terminate when the terminate event is
                /// set.
                /// </summary>
                private void PacketPumpProc()
                {
                    AutoResetEvent localTerminatePacketPump = _terminatePacketPump;

                    RunReadLoop(_nodeStream, localTerminatePacketPump);

                    CommunicationsUtilities.Trace("Ending read loop");
                }


                private void RunReadLoop(Stream localPipe, AutoResetEvent localTerminatePacketPump)
                {
                    CommunicationsUtilities.Trace("Entering read loop.");
                    byte[] headerByte = new byte[5];
        #if FEATURE_APM
                    IAsyncResult result = localPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);
        #else
                    Task<int> readTask = CommunicationsUtilities.ReadAsync(localReadPipe, headerByte, headerByte.Length);
        #endif

                    do
                    {
                        // Ordering is important. 
                        WaitHandle[] handles = new WaitHandle[] {
        #if FEATURE_APM
                            result.AsyncWaitHandle,
        #else
                            ((IAsyncResult)readTask).AsyncWaitHandle,
        #endif
                            localTerminatePacketPump };

                        int waitId = WaitHandle.WaitAny(handles);
                        switch (waitId)
                        {
                            case 0:
                                {
                                    // Client recieved a packet header. Read the rest of a package.
                                    int bytesRead = 0;
                                    try
                                    {
        #if FEATURE_APM
                                        bytesRead = localPipe.EndRead(result);
        #else
                                        bytesRead = readTask.Result;
        #endif
                                    }
                                    catch (Exception e)
                                    {
                                        // Lost communications.  Abort (but allow node reuse)
                                        CommunicationsUtilities.Trace("Exception reading from server.  {0}", e);
                                        ExceptionHandling.DumpExceptionToFile(e);
                                        break;
                                    }

                                    if (bytesRead != headerByte.Length)
                                    {
                                        // Incomplete read.  Abort.
                                        if (bytesRead == 0)
                                        {
                                            CommunicationsUtilities.Trace("Parent disconnected abruptly");
                                        }
                                        else
                                        {
                                            CommunicationsUtilities.Trace("Incomplete header read from server.  {0} of {1} bytes read", bytesRead, headerByte.Length);
                                        }

                                        break;
                                    }

                                    NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), headerByte[0]);

                                    try
                                    {
                                        // lock (_receivedPackets)
                                        // {
                                        //     _receivedPackets.Enqueue(packet);
                                        //     _packetReceivedEvent.Set();
                                        // }
                                        // _packetFactory.DeserializeAndRoutePacket(0, packetType, BinaryTranslator.GetReadTranslator(localReadPipe, _sharedReadBuffer));
                                    }
                                    catch (Exception e)
                                    {
                                        // Error while deserializing or handling packet.  Abort.
                                        CommunicationsUtilities.Trace("Exception while deserializing packet {0}: {1}", packetType, e);
                                        ExceptionHandling.DumpExceptionToFile(e);
                                        break;
                                    }

        #if FEATURE_APM
                                    result = localPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);
        #else
                                    readTask = CommunicationsUtilities.ReadAsync(localReadPipe, headerByte, headerByte.Length);
        #endif
                                }

                                break;

                            case 1:
                                // Terminate a message pump.
                                throw new NotImplementedException();

                            default:
                                ErrorUtilities.ThrowInternalError("waitId {0} out of range.", waitId);
                                break;
                        }
                    }
                    while (true);
                }

                #endregion
        */

        // TODO: refactor communication.

        /// <summary>
        /// Connects to MSBuild server.
        /// </summary>
        /// <returns> Whether the client connected to MSBuild server successfully.</returns>
        private bool ConnectToServer(int timeout)
        {
            try
            {
                _nodeStream.Connect(timeout);

                int[] handshakeComponents = _handshake.RetrieveHandshakeComponents();
                for (int i = 0; i < handshakeComponents.Length; i++)
                {
                    CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], _pipeName);
                    _nodeStream.WriteIntForHandshake(handshakeComponents[i]);
                }

                // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
                _nodeStream.WriteIntForHandshake(ServerNodeHandshake.EndOfHandshakeSignal);

                CommunicationsUtilities.Trace("Reading handshake from pipe {0}", _pipeName);

#if NETCOREAPP2_1_OR_GREATER || MONO
                _nodeStream.ReadEndOfHandshakeSignal(false, 1000); 
#else
                _nodeStream.ReadEndOfHandshakeSignal(false);
#endif

                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", _pipeName);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Failed to conect to server: {ex.Message}");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return false;
            }

            return true;
        }

        private Process LaunchNode()
        {
            string[] msBuildServerOptions = new[] {
                _dllLocation,
                "/nologo",
                "/nodemode:8"
            }.ToArray();

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = _exeLocation,
                Arguments = string.Join(" ", msBuildServerOptions),
                UseShellExecute = false
            };

            foreach (var entry in ServerEnvironmentVariables)
            {
                processStartInfo.Environment[entry.Key] = entry.Value;
            };


            // Redirect the streams of worker nodes so that this 
            // parent doesn't wait on idle worker nodes to close streams
            // after the build is complete.
            processStartInfo.RedirectStandardInput = false;
            processStartInfo.RedirectStandardOutput = false;
            processStartInfo.RedirectStandardError = false;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;

            return Process.Start(processStartInfo) ?? throw new InvalidOperationException("MSBuild server node failed to lunch");
        }

        private INodePacket ReadPacket(NamedPipeClientStream nodeStream)
        {
            var headerBytes = new byte[5];
            var readBytes = nodeStream.Read(headerBytes, 0, 5);
            if (readBytes != 5)
                throw new InvalidOperationException("Not enough header bytes read from named pipe");

            NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), headerBytes[0]);

            try
            {
                _packetFactory.DeserializeAndRoutePacket(0, packetType, BinaryTranslator.GetReadTranslator(nodeStream, _sharedReadBuffer));
            }
            catch (Exception e)
            {
                // Error while deserializing or handling packet.  Abort.
                CommunicationsUtilities.Trace("Exception while deserializing packet {0}: {1}", packetType, e);
                ExceptionHandling.DumpExceptionToFile(e);
                throw;
            }
            
            if (_receivedPackets.TryDequeue(out INodePacket result))
            {
                return result;
            }
            else
            {
                throw new InvalidOperationException($"Internal error: packet queue is empty.");
            }
        }

        private void WritePacket(Stream nodeStream, INodePacket packet)
        {
            MemoryStream memoryStream = _packetMemoryStream;
            memoryStream.SetLength(0);

            ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(memoryStream);

            // Write header
            memoryStream.WriteByte((byte)packet.Type);

            // Pad for packet length
            _binaryWriter.Write(0);

            // Reset the position in the write buffer.
            packet.Translate(writeTranslator);

            int packetStreamLength = (int)memoryStream.Position;

            // Now write in the actual packet length
            memoryStream.Position = 1;
            _binaryWriter.Write(packetStreamLength - 5);

            nodeStream.Write(memoryStream.GetBuffer(), 0, packetStreamLength);
        }
    }
}
