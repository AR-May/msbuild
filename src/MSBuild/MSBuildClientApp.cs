using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using static Microsoft.Build.CommandLine.MSBuildApp;

namespace Microsoft.Build.Client
{
    // TODO: should it be static? consider that.
    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    public static class MSBuildClientApp
    {
        public static ExitType Execute(
#if FEATURE_GET_COMMANDLINE
            string commandLine
#else
            string[] commandLineArr
#endif
            )
        {
            // TODO: remove
            // Debugger.Launch();

            // Fallback to old behavior.
            bool runMsbuildInServer = Environment.GetEnvironmentVariable("RUN_MSBUILD_IN_SERVER") == "1";
            if (!runMsbuildInServer)
            {
                return MSBuildApp.Execute(commandLine);
            }

            ExitType exitType = ExitType.Success;
#if !FEATURE_GET_COMMANDLINE
            string commandLine = string.Join(" ", commandLineArr); // TODO: maybe msbuildLocation would be needed here. 
#endif
            MSBuildClient msbuildClient = new MSBuildClient();
            int exitCode = msbuildClient.Execute(commandLine);

            if (exitCode != 0)
            {
                exitType = ExitType.BuildError;
            }

            return exitType;
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
    }
}
