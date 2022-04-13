using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Experimental.Client;

#if RUNTIME_TYPE_NETCORE || MONO
using System.IO;
using System.Diagnostics;
#endif

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class implements client for MSBuild server. It
    /// 1. starts the MSBuild server in a separate process if it does not yet exist.
    /// 2. establishes a connection with MSBuild server and sends a build request.
    /// 3. if server is busy, it falls back to old build behavior.
    /// </summary>
    public static class MSBuildClientApp
    {
        /// <summary>
        /// This is the entry point for the MSBuild client.
        /// </summary>
        /// <remark>
        /// The locations of msbuild exe/dll and dotnet.exe are automatically detected.
        /// </remark>
        /// <returns>0 on success, 1 on failure</returns>
        public static int Run(
#if !FEATURE_GET_COMMANDLINE
            string[] args
#endif
            )
        {
            string msBuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            string dllLocation = string.Empty;
            string exeLocation = string.Empty;

            // TODO: check the detection.

#if RUNTIME_TYPE_NETCORE || MONO
            // Run the child process with the same host as the currently-running process.
            // Mono automagically uses the current mono, to execute a managed assembly.
            if (!NativeMethodsShared.IsMono)
            {
                // _exeFileLocation consists the msbuild dll instead.
                dllLocation = msBuildLocation;
                exeLocation = GetCurrentHost();
            }
            else
            {
                // _exeFileLocation consists the msbuild dll instead.
                exeLocation = msBuildLocation;
            }
#else
            exeLocation = msBuildLocation;
#endif

            int exitCode = (Execute(
#if FEATURE_GET_COMMANDLINE
                Environment.CommandLine,
#else
                ConstructArrayArg(args),
#endif
                msBuildLocation,
                exeLocation,
                dllLocation
            ) == MSBuildApp.ExitType.Success) ? 0 : 1;
            return exitCode;
        }

        /// <summary>
        /// This is the entry point for the MSBuild client.
        /// </summary>
        /// <returns>0 on success, 1 on failure</returns>
        public static int Run(
#if !FEATURE_GET_COMMANDLINE
            string[] args,
#endif
            string msbuildLocation,
            string exeLocation,
            string dllLocation
            )
        {
            int exitCode = (Execute(
#if FEATURE_GET_COMMANDLINE
                Environment.CommandLine,
#else
                ConstructArrayArg(args),
#endif
                msbuildLocation,
                exeLocation,
                dllLocation
            ) == MSBuildApp.ExitType.Success) ? 0 : 1;
            return exitCode;
        }

        /// <summary>
        /// Orchestrates the execution of the msbuild client, and is also responsible
        /// for escape hatches and fallbacks.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <returns>A value of type MSBuildApp.ExitType that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public static MSBuildApp.ExitType Execute(
#if FEATURE_GET_COMMANDLINE
            string commandLine,
#else
            string[] commandLine,
#endif
            string msbuildLocation,
            string exeLocation,
            string dllLocation
            )
        {
            // Escape hatch to an old behavior.
            bool runMsbuildInServer = Environment.GetEnvironmentVariable("RUN_MSBUILD_IN_SERVER") == "1";
            if (!runMsbuildInServer)
            {
                return MSBuildApp.Execute(commandLine);
            }

            // MSBuild client orchestration.
#if !FEATURE_GET_COMMANDLINE
            string commandLineString = string.Join(" ", commandLine); 
#else
            string commandLineString = commandLine;
#endif
            MSBuildClient msbuildClient = new MSBuildClient(msbuildLocation, exeLocation, dllLocation); 
            MSBuildClientExitResult exitResult = msbuildClient.Execute(commandLineString);

            if (exitResult.MSBuildClientExitType == MSBuildClientExitType.ServerBusy
                || exitResult.MSBuildClientExitType == MSBuildClientExitType.ConnectionError
            )
            {
                // TODO: debug, remove it.
                throw new Exception("NOT CONNECTED TO SERVER.");

                // Server is busy, fallback to old behavior.
                // return MSBuildApp.Execute(commandLine);
            }
            else if ((exitResult.MSBuildClientExitType == MSBuildClientExitType.Success)
                    && Enum.TryParse(exitResult.MSBuildAppExitTypeString, out MSBuildApp.ExitType MSBuildAppExitType))
            {
                // The client successfully set up a build task for MSBuild server and recieved the result.
                // (Which could be a failure as well). Return the recieved exit type. 
                return MSBuildAppExitType;
            }

            return MSBuildApp.ExitType.MSBuildClientFailure;
        }

        /// <summary>
        /// Insert the command executable path as the first element of the args array.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static string[] ConstructArrayArg(string[] args)
        {
            string[] newArgArray = new string[args.Length + 1];

            newArgArray[0] = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            Array.Copy(args, 0, newArgArray, 1, args.Length);

            return newArgArray;
        }


        // Copied from NodeProviderOutOfProc. TODO: Refactor this?
#if RUNTIME_TYPE_NETCORE || MONO
        private static string? CurrentHost;
        private static string GetCurrentHost()
        {
            if (CurrentHost == null)
            {
                string dotnetExe = Path.Combine(FileUtilities.GetFolderAbove(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, 2),
                    NativeMethodsShared.IsWindows ? "dotnet.exe" : "dotnet");
                if (File.Exists(dotnetExe))
                {
                    CurrentHost = dotnetExe;
                }
                else
                {
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        CurrentHost = currentProcess.MainModule?.FileName ?? throw new InvalidOperationException("Failed to retrieve process executable.");
                    }
                }
            }

            return CurrentHost;
        }
#endif
    }
}
