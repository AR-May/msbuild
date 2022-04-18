// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Node;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.OutOfProcServerNode;

namespace Microsoft.Build.Experimental.Client
{
    /// <summary>
    /// This class implements client for MSBuild server. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    /// // TODO: Add argument/attribute saying that it is an experimental API
    public class MSBuildClient 
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

        /// <summary>
        /// A way to cache a byte array when writing out packets
        /// </summary>
        private MemoryStream _packetMemoryStream;

        /// <summary>
        /// A binary writer to help write into <see cref="_packetMemoryStream"/>
        /// </summary>
        private BinaryWriter _binaryWriter;

        /// <summary>
        /// Cancel when handling Ctrl-C
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        #endregion

        // TODO: work on eleminating extra parameters or making them more clearly described at least.
        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        /// <param name="msbuildLocation">Location of msbuild dll or exe.</param>
        /// <param name="exeLocation">Location of executable file to launch the server process.
        /// That should be either dotnet.exe or MSBuild.exe location.</param>
        /// <param name="dllLocation">Location of dll file to launch the server process if needed.
        /// Empty if executable is msbuild.exe and not empty if dotnet.exe.</param>
        public MSBuildClient(string msbuildLocation, string exeLocation, string dllLocation)
        {
            ServerEnvironmentVariables = new();
            _exitResult = new();

            // dll & exe locations
            _msBuildLocation = msbuildLocation;
            _exeLocation = exeLocation;
            _dllLocation = dllLocation;

            // Client <-> Server communication stream
            _handshake = GetHandshake();
            _pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuildServer-" + _handshake.ComputeHash());
            _nodeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
            );

            _packetMemoryStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetMemoryStream);
        }

        /// <summary>
        /// Orchestrates the execution of the build on the server,
        /// responsible for client-server communication.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <returns>A value of type <see cref="MSBuildClientExitResult"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public MSBuildClientExitResult Execute(string commandLine)
        {
            string serverRunningMutexName = $@"Global\server-running-{_pipeName}";
            string serverBusyMutexName = $@"Global\server-busy-{_pipeName}";

            // Start server it if is not running.
            bool serverWasAlreadyRunning = ServerNamedMutex.WasOpen(serverRunningMutexName);
            if (!serverWasAlreadyRunning && !TryLaunchServer())
            {
                return _exitResult;
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
            if (!TryConnectToServer(serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000))
            {
                CommunicationsUtilities.Trace("Failure to connect to a server.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return _exitResult;
            }


            // Build in client.

            // Add cancellation handler function.
            ConsoleCancelEventHandler cancelHandler = Console_CancelKeyPress;
            Console.CancelKeyPress += cancelHandler;

            // Send build command.
            // Let's send it outside the packet pump so that we easier and quicklier deal with possible issues with connection to server.
            SendBuildCommand(commandLine, _nodeStream);

            MSBuildClientPacketPump packetPump = new MSBuildClientPacketPump(_nodeStream);
            (packetPump as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeConsoleWrite, ServerNodeConsoleWrite.FactoryForDeserialization, packetPump);
            (packetPump as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeBuildResult, ServerNodeBuildResult.FactoryForDeserialization, packetPump);
            packetPump.Start();

            var waitHandles = new WaitHandle[] {
                _cts.Token.WaitHandle,
                packetPump.PacketPumpErrorEvent,
                packetPump.PacketReceivedEvent };

            // Get the current directory before doing any work. We need this so we can restore the directory when the node shutsdown.
            while (!_buildFinished)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        HandleCancellation();
                        break;

                    case 1:
                        HandlePacketPumpError(packetPump);
                        break;

                    case 2:
                        while (packetPump.ReceivedPacketsQueue.TryDequeue(out INodePacket? packet)
                            && (!_buildFinished)
                            && (!_cts.Token.IsCancellationRequested))
                        {
                            if (packet != null)
                            {
                                try
                                {
                                    HandlePacket(packet);
                                }
                                catch (Exception ex)
                                {
                                    CommunicationsUtilities.Trace($"MSBuild client error: problem during packet handling occured: {0}.", ex);
                                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
                                    _buildFinished = true;
                                    break;
                                }
                            }
                        }

                        break;
                }
            }

            // Stop packet pump.
            packetPump.Stop();
            Console.CancelKeyPress -= cancelHandler;

            CommunicationsUtilities.Trace("Build finished.");
            return _exitResult;
        }

        /// <summary>
        /// Handler for when CTRL-C or CTRL-BREAK is called.
        /// </summary>
        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Cancelling...");

            // Send cancellation command to server.
            // SendCancelCommand(_nodeStream);

            _cts.Cancel();
        }

        private void SendCancelCommand(NamedPipeClientStream nodeStream) => throw new NotImplementedException();

        /// <summary>
        /// Launches MSBuild server. 
        /// </summary>
        /// <returns> Whether MSBuild server was started successfully.</returns>
        private bool TryLaunchServer()
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

            string[] msBuildServerOptions = new string[] {
                _dllLocation,
                "/nologo",
                "/nodemode:8"
            };

            try
            {
                Process msbuildProcess = LaunchNode(_exeLocation, string.Join(" ", msBuildServerOptions),  ServerEnvironmentVariables);
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

        private Process LaunchNode(string exeLocation, string msBuildServerArguments, Dictionary<string, string> serverEnvironmentVariables)
        { 
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = exeLocation,
                Arguments = msBuildServerArguments,
                UseShellExecute = false
            };

            // TODO: do we really need to start msbuild server with these variables, performance-wise and theoretically thinking?
            // We are sending them in build command as well.
            foreach (var entry in serverEnvironmentVariables)
            {
                processStartInfo.Environment[entry.Key] = entry.Value;
            }

            // We remove env MSBUILDRUNSERVERCLIENT that might be equal to 1, so we do not get an infinite recursion here. 
            processStartInfo.Environment["MSBUILDRUNSERVERCLIENT"] = "0";

            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;

            return Process.Start(processStartInfo) ?? throw new InvalidOperationException("MSBuild server node failed to lunch");
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

            // We remove env MSBUILDRUNSERVERCLIENT that might be equal to 1, so we do not get an infinite recursion here. 
            envVars["MSBUILDRUNSERVERCLIENT"] = "0";

            return new ServerNodeBuildCommand(
                        commandLine,
                        startupDirectory: Directory.GetCurrentDirectory(),
                        buildProcessEnvironment: envVars,
                        CultureInfo.CurrentCulture,
                        CultureInfo.CurrentUICulture);
        }

        private ServerNodeHandshake GetHandshake()
        {
            return new ServerNodeHandshake(
                CommunicationsUtilities.GetHandshakeOptions(taskHost: false, is64Bit: EnvironmentUtilities.Is64BitProcess),
                _msBuildLocation
            );
        }

        /// <summary>
        /// Handle cancellation.
        /// </summary>
        private void HandleCancellation()
        {
            Console.WriteLine("MSBuild client cancelled.");
            CommunicationsUtilities.Trace("MSBuild client cancelled.");
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Cancelled;
            _buildFinished = true;
        }

        /// <summary>
        /// Handle packet pump error.
        /// </summary>
        private void HandlePacketPumpError(MSBuildClientPacketPump packetPump)
        {
            CommunicationsUtilities.Trace("MSBuild client error: packet pump unexpectedly shutted down: {0}", packetPump.PacketPumpException);
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
            _buildFinished = true;
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
                case NodePacketType.ServerNodeBuildResult:
                    HandleServerNodeBuildResult((ServerNodeBuildResult)packet);
                    break;
                default: throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
            }
        }

        private void HandleServerNodeConsoleWrite(ServerNodeConsoleWrite consoleWrite)
        {
            switch (consoleWrite.OutputType)
            {
                case ConsoleOutput.Standard:
                    Console.Write(consoleWrite.Text);
                    break;
                case ConsoleOutput.Error:
                    Console.Error.Write(consoleWrite.Text);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected console output type {consoleWrite.OutputType}");
            }
        }

        private void HandleServerNodeBuildResult(ServerNodeBuildResult response)
        {
            CommunicationsUtilities.Trace($"Build response received: exit code {response.ExitCode}, exit type '{response.ExitType}'");
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Success;
            _exitResult.MSBuildAppExitTypeString = response.ExitType;
            _buildFinished = true;
        }


        /// <summary>
        /// Connects to MSBuild server.
        /// </summary>
        /// <returns> Whether the client connected to MSBuild server successfully.</returns>
        private bool TryConnectToServer(int timeout)
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
