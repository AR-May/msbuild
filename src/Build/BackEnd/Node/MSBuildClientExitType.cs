#nullable enable 

namespace Microsoft.Build.Experimental.Client
{
    public enum MSBuildClientExitType
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
}
