// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Forwarding logger for the build check infrastructure.
/// For now we jus want to forward all events, while disable verbose logging of tasks.
/// In the future we may need more specific behavior.
/// </summary>
/// <remarks>
/// Keep this logger synchronized with <see cref="BuildCheckConnectorLogger"/>
/// </remarks>
internal class BuildCheckForwardingLogger : IForwardingLogger
{
    public IEventRedirector? BuildEventRedirector { get; set; }
    public int NodeId { get; set; }
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Quiet; set { return; } }
    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource, int nodeCount) => Initialize(eventSource);
    public void Initialize(IEventSource eventSource)
    {
        IEventSource5? eventSource5 = eventSource as IEventSource5;
        if (eventSource5 != null)
        {
            eventSource5.TaskParameterLogged += EventSource5_TaskParameterEventRaised;
            eventSource5.BuildCheckEventRaised += EventSource5_BuildCheckEventRaised;
        }

        eventSource.StatusEventRaised  += EventSource_BuildStatus;
    }
    private void EventSource_BuildStatus(object sender, BuildStatusEventArgs buildStatusEvent)
    {
        switch (buildStatusEvent)
        {
            case ProjectEvaluationFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case ProjectEvaluationStartedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case ProjectStartedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case ProjectFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case BuildFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case TaskStartedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case TaskFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
        }
    }

    public void EventSource5_BuildCheckEventRaised(object sender, BuildCheckEventArgs e)
    {
        BuildEventRedirector?.ForwardEvent(e);
    }

    public void EventSource5_TaskParameterEventRaised(object sender, TaskParameterEventArgs taskParameter)
    {
        BuildEventRedirector?.ForwardEvent(taskParameter);
    }

    public void Shutdown()
    {
    }
}
