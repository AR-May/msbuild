using System;
using Microsoft.Build.CommandLine;

namespace Microsoft.Build.Client
{
    /// <summary>
    /// This class implements the MSBuildClient.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    public static class MSBuildClientApp
    {
        public static MSBuildApp.ExitType Execute(
#if FEATURE_GET_COMMANDLINE
            string commandLine
#else
            string[] commandLine
#endif
            )
        {
            // TODO: remove
            // Debugger.Launch();

            bool runMsbuildInServer = Environment.GetEnvironmentVariable("RUN_MSBUILD_IN_SERVER") == "1";
            if (!runMsbuildInServer)
            {
                // Escape hatch to an old behavior.
                return MSBuildApp.Execute(commandLine);
            }

            // TODO:? process switches "lowpriority".

#if !FEATURE_GET_COMMANDLINE
            string commandLineString = string.Join(" ", commandLine); // TODO: maybe msbuildLocation would be needed here.
#else
            string commandLineString = commandLine;
#endif
            MSBuildClient msbuildClient = new MSBuildClient();
            MSBuildClientExitResult exitResult = msbuildClient.Execute(commandLineString);

            if (exitResult.MSBuildClientExitType == ClientExitType.ServerBusy)
            {
                // Server is busy, fall back to old behavior.
                return MSBuildApp.Execute(commandLine);
            }
            else if ((exitResult.MSBuildClientExitType == ClientExitType.Success)
                    && Enum.TryParse(exitResult.MSBuildAppExitTypeString, out MSBuildApp.ExitType MSBuildAppExitType))
            {
                return MSBuildAppExitType;
            }

            return MSBuildApp.ExitType.Unexpected;
        }
    }
}
