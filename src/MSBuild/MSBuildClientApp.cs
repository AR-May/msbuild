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
            string[] commandLineArr
#endif
            )
        {
            // TODO: remove
            // Debugger.Launch();

            bool runMsbuildInServer = Environment.GetEnvironmentVariable("RUN_MSBUILD_IN_SERVER") == "1";
            if (!runMsbuildInServer)
            {
                // Fallback to old behavior.
                return MSBuildApp.Execute(commandLine);
            }

            MSBuildClient msbuildClient = new MSBuildClient();
#if !FEATURE_GET_COMMANDLINE
            string commandLine = string.Join(" ", commandLineArr); // TODO: maybe msbuildLocation would be needed here. 
#endif
            MSBuildClient.ExitResult exitResult = msbuildClient.Execute(commandLine);

            if (exitResult.MSBuildClientExitType == MSBuildClient.ExitType.ServerBusy)
            {
                // Fallback to old behavior.
                return MSBuildApp.Execute(commandLine);
            }
            else if ((exitResult.MSBuildClientExitType == MSBuildClient.ExitType.Success)
                    && Enum.TryParse(exitResult.MSBuildAppExitTypeString, out MSBuildApp.ExitType MSBuildAppExitType))
            {
                return MSBuildAppExitType;
            }

            return MSBuildApp.ExitType.Unexpected;
        }
    }
}
