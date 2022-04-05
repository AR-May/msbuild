using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.OutOfProcServerNode;
#nullable enable 

namespace Microsoft.Build.Experimental.Client
{
    /// <summary>
    /// Enumeration of the various ways in which the MSBuildClient execution can exit.
    /// </summary>
    public class MSBuildClientExitResult
    {
        /// <summary>
        /// The MSBuild client exit type.
        /// Covers different ways MSBuild client execution can finish.
        /// Build errors are not included. The client could finish successfully and the build at the same time could result in a build error.
        /// </summary>
        public ClientExitType MSBuildClientExitType { get; set; }

        /// <summary>
        /// The build exit type. Possible values: MSBuildApp.ExitType serialized into a string.
        /// This field is null if MSBuild client execution was not successful.
        /// </summary>
        public string? MSBuildAppExitTypeString { get; set; }

        public MSBuildClientExitResult() { }
    }

    public enum ClientExitType
    {
        /// <summary>
        /// The MSBuild client successfully processed the build request.
        /// </summary>
        Success,
        /// <summary>
        /// Server is busy. This return value should cause fallback to old MSBuildApp execution.
        /// </summary>
        ServerBusy,
        /// <summary>
        /// Client was shutted down.
        /// </summary>
        Shutdown,
        /// <summary>
        /// Client was unable to connect to the server.
        /// </summary>
        ConnectionError,
        /// <summary>
        /// Client was unable to launch to the server.
        /// </summary>
        LaunchError,
        /// <summary>
        /// The build stopped unexpectedly, for example,
        /// because a named pipe between the server and the client was unexpectedly closed.
        /// </summary>
        Unexpected
    }

    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
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
            _pipeName = GetPipeNameOrPath("MSBuildServer-" + _handshake.ComputeHash());
            _nodeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
            );
        }

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
            _handshake = handshake;
            _pipeName = pipeName;
            _nodeStream = nodeStream;
            _msBuildLocation = msbuildLocation;
            _exeLocation = exeLocation;
            _dllLocation = dllLocation;

            ServerEnvironmentVariables = new();
            _exitResult = new();
        }

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
                _exitResult.MSBuildClientExitType = ClientExitType.ServerBusy;
                return _exitResult;
            }

            // Connect to server.
            if (!ConnectToServer(serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000))
            {
                return _exitResult;
            }

            // Send build command.
            SendBuildCommand(commandLine, _nodeStream);

            // Read server responses.
            _buildFinished = false;
            while (!_buildFinished)
            {
                var packet = ReadPacket(_nodeStream);
                try
                {
                    HandlePacket(packet);
                }
                catch (Exception ex)
                {
                    CommunicationsUtilities.Trace($"HandlePacket error: {ex.Message}");
                    _exitResult.MSBuildClientExitType = ClientExitType.Unexpected;
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
                _exitResult.MSBuildClientExitType = ClientExitType.ServerBusy;
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
                _exitResult.MSBuildClientExitType = ClientExitType.LaunchError;
                return false;
            }

            return true;
        }

        private void SendBuildCommand(string commandLine, NamedPipeClientStream nodeStream)
        {
            ServerNodeBuildCommand buildCommand = GetServerNodeBuildCommand(commandLine);
            WritePacket(_nodeStream, buildCommand);
        }

        private ServerNodeBuildCommand GetServerNodeBuildCommand(string commandLine)
        {

            Dictionary<string, string> envVars = new Dictionary<string, string>();
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var key in environmentVariables.Keys)
            {
                envVars[(string)key] = (string)environmentVariables[key];
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
            _exitResult.MSBuildClientExitType = ClientExitType.Success;
            _exitResult.MSBuildAppExitTypeString = response.ExitType;
            _buildFinished = true;
        }

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
                    WriteIntForHandshake(_nodeStream, handshakeComponents[i]);
                }

                // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
                WriteIntForHandshake(_nodeStream, ServerNodeHandshake.EndOfHandshakeSignal);

                CommunicationsUtilities.Trace("Reading handshake from pipe {0}", _pipeName);

                ReadEndOfHandshakeSignal(_nodeStream, timeout: 1000);

                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", _pipeName);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Failed to conect to server: {ex.Message}");
                _exitResult.MSBuildClientExitType = ClientExitType.ConnectionError;
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

        private static INodePacket ReadPacket(NamedPipeClientStream nodeStream)
        {
            var headerBytes = new byte[5];
            var readBytes = nodeStream.Read(headerBytes, 0, 5);
            if (readBytes != 5)
                throw new InvalidOperationException("Not enough header bytes read from named pipe");
            byte packetType = headerBytes[0];
            int bodyLen = (headerBytes[1] << 00) |
                          (headerBytes[2] << 08) |
                          (headerBytes[3] << 16) |
                          (headerBytes[4] << 24);
            var bodyBytes = new byte[bodyLen];
            readBytes = nodeStream.Read(bodyBytes, 0, bodyLen);
            if (readBytes != bodyLen)
                throw new InvalidOperationException($"Not enough bytes read to read body: expected {bodyLen}, read {readBytes}");

            var ms = new MemoryStream(bodyBytes);
            switch (headerBytes[0])
            {
                case (byte)NodePacketType.ServerNodeResponse:
                    return DeserializeFromStreamServerNodeResponse(ms);
                case (byte)NodePacketType.ServerNodeConsoleWrite:
                    return DeserializeFromStreamServerNodeConsoleWrite(ms);
            }

            throw new InvalidOperationException($"Unexpected packet type {headerBytes[0]:X}");
        }

        private static ServerNodeResponse DeserializeFromStreamServerNodeResponse(Stream inputStream)
        {
            using var br = new BinaryReader(inputStream);

            int exitCode = br.ReadInt32();
            string exitType = br.ReadString();

            ServerNodeResponse response = new ServerNodeResponse(exitCode, exitType);
            return response;
        }

        private static ServerNodeConsoleWrite DeserializeFromStreamServerNodeConsoleWrite(Stream inputStream)
        {
            using var br = new BinaryReader(inputStream);

            string text = br.ReadString();
            byte outputType = br.ReadByte();

            ServerNodeConsoleWrite consoleWrite = new ServerNodeConsoleWrite(text, outputType);
            return consoleWrite;
        }

        private void WritePacket(Stream nodeStream, ServerNodeBuildCommand buildCommand)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // header
            bw.Write((byte)NodePacketType.ServerNodeBuildCommand);
            bw.Write((int)0);
            int headerSize = (int)ms.Position;

            // body
            bw.Write(buildCommand.CommandLine);
            bw.Write(buildCommand.StartupDirectory);
            bw.Write(buildCommand.BuildProcessEnvironment.Count);
            foreach (var pair in buildCommand.BuildProcessEnvironment)
            {
                bw.Write(pair.Key);
                bw.Write(pair.Value);
            }
            bw.Write(buildCommand.Culture.Name);
            bw.Write(buildCommand.UICulture.Name);

            int bodySize = (int)ms.Position - headerSize;

            ms.Position = 1;
            ms.WriteByte((byte)bodySize);
            ms.WriteByte((byte)(bodySize >> 8));
            ms.WriteByte((byte)(bodySize >> 16));
            ms.WriteByte((byte)(bodySize >> 24));

            // copy packet message bytes into stream
            var bytes = ms.GetBuffer();
            nodeStream.Write(bytes, 0, headerSize + bodySize);

            CommunicationsUtilities.Trace("Build command send...");
        }
    }
}
