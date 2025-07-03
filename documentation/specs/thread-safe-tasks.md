# Thread Safe Tasks

## Overview

In the traditional MSBuild execution model, tasks operate under the assumption that they have exclusive control over the entire process during execution. This allows them to freely modify global process state, including environment variables, the current working directory, and other process-level resources. This design was well-suited for MSBuild's historical approach of using separate processes for parallel execution.

However, with the introduction of multithreaded MSBuild execution mode, multiple tasks can now run concurrently within the same process. This change requires a new approach to task design to prevent race conditions and ensure thread safety. To enable tasks to opt into this multithreaded execution model, we introduce a new interface that tasks should implement and utilize to declare their thread-safety.

Tasks that implement the following `IThreadSafeTask` interface should avoid using APIs that modify global process state or rely on process-level state that could cause conflicts when multiple tasks execute simultaneously. For a list of such APIs and their safe alternatives, refer to [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

## IThreadSafeTask Interface

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

**TODO**: I want to prevent customers from setting or modifying the ExecutionContext, but I don't want to create it during task construction.

## ITaskExecutionContext Interface

The `ITaskExecutionContext` provides tasks with access to execution environment information that was previously accessed through global process state:

```csharp
/// <summary>
/// Provides access to task execution context and environment information.
/// </summary>
public interface ITaskExecutionContext
{
    /// <summary>
    /// Gets the environment context for this task execution.
    /// Provides thread-safe access to environment variables.
    /// </summary>
    ITaskEnvironmentContext Environment { get; }

    /// <summary>
    /// Context-aware File System. All Path/File/Directory calls should be used through it.
    /// </summary>
    IContextBasedFileSystem FileSystem { get; }
}
```

## ITaskEnvironmentContext Interface

The `ITaskEnvironmentContext` provides thread-safe access to environment variables:

```csharp
/// <summary>
/// Provides thread-safe access to environment variables for task execution.
/// </summary>
public interface ITaskEnvironmentContext
{
    /// <summary>
    /// Gets environment variables for this task execution.
    /// </summary>
    IReadOnlyDictionary<string, string> Variables { get; }
    
    /// <summary>
    /// Gets an environment variable value, or null if not found.
    /// </summary>
    /// <param name="name">The environment variable name</param>
    /// <returns>The environment variable value, or null if not found</returns>
    string GetVariable(string name);
    
    /// <summary>
    /// Sets an environment variable for subsequent tasks in the same project.
    /// This change will be isolated to the current project's execution context.
    /// </summary>
    /// <param name="name">The environment variable name</param>
    /// <param name="value">The environment variable value</param>
    void SetVariable(string name, string value);
}
```

### Questions and Design Notes

**Question**: Should we use `IReadOnlyDictionary<string, string>` with setter methods or `IDictionary<string, string>` for the `EnvironmentVariables` property? Do we want control over modifications to the dictionary? Should we even expose the `EnvironmentVariables` dictionary? It might not be thread-safe.

**Note**: Our `ITaskExecutionContext` class should be thread-safe as well! Task authors can create multi-threaded tasks, and the class should be safe to use.

**TODO**: Figure out how our `IContextFileSystem` will work.