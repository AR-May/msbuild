// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Type of handler for ProjectEvaluationStartedEventArgs events
    /// </summary>
    public delegate void ProjectEvaluationStartedEventHandler(object sender, ProjectEvaluationStartedEventArgs e);

    /// <summary>
    /// Type of handler for ProjectEvaluationFinishedEventArgs events
    /// </summary>
    public delegate void ProjectEvaluationFinishedEventHandler(object sender, ProjectEvaluationFinishedEventArgs e);

    /// <summary>
    /// Type of handler for TaskParameterEventArgs events
    /// </summary>
    public delegate void TaskParameterEventHandler(object sender, TaskParameterEventArgs e);

    /// <summary>
    /// Type of handler for BuildCheckEventHandler events
    /// </summary>
    public delegate void BuildCheckEventHandler(object sender, BuildCheckEventArgs e);

    /// <summary>
    /// This interface defines the events raised by the build engine.
    /// Loggers use this interface to subscribe to the events they
    /// are interested in receiving.
    /// </summary>
    public interface IEventSource5 : IEventSource4
    {
        /// <summary>
        /// this event is raised to when telemetry is logged.
        /// </summary>
        event TaskParameterEventHandler TaskParameterLogged;

        /// <summary>
        /// This event is raised to log BuildCheck events.
        /// </summary>
        event BuildCheckEventHandler BuildCheckEventRaised;
    }
}
