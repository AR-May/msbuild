using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.OutOfProcServerNode;

namespace Microsoft.Build.Client
{
    /// <summary>
    /// Enumeration of the various ways in which the MSBuildClient.exe application can exit.
    /// </summary>
    public class MSBuildClientExitResult
    {
        /// <summary>
        /// The MSBuild client .
        /// </summary>
        public ClientExitType MSBuildClientExitType { get; set; }
        public string? MSBuildAppExitTypeString { get; set; }
        public string? MSbuildServerErrorMsg; // TODO remove it, just log.

        public MSBuildClientExitResult(ClientExitType MSBuildClientExitType, string MSBuildAppExitTypeString)
        {
            this.MSBuildClientExitType = MSBuildClientExitType;
            this.MSBuildAppExitTypeString = MSBuildAppExitTypeString;
        }

        public MSBuildClientExitResult(ClientExitType MSBuildClientExitType, string MSBuildAppExitTypeString, string ErrorMsg)
        {
            this.MSBuildClientExitType = MSBuildClientExitType;
            this.MSBuildAppExitTypeString = MSBuildAppExitTypeString;
            this.MSbuildServerErrorMsg = ErrorMsg;
        }

        public MSBuildClientExitResult()
        {
        }
    }

    public enum ClientExitType
    {
        /// <summary>
        /// The MSBuild client successfully processed the build request.
        /// </summary>
        Success,
        /// <summary>
        /// The build stopped unexpectedly, for example,
        /// because a namedpipe was unexpectedly closed.
        /// </summary>
        Unexpected,
        /// <summary>
        /// Server is busy. This should cause fallback to MSBuildApp execution.
        /// </summary>
        ServerBusy,
        /// <summary>
        /// Client was shutted down.
        /// </summary>
        Shutdown
    }

    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    /// // TODO: argument/attribute saying that it is an experimental API
    public class MSBuildClient
    {
        private MSBuildClientExitResult _exitResult;

        private string _msBuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

        public MSBuildClient()
        {
            _exitResult = new();
        }

        public MSBuildClientExitResult Execute(string commandLine)
        {
            // string msBuildLocation = @"C:\Users\alinama\work\MSBUILD\msbuild-1\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\amd64\MSBuild.exe";

            string[] msBuildServerOptions = new[] {
                "/nologo",
                "/nodemode:8",
                "/nodeReuse:true"
            }.ToArray();

            ProcessStartInfo msBuildServerStartInfo = GetMSBuildServerProcessStartInfo(_msBuildLocation, msBuildServerOptions, new Dictionary<string, string>());

            var handshake = new ServerNodeHandshake(
                CommunicationsUtilities.GetHandshakeOptions(taskHost: false, nodeReuse: false, lowPriority: false, is64Bit: EnvironmentUtilities.Is64BitProcess),
                _msBuildLocation);

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
                CommunicationsUtilities.Trace("Server is busy, falling back to former behavior.");
                _exitResult.MSBuildClientExitType = ClientExitType.ServerBusy;
                return _exitResult;
            }

            // connect to it
            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
            );

            nodeStream.Connect(serverWasAlreadyRunning && !serverWasBusy ? 1_000 : 20_000);

            int[] handshakeComponents = handshake.RetrieveHandshakeComponents();
            for (int i = 0; i < handshakeComponents.Length; i++)
            {
                CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], pipeName);
                WriteIntForHandshake(nodeStream, handshakeComponents[i]);
            }

            // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
            WriteIntForHandshake(nodeStream, ServerNodeHandshake.EndOfHandshakeSignal);

            CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);

            ReadEndOfHandshakeSignal(nodeStream, timeout: 1000);

            CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);

            Dictionary<string, string> envVars = new Dictionary<string, string>();
            var vars = Environment.GetEnvironmentVariables();
            foreach (var key in vars.Keys)
            {
                envVars[(string)key] = (string)vars[key];
            }

            foreach (var pair in msBuildServerStartInfo.Environment)
            {
                envVars[pair.Key] = pair.Value;
            }

            var buildCommand = new ServerNodeBuildCommand(
                commandLine,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: envVars,
                CultureInfo.CurrentCulture,
                CultureInfo.CurrentUICulture);

            WritePacket(nodeStream, buildCommand);

            CommunicationsUtilities.Trace("Build command send...");

            while (true)
            {
                var packet = ReadPacket(nodeStream);
                try
                {
                    HandlePacket(packet);
                }
                catch (Exception ex)
                {
                    _exitResult.MSBuildClientExitType = ClientExitType.Unexpected;
                    CommunicationsUtilities.Trace(ex.Message);
                    return _exitResult;
                }
            }
        }



        private INodePacket ReadPacket(NamedPipeClientStream nodeStream)
        {
            throw new NotImplementedException();
        }

        private void WritePacket(NamedPipeClientStream nodeStream, INodePacket packet)
        {
            throw new NotImplementedException();
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
                default: throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
            }
        }

        private void HandleServerNodeConsole(ServerNodeConsoleWrite consoleWrite)
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
            int exitCode = response.ExitCode;
            _exitResult = new MSBuildClientExitResult(ClientExitType.Success, response.ExitType);
        }

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
    }
}
