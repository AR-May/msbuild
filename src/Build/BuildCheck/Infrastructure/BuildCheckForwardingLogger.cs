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
internal class BuildCheckForwardingLogger : IForwardingLogger
{
    public IEventRedirector? BuildEventRedirector { get; set; }
    public int NodeId { get; set; }
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Quiet; set { return; } }
    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource, int nodeCount) => Initialize(eventSource);
    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;
    }

    public void EventSource_AnyEventRaised(object sender, BuildEventArgs buildEvent)
    {
        switch (buildEvent)
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
            case BuildCheckTracingEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case BuildCheckAcquisitionEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case TaskStartedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case TaskFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case TaskParameterEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
            case BuildFinishedEventArgs e:
                BuildEventRedirector?.ForwardEvent(e);
                break;
        }
    }

    public void Shutdown()
    {
    }
}
