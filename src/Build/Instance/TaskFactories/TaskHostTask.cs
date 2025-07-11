// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
#if FEATURE_REPORTFILEACCESSES
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.FileAccesses;
#endif

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The wrapper task for tasks that wish to take advantage of the
    /// task host factory feature.  Generated by AssemblyTaskFactory
    /// when it wants to run the loaded task in the task host.
    /// </summary>
    internal class TaskHostTask : IGeneratedTask, ICancelableTask, INodePacketFactory, INodePacketHandler
    {
        /// <summary>
        /// The IBuildEngine callback object.
        /// </summary>
        private IBuildEngine _buildEngine;

        /// <summary>
        /// The host object that can be passed to this task.
        /// </summary>
        private ITaskHost _hostObject;

        /// <summary>
        /// Logging context for logging errors / issues
        /// encountered in the TaskHostTask itself.
        /// </summary>
        private TaskLoggingContext _taskLoggingContext;

        /// <summary>
        /// Location of the task in the project file.
        /// </summary>
        private IElementLocation _taskLocation;

        /// <summary>
        ///  The provider for the task host nodes.
        /// </summary>
        private IBuildComponentHost _buildComponentHost;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private NodePacketFactory _packetFactory;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private AutoResetEvent _packetReceivedEvent;

        /// <summary>
        /// The packet that is the end result of the task host task execution process
        /// </summary>
        private ConcurrentQueue<INodePacket> _receivedPackets;

        /// <summary>
        /// The set of parameters used to decide which host to launch.
        /// </summary>
        private IDictionary<string, string> _taskHostParameters;

        /// <summary>
        /// The type of the task that we are wrapping.
        /// </summary>
        private LoadedType _taskType;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// The AppDomainSetup we'll want to apply to the AppDomain that we may
        /// want to load the OOP task into.
        /// </summary>
        private AppDomainSetup _appDomainSetup;
#endif

        /// <summary>
        /// The task host context of the task host we're launching -- used to
        /// communicate with the task host.
        /// </summary>
        private HandshakeOptions _requiredContext = HandshakeOptions.None;

        /// <summary>
        /// True if currently connected to the task host; false otherwise.
        /// </summary>
        private bool _connectedToTaskHost = false;

        /// <summary>
        /// The provider for task host nodes.
        /// </summary>
        private NodeProviderOutOfProcTaskHost _taskHostProvider;

        /// <summary>
        /// Lock object to serialize access to the task host.
        /// </summary>
        private Object _taskHostLock;

        /// <summary>
        /// Keeps track of whether the wrapped task has had cancel called against it.
        /// </summary>
        private bool _taskCancelled;

        /// <summary>
        /// The set of parameters that has been set to this wrapped task -- save them
        /// here so that we can forward them on to the task host.
        /// </summary>
        private IDictionary<string, object> _setParameters;

        /// <summary>
        /// Did the task succeed?
        /// </summary>
        private bool _taskExecutionSucceeded = false;

        /// <summary>
        /// Constructor
        /// </summary>
        ///
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        public TaskHostTask(
            IElementLocation taskLocation,
            TaskLoggingContext taskLoggingContext,
            IBuildComponentHost buildComponentHost,
            IDictionary<string, string> taskHostParameters,
            LoadedType taskType
#if FEATURE_APPDOMAIN
                , AppDomainSetup appDomainSetup
#endif
            )
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        {
            ErrorUtilities.VerifyThrowInternalNull(taskType);

            _taskLocation = taskLocation;
            _taskLoggingContext = taskLoggingContext;
            _buildComponentHost = buildComponentHost;
            _taskType = taskType;
#if FEATURE_APPDOMAIN
            _appDomainSetup = appDomainSetup;
#endif
            _taskHostParameters = taskHostParameters;

            _packetFactory = new NodePacketFactory();

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.LogMessage, LogMessagePacket.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.TaskHostTaskComplete, TaskHostTaskComplete.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, this);

            _packetReceivedEvent = new AutoResetEvent(false);
            _receivedPackets = new ConcurrentQueue<INodePacket>();
            _taskHostLock = new Object();

            _setParameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// THe IBuildEngine callback object
        /// </summary>
        public IBuildEngine BuildEngine
        {
            get
            {
                return _buildEngine;
            }

            set
            {
                _buildEngine = value;
            }
        }

        /// <summary>
        /// The host object that can be passed to this task.
        /// </summary>
        public ITaskHost HostObject
        {
            get
            {
                return _hostObject;
            }

            set
            {
                _hostObject = value;
            }
        }

        /// <summary>
        /// Sets the requested task parameter to the requested value.
        /// </summary>
        public void SetPropertyValue(TaskPropertyInfo property, object value)
        {
            _setParameters[property.Name] = value;
        }

        /// <summary>
        /// Returns the value of the requested task parameter
        /// </summary>
        public object GetPropertyValue(TaskPropertyInfo property)
        {
            if (_setParameters.TryGetValue(property.Name, out object value))
            {
                // If we returned an exception, then we want to throw it when we
                // do the get.
                if (value is Exception ex)
                {
                    throw ex;
                }

                return value;
            }
            else
            {
                PropertyInfo parameter = _taskType.Type.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public);
                return parameter.GetValue(this, null);
            }
        }

        /// <summary>
        /// Cancels the currently executing task
        /// </summary>
        public void Cancel()
        {
            if (!_taskCancelled)
            {
                lock (_taskHostLock)
                {
                    if (_taskHostProvider != null && _connectedToTaskHost)
                    {
                        _taskHostProvider.SendData(_requiredContext, new TaskHostTaskCancelled());
                    }
                }

                _taskCancelled = true;
            }
        }

        /// <summary>
        /// Executes the task.
        /// </summary>
        public bool Execute()
        {
            // log that we are about to spawn the task host
            string runtime = _taskHostParameters[XMakeAttributes.runtime];
            string architecture = _taskHostParameters[XMakeAttributes.architecture];
            _taskLoggingContext.LogComment(MessageImportance.Low, "ExecutingTaskInTaskHost", _taskType.Type.Name, _taskType.Assembly.AssemblyLocation, runtime, architecture);

            // set up the node
            lock (_taskHostLock)
            {
                _taskHostProvider = (NodeProviderOutOfProcTaskHost)_buildComponentHost.GetComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
                ErrorUtilities.VerifyThrowInternalNull(_taskHostProvider, "taskHostProvider");
            }

            TaskHostConfiguration hostConfiguration =
                new TaskHostConfiguration(
                        runtime,
                        _buildComponentHost.BuildParameters.NodeId,
                        NativeMethodsShared.GetCurrentDirectory(),
                        CommunicationsUtilities.GetEnvironmentVariables(),
                        _buildComponentHost.BuildParameters.Culture,
                        _buildComponentHost.BuildParameters.UICulture,
#if FEATURE_APPDOMAIN
                        _appDomainSetup,
#endif
                        BuildEngine.LineNumberOfTaskNode,
                        BuildEngine.ColumnNumberOfTaskNode,
                        BuildEngine.ProjectFileOfTaskNode,
                        BuildEngine.ContinueOnError,
                        _taskType.Type.FullName,
                        AssemblyUtilities.GetAssemblyLocation(_taskType.Type.GetTypeInfo().Assembly),
                        _buildComponentHost.BuildParameters.LogTaskInputs,
                        _setParameters,
                        new Dictionary<string, string>(_buildComponentHost.BuildParameters.GlobalProperties),
                        _taskLoggingContext.GetWarningsAsErrors(),
                        _taskLoggingContext.GetWarningsNotAsErrors(),
                        _taskLoggingContext.GetWarningsAsMessages());

            try
            {
                lock (_taskHostLock)
                {
                    _requiredContext = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, taskHostParameters: _taskHostParameters);
                    _connectedToTaskHost = _taskHostProvider.AcquireAndSetUpHost(_requiredContext, this, this, hostConfiguration);
                }

                if (_connectedToTaskHost)
                {
                    try
                    {
                        bool taskFinished = false;

                        while (!taskFinished)
                        {
                            _packetReceivedEvent.WaitOne();

                            INodePacket packet = null;

                            // Handle the packet that's coming in
                            while (_receivedPackets.TryDequeue(out packet))
                            {
                                if (packet != null)
                                {
                                    HandlePacket(packet, out taskFinished);
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (_taskHostLock)
                        {
                            _taskHostProvider.DisconnectFromHost(_requiredContext);
                            _connectedToTaskHost = false;
                        }
                    }
                }
                else
                {
                    LogErrorUnableToCreateTaskHost(_requiredContext, runtime, architecture, null);
                }
            }
            catch (BuildAbortedException)
            {
                LogErrorUnableToCreateTaskHost(_requiredContext, runtime, architecture, null);
            }
            catch (NodeFailedToLaunchException e)
            {
                LogErrorUnableToCreateTaskHost(_requiredContext, runtime, architecture, e);
            }

            return _taskExecutionSucceeded;
        }

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="factory">The factory for packets of the specified type.</param>
        /// <param name="handler">The handler to be called when packets of the specified type are received.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Takes a serializer and deserializes the packet.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            return _packetFactory.DeserializePacket(packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for
        /// this recipient.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        public void PacketReceived(int node, INodePacket packet)
        {
            _receivedPackets.Enqueue(packet);
            _packetReceivedEvent.Set();
        }

        /// <summary>
        /// Called by TaskHostFactory to let the task know that if it needs to do any additional cleanup steps,
        /// now would be the time.
        /// </summary>
        internal void Cleanup()
        {
            // for now, do nothing.
        }

        /// <summary>
        /// Handles the packets received from the task host.
        /// </summary>
        private void HandlePacket(INodePacket packet, out bool taskFinished)
        {
            Debug.WriteLine("[TaskHostTask] Handling packet {0} at {1}", packet.Type, DateTime.Now);
            taskFinished = false;

            switch (packet.Type)
            {
                case NodePacketType.TaskHostTaskComplete:
                    HandleTaskHostTaskComplete(packet as TaskHostTaskComplete);
                    taskFinished = true;
                    break;
                case NodePacketType.NodeShutdown:
                    HandleNodeShutdown(packet as NodeShutdown);
                    taskFinished = true;
                    break;
                case NodePacketType.LogMessage:
                    HandleLoggedMessage(packet as LogMessagePacket);
                    break;
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    break;
            }
        }

        /// <summary>
        /// Task completed executing in the task host
        /// </summary>
        private void HandleTaskHostTaskComplete(TaskHostTaskComplete taskHostTaskComplete)
        {
#if FEATURE_REPORTFILEACCESSES
            if (taskHostTaskComplete.FileAccessData?.Count > 0)
            {
                IFileAccessManager fileAccessManager = ((IFileAccessManager)_buildComponentHost.GetComponent(BuildComponentType.FileAccessManager));
                foreach (FileAccessData fileAccessData in taskHostTaskComplete.FileAccessData)
                {
                    fileAccessManager.ReportFileAccess(fileAccessData, _buildComponentHost.BuildParameters.NodeId);
                }
            }
#endif

            // If it crashed, or if it failed, it didn't succeed.
            _taskExecutionSucceeded = taskHostTaskComplete.TaskResult == TaskCompleteType.Success ? true : false;

            // reset the environment, as though the task were executed in this process all along.
            CommunicationsUtilities.SetEnvironment(taskHostTaskComplete.BuildProcessEnvironment);

            // If it crashed during the execution phase, then we can effectively replicate the inproc task execution
            // behaviour by just throwing here and letting the taskbuilder code take care of it the way it would
            // have normally.
            // We will also replicate the same behaviour if the TaskHost caught some exceptions after execution of the task.
            if ((taskHostTaskComplete.TaskResult == TaskCompleteType.CrashedDuringExecution) ||
                (taskHostTaskComplete.TaskResult == TaskCompleteType.CrashedAfterExecution))
            {
                throw new TargetInvocationException(taskHostTaskComplete.TaskException);
            }

            // On the other hand, if it crashed during initialization, there's not really a way to effectively replicate
            // the inproc behavior -- in the inproc case, the task would have failed to load and crashed long before now.
            // Furthermore, if we were just to throw here like in the execution case, we'd lose the ability to log
            // different messages based on the circumstances of the initialization failure -- whether it was a setter failure,
            // the task just could not be loaded, etc.

            // So instead, when we catch the exception in the task host, we'll also record what message we want it to use
            // when the error is logged; and given that information, log that error here.  This has the effect of differing
            // from the inproc case insofar as ContinueOnError is now respected, instead of forcing a stop here.
            if (taskHostTaskComplete.TaskResult == TaskCompleteType.CrashedDuringInitialization)
            {
                string exceptionMessage;
                string[] exceptionMessageArgs;

                if (taskHostTaskComplete.TaskExceptionMessage != null)
                {
                    exceptionMessage = taskHostTaskComplete.TaskExceptionMessage;
                    exceptionMessageArgs = taskHostTaskComplete.TaskExceptionMessageArgs;
                }
                else
                {
                    exceptionMessageArgs = [_taskType.Type.Name,
                        AssemblyUtilities.GetAssemblyLocation(_taskType.Type.GetTypeInfo().Assembly),
                        string.Empty];
                }

                _taskLoggingContext.LogFatalError(taskHostTaskComplete.TaskException, new BuildEventFileInfo(_taskLocation), taskHostTaskComplete.TaskExceptionMessage, taskHostTaskComplete.TaskExceptionMessageArgs);
            }

            // Set the output parameters for later
            foreach (KeyValuePair<string, TaskParameter> outputParam in taskHostTaskComplete.TaskOutputParameters)
            {
                _setParameters[outputParam.Key] = outputParam.Value?.WrappedParameter;
            }
        }

        /// <summary>
        /// The task host node failed for some reason
        /// </summary>
        private void HandleNodeShutdown(NodeShutdown nodeShutdown)
        {
            // if the task was canceled, it may send the shutdown packet before the task itself has exited --
            // in this case, the shutdown is expected, so don't log errors.  Also don't update taskExecutionSucceeded,
            // as it has already been set properly (likely also to false) when we dealt with the TaskComplete
            // packet that was sent immediately prior to this.
            if (!_taskCancelled)
            {
                // nothing much else to say.
                _taskExecutionSucceeded = false;

                _taskLoggingContext.LogError(new BuildEventFileInfo(_taskLocation), "TaskHostExitedPrematurely", (nodeShutdown.Exception == null) ? String.Empty : nodeShutdown.Exception.ToString());
            }
        }

        /// <summary>
        /// Handle logged messages from the task host.
        /// </summary>
        private void HandleLoggedMessage(LogMessagePacket logMessagePacket)
        {
            switch (logMessagePacket.EventType)
            {
                case LoggingEventType.BuildErrorEvent:
                    this.BuildEngine.LogErrorEvent((BuildErrorEventArgs)logMessagePacket.NodeBuildEvent.Value.Value);
                    break;
                case LoggingEventType.BuildWarningEvent:
                    this.BuildEngine.LogWarningEvent((BuildWarningEventArgs)logMessagePacket.NodeBuildEvent.Value.Value);
                    break;
                case LoggingEventType.TaskCommandLineEvent:
                case LoggingEventType.BuildMessageEvent:
                    this.BuildEngine.LogMessageEvent((BuildMessageEventArgs)logMessagePacket.NodeBuildEvent.Value.Value);
                    break;
                case LoggingEventType.CustomEvent:
                    BuildEventArgs buildEvent = logMessagePacket.NodeBuildEvent.Value.Value;

                    // "Custom events" in terms of the communications infrastructure can also be, e.g. custom error events,
                    // in which case they need to be dealt with in the same way as their base type of event.
                    if (buildEvent is BuildErrorEventArgs buildErrorEventArgs)
                    {
                        this.BuildEngine.LogErrorEvent(buildErrorEventArgs);
                    }
                    else if (buildEvent is BuildWarningEventArgs buildWarningEventArgs)
                    {
                        this.BuildEngine.LogWarningEvent(buildWarningEventArgs);
                    }
                    else if (buildEvent is BuildMessageEventArgs buildMessageEventArgs)
                    {
                        this.BuildEngine.LogMessageEvent(buildMessageEventArgs);
                    }
                    else if (buildEvent is CustomBuildEventArgs customBuildEventArgs)
                    {
                        this.BuildEngine.LogCustomEvent(customBuildEventArgs);
                    }
                    else
                    {
                        ErrorUtilities.ThrowInternalError("Unknown event args type.");
                    }

                    break;
            }
        }

        /// <summary>
        /// Since we log that we weren't able to connect to the task host in a couple of different places,
        /// extract it out into a separate method.
        /// </summary>
        private void LogErrorUnableToCreateTaskHost(HandshakeOptions requiredContext, string runtime, string architecture, NodeFailedToLaunchException e)
        {
            string taskHostLocation = NodeProviderOutOfProcTaskHost.GetMSBuildExecutablePathForNonNETRuntimes(requiredContext);
#if NETFRAMEWORK
            if (Handshake.IsHandshakeOptionEnabled(requiredContext, HandshakeOptions.NET))
            {
                taskHostLocation = NodeProviderOutOfProcTaskHost.GetMSBuildLocationForNETRuntime(requiredContext).MSBuildAssemblyPath;
            }
#endif
            string msbuildLocation = taskHostLocation ??
                // We don't know the path -- probably we're trying to get a 64-bit assembly on a
                // 32-bit machine.  At least give them the exe name to look for, though ...
                ((requiredContext & HandshakeOptions.CLR2) == HandshakeOptions.CLR2 
                ? "MSBuildTaskHost.exe" 
                : NodeProviderOutOfProcTaskHost.GetTaskHostNameFromHostContext(requiredContext));

            if (e == null)
            {
                _taskLoggingContext.LogError(new BuildEventFileInfo(_taskLocation), "TaskHostAcquireFailed", _taskType.Type.Name, runtime, architecture, msbuildLocation);
            }
            else
            {
                _taskLoggingContext.LogError(new BuildEventFileInfo(_taskLocation), "TaskHostNodeFailedToLaunch", _taskType.Type.Name, runtime, architecture, msbuildLocation, e.ErrorCode, e.Message);
            }
        }
    }
}
