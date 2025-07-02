# Thread Safe Tasks

## Overview
In the traditional MSBuild execution model, tasks assume they own the entire process during execution. They freely mutate global process state including environment variables, the current working directory, and other process-level resources. This design worked well when MSBuild used separate processes for parallel execution, as each task had true process isolation.

However, with the introduction of multithreaded MSBuild execution, multiple tasks can now run concurrently within the same process. To mark that a task is multithreaded-MSBuild-aware, we will introduce a new interface that tasks can implement.

## Interface Definition

```csharp
/// <summary>
/// Interface for tasks that support multithreaded execution in MSBuild.
/// Tasks implementing this interface guarantee that they can run concurrently with other tasks
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

### Questions and Design Notes

**Question**: Should we expose an `ExecutionContext` property, or should we have an overload for the `Execute` function that takes `ExecutionContext` as a parameter? If we implement `ExecuteAsync` in async tasks, the property approach would be decoupled, while adding an overload would need to be done for both sync and async tasks. Exposing a property is also consistent with how the `BuildEngine` property is available in tasks. The property approach is more extensible if additional parameters need to be passed in the future.

**Question**: I want to prevent customers from setting or modifying the ExecutionContext, but I don't want to create it during task construction.

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

    /// <summary>
    /// Context-aware File System. All Path/File/Directory calls should be used through it.
    /// </summary>
    IContextFileSystem FileSystem { get; }
}
```


### Questions and Design Notes

**Question**: Should we use `IReadOnlyDictionary<string, string>` with setter methods or `IDictionary<string, string>` for the `EnvironmentVariables` property? Do we want control over modifications to the dictionary? Should we even expose the `EnvironmentVariables` dictionary? It might not be thread-safe.

**Note**: Our `ITaskExecutionContext` class should be thread-safe as well! Task authors can create multi-threaded tasks, and the class should be safe to use.

**TODO**: Figure out how our `IContextFileSystem` will work
