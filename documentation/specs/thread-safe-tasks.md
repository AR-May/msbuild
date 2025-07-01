# Thread Safe Tasks

## Overview
In the traditional MSBuild execution model, tasks assume they own the entire process during execution. They freely mutate global process state including environment variables, the current working directory, and other process-level resources. This design worked well when MSBuild used separate processes for parallel execution, as each task had true process isolation.

However, with the introduction of multithreaded MSBuild execution, multiple tasks can now run concurrently within the same process. To mark that a task is multithreaded-MSBuild-aware, we will introduce a new interface that tasks can implement.

## Interface Definition

```csharp
/// <summary>
/// Interface for tasks that support multithreaded execution in MSBuild.
/// Tasks implementing this interface can run concurrently with other tasks
/// within the same MSBuild process.
/// </summary>
public interface IThreadSafeTask : ITask
{
    /// <summary>
    /// Execution context for the task, providing thread-safe
    /// access to environment variables, working directory, and other build context.
    /// This property will be set by the MSBuild engine before execution is called.
    /// </summary>
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

## ITaskExecutionContext Interface

The `ITaskExecutionContext` provides tasks with access to execution environment information that was previously accessed through global process state:

```csharp
/// <summary>
/// Provides access to task execution context and environment information.
/// </summary>
public interface ITaskExecutionContext
{
    /// <summary>
    /// Gets or sets the current working directory for this task execution.
    /// Changes will be isolated to the current project's execution context.
    /// </summary>
    string CurrentDirectory { get; set; }
    
    /// <summary>
    /// Gets environment variables for this task execution.
    /// </summary>
    IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
    
    /// <summary>
    /// Gets an environment variable value, or null if not found.
    /// </summary>
    /// <param name="name">The environment variable name</param>
    /// <returns>The environment variable value, or null if not found</returns>
    string GetEnvironmentVariable(string name);
    
    /// <summary>
    /// Sets an environment variable for subsequent tasks in the same project.
    /// This change will be isolated to the current project's execution context.
    /// </summary>
    /// <param name="name">The environment variable name</param>
    /// <param name="value">The environment variable value</param>
    void SetEnvironmentVariable(string name, string value);
}
```


## Questions and Design Notes

**Question**: Should we use for the `EnvironmentVariables` property `IReadOnlyDictionary<string, string>` with setting function or `IDictionary<string, string>`? Do we want controll over the modifications of the dictionary?
