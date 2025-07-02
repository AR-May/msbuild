# Async Tasks

## Overview
We should consider how MSBuild can benefit from asynchronous task execution patterns that align with modern .NET development practices.

## Interface Definition

```csharp
public interface IAsyncTask : ITask
{
    /// <summary>
    /// Executes the task asynchronously with the provided execution context.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the task execution</param>
    /// <returns>A task that represents the asynchronous execution. True if successful, false otherwise.</returns>
    ValueTask<bool> ExecuteAsync(CancellationToken cancellationToken = default) { }
}
```

## Questions and Design Notes

**Question**: What are the strategic benefits of implementing async support?

**Question**: If we were to write the entire build system from scratch, we would implement it using async and await patterns. Instead of having a Scheduler schedule targets for execution, we would allow async to handle the dependency graph for us. However, given our current situation, we have a limited number of (thread) nodes, each running tasks within targets sequentially. How do async patterns combine with this architecture? What will the node do when awaiting I/O? Threads that execute the task would not be blocked during I/O operations, but the node threads themselves would be, so we would not save any resources—only incur additional overhead. We also already have `BuildEngine3.Yield()` and `BuildEngine3.Reacquire()` to free up nodes when tasks perform heavy I/O operations.

**Question**: Should async tasks implement both `Execute()` and `ExecuteAsync()`, or should we separate async interfaces from sync interfaces (though that might be challenging)?

**Question**: Async design provides proper support for cancellation during long-running operations. We already have `ICancelableTask` that allows canceling tasks that implement it, but having proper cancellation support is beneficial. How will cancellation through the `cancellationToken` interact with existing cancellation via `ICancelableTask`?

**Question**: Will our exception handling and error reporting be affected by the change to async?

**Question**: What overhead will we incur by making MSBuild and SDK tasks async? Will the benefits outweigh the costs?

**Question**: Do we need a default implementation of `ExecuteAsync()` that calls `Execute()`? This would be false async - creating overhead without benefits. We probably should not encourage this pattern.

Some Copilot-generated questions to consider:

**Question**: How will async tasks interact with MSBuild's batching system? When tasks are batched (e.g., processing multiple items), should `ExecuteAsync()` be called once per batch or once per item? How does this affect parallelization opportunities?

**Question**: What happens to task parameter validation and property setting when transitioning to async? The current `TaskExecutionHost` uses reflection to set properties before calling `Execute()` - will this same pattern work with `ExecuteAsync()`?

**Question**: How should async tasks handle the `BuildEngine` property and logging? Should logging calls within async tasks be thread-safe, and how do we maintain proper task context across async boundaries?

**Question**: What is the interaction between async tasks and MSBuild's AppDomain isolation? Some tasks run in separate AppDomains - how does async/await work across AppDomain boundaries, and should async tasks be restricted from AppDomain isolation?

**Question**: How do async tasks work with MSBuild's out-of-process task execution? The current `OutOfProcTaskAppDomainWrapper` handles task execution in separate processes - can async tasks be executed out-of-process, and what are the marshaling implications?

**Question**: Should there be different async execution strategies for different task types? For example, should I/O-bound tasks (file operations) have different async patterns than CPU-bound tasks (compilation) or network-bound tasks (package downloads)?

**Question**: How will async tasks integrate with MSBuild's target dependency resolution? If multiple async tasks are running in parallel, how do we ensure proper target dependency ordering and avoid race conditions?

**Question**: What impact does async have on MSBuild's incremental build and up-to-date checking? How do we handle scenarios where async tasks might have side effects that affect build incrementality?

**Question**: How should async tasks handle resource management and cleanup? If a task holds file handles, network connections, or other resources, how do we ensure proper disposal when cancellation occurs?

**Question**: What debugging and diagnostic experience should we provide for async tasks? How can developers troubleshoot deadlocks, race conditions, or performance issues in async task execution?

**Question**: Should MSBuild provide any built-in async utilities or base classes for common async patterns? For example, async file operations, HTTP requests, or process execution helpers?

**Question**: How do async tasks interact with MSBuild's task factories (`ITaskFactory`, `ITaskFactory2`)? Do task factories need to be updated to support async task creation and lifecycle management?

**Question**: What compatibility requirements exist for existing MSBuild hosts and extensions? How do we ensure that third-party tools that embed MSBuild can opt-in or opt-out of async task execution?

**Question**: How should task timeouts work with async tasks? Should we rely on `CancellationToken` timeouts, or provide additional timeout mechanisms specific to MSBuild's execution model?

**Question**: What happens to MSBuild's task output inference (`TaskExecutionMode.InferOutputsOnly`) with async tasks? Can output inference work asynchronously, or does it require synchronous execution?

**Question**: How do async tasks interact with MSBuild's STA thread requirements? Some tasks require STA threads (`RequireSTAThread` attribute) - how does this constraint work with async execution?

**Question**: Should async task execution be controllable through MSBuild command-line options or project properties? Should users be able to force synchronous execution for debugging or compatibility reasons?

**Question**: How will async tasks affect MSBuild's memory usage patterns? Will async execution lead to higher memory usage due to task state machines and continuations, and how can we mitigate this?

**Question**: What testing strategy should we adopt for async tasks? How do we ensure deterministic testing of async task execution, cancellation, and error handling scenarios?