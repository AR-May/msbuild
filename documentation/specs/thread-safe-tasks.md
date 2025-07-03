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
    IContextBasedFileSystem FileSystem { get; }
}
```


### Questions and Design Notes

**Question**: Should we use `IReadOnlyDictionary<string, string>` with setter methods or `IDictionary<string, string>` for the `EnvironmentVariables` property? Do we want control over modifications to the dictionary? Should we even expose the `EnvironmentVariables` dictionary? It might not be thread-safe.

**Note**: Our `ITaskExecutionContext` class should be thread-safe as well! Task authors can create multi-threaded tasks, and the class should be safe to use.

**TODO**: Figure out how our `IContextFileSystem` will work

### List of prohibited APIs

The following APIs are **prohibited** in thread-safe tasks because they modify global process state or rely on process-level state that can cause race conditions in multithreaded execution:

#### System.IO.Path Class // Redist version. check for msft extensions
**Reason**: Methods that resolve relative paths using the current working directory are not thread-safe.

- `Path.GetFullPath(string path)` - Uses current working directory to resolve relative paths
- `Path.GetRelativePath(string relativeTo, string path)` - When `relativeTo` is relative, uses current working directory
- Any method that implicitly uses the current working directory for path resolution

**Alternative**: Use `IContextBasedFileSystem.GetFullPath()` or ensure all paths are absolute.

#### System.IO.File Class
**Reason**: Methods that accept relative paths use the current working directory, which is shared process state.

- `File.Exists(string path)` - When path is relative
- `File.ReadAllText(string path)` - When path is relative
- `File.WriteAllText(string path, string contents)` - When path is relative
- `File.Copy(string sourceFileName, string destFileName)` - When paths are relative
- `File.Move(string sourceFileName, string destFileName)` - When paths are relative
- `File.Delete(string path)` - When path is relative
- `File.Create(string path)` - When path is relative
- `File.Open(string path, FileMode mode)` - When path is relative
- `File.OpenRead(string path)` - When path is relative
- `File.OpenWrite(string path)` - When path is relative
- `File.ReadAllBytes(string path)` - When path is relative
- `File.WriteAllBytes(string path, byte[] bytes)` - When path is relative
- `File.ReadAllLines(string path)` - When path is relative
- `File.WriteAllLines(string path, string[] contents)` - When path is relative
- `File.AppendAllText(string path, string contents)` - When path is relative
- `File.GetAttributes(string path)` - When path is relative
- `File.SetAttributes(string path, FileAttributes fileAttributes)` - When path is relative
- `File.GetCreationTime(string path)` - When path is relative
- `File.SetCreationTime(string path, DateTime creationTime)` - When path is relative
- `File.GetLastAccessTime(string path)` - When path is relative
- `File.SetLastAccessTime(string path, DateTime lastAccessTime)` - When path is relative
- `File.GetLastWriteTime(string path)` - When path is relative
- `File.SetLastWriteTime(string path, DateTime lastWriteTime)` - When path is relative

**Alternative**: Use `IContextBasedFileSystem` methods or ensure all paths are absolute.

#### System.IO.Directory Class
**Reason**: Methods that accept relative paths use the current working directory, which is shared process state.

- `Directory.Exists(string path)` - When path is relative
- `Directory.CreateDirectory(string path)` - When path is relative
- `Directory.Delete(string path)` - When path is relative
- `Directory.GetFiles(string path)` - When path is relative
- `Directory.GetDirectories(string path)` - When path is relative
- `Directory.GetFileSystemEntries(string path)` - When path is relative
- `Directory.Move(string sourceDirName, string destDirName)` - When paths are relative
- `Directory.GetCurrentDirectory()` - Returns process-level current directory
- `Directory.SetCurrentDirectory(string path)` - Modifies process-level current directory
- `Directory.GetParent(string path)` - When path is relative
- `Directory.GetCreationTime(string path)` - When path is relative
- `Directory.SetCreationTime(string path, DateTime creationTime)` - When path is relative
- `Directory.GetLastAccessTime(string path)` - When path is relative
- `Directory.SetLastAccessTime(string path, DateTime lastAccessTime)` - When path is relative
- `Directory.GetLastWriteTime(string path)` - When path is relative
- `Directory.SetLastWriteTime(string path, DateTime lastWriteTime)` - When path is relative

**Alternative**: Use `IContextBasedFileSystem` methods or ensure all paths are absolute.

#### System.Environment Class
**Reason**: These methods modify or access global process state.

- `Environment.SetEnvironmentVariable(string variable, string value)` - Modifies process-level environment
- `Environment.SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)` - When target is `Process`
- `Environment.CurrentDirectory` (getter) - Returns process-level current directory
- `Environment.CurrentDirectory` (setter) - Modifies process-level current directory
- `Environment.Exit` - Prohibit


**Alternative**: Use `ITaskExecutionContext.GetEnvironmentVariable()`, `ITaskExecutionContext.SetEnvironmentVariable()`, and `ITaskExecutionContext.CurrentDirectory`.

#### System.IO.FileInfo Class
**Reason**: Constructor with relative path uses current working directory.

- `new FileInfo(string fileName)` - When fileName is relative

**Alternative**: Use `IContextBasedFileSystem.GetFileInfo()` or ensure paths are absolute.

#### System.IO.DirectoryInfo Class
**Reason**: Constructor with relative path uses current working directory.

- `new DirectoryInfo(string path)` - When path is relative

**Alternative**: Use `IContextBasedFileSystem.GetDirectoryInfo()` or ensure paths are absolute.

#### System.Diagnostics.Process Class
**Reason**: Process creation that inherits environment variables and working directory from current process.

- `Process.Start(string fileName)` - Inherits current process environment and working directory
- `Process.Start(string fileName, string arguments)` - Inherits current process environment and working directory
- `Process.Start(ProcessStartInfo startInfo)` - When `UseShellExecute = true` and working directory/environment not explicitly set
- `Process.GetCurrentProcess().Kill()` - prohibit
- `ThreadPool.SetMinThreads(Int32, Int32)`  - prohibit

**Alternative**: Always explicitly set `ProcessStartInfo.WorkingDirectory` and `ProcessStartInfo.Environment` or `ProcessStartInfo.EnvironmentVariables`.

#### System.IO.FileStream Class
**Reason**: Constructor with relative path uses current working directory.

- `new FileStream(string path, FileMode mode)` - When path is relative
- `new FileStream(string path, FileMode mode, FileAccess access)` - When path is relative
- All other FileStream constructors that accept a string path parameter when the path is relative

**Alternative**: Use `IContextBasedFileSystem` methods or ensure all paths are absolute.

#### Other File System APIs
**Reason**: Various file system operations that rely on current working directory.

- `System.IO.StreamReader(string path)` - When path is relative
- `System.IO.StreamWriter(string path)` - When path is relative
- `System.IO.File.OpenText(string path)` - When path is relative
- `System.IO.File.CreateText(string path)` - When path is relative
- `System.IO.File.AppendText(string path)` - When path is relative

**Alternative**: Use `IContextBasedFileSystem` methods or ensure all paths are absolute.

#### Registry Operations (Windows) - out of scope
**Reason**: Registry modifications affect global system state.

- `Microsoft.Win32.Registry.SetValue()` - Modifies global registry state
- Registry key modifications through `RegistryKey.SetValue()` on system-level keys

**Alternative**: Avoid or ensure operations are isolated to user-specific or temporary locations.

#### Static Global Variables - talk to Rainer
Reasons for having them:
A way to carry info between executions of the task.

**Reason**: Static mutable state is shared across all threads and can cause race conditions.

- Any static mutable fields or properties in custom classes
- Static collections that are modified during task execution
- Global caches that are not thread-safe

**Alternative**: Use thread-safe collections, immutable data structures, or instance-based state through `ITaskExecutionContext`.

### Assembly loading.

**Issues**:
1. What happens with tasks that loads assemblies in the task host? Can they run there?

**Action**: Warn that version conflict in tasks assemblies explode the build for certain (before it might be sporadic). Note - not only dynamically loaded dependencies are an issue.

### P/Invoke
**Action**: Warn that sutomer needs to check the code. Provide link to docs what toi check for. 
**Action**: Either do a sham that uses our currect directory (id possible at all), or add to the warning that they need to use absolute paths on the calls themselves. 

### Cultural Info (Globalization)

**Reason**: Cultural settings are process-wide global state that affect string formatting, parsing, sorting, and comparison operations. Modifying cultural settings in one task can affect the behavior of other concurrently running tasks, leading to unpredictable results and race conditions.


#### System.Globalization.CultureInfo Class
Make setters a warning.

**Prohibited APIs**:
- `CultureInfo.CurrentCulture` (setter) - Modifies process-wide current culture
- `CultureInfo.CurrentUICulture` (setter) - Modifies process-wide UI culture
- `CultureInfo.DefaultThreadCurrentCulture` (setter) - Affects new threads created after the change
- `CultureInfo.DefaultThreadCurrentUICulture` (setter) - Affects new threads created after the change
- `Thread.CurrentThread.CurrentCulture` (setter) - Can affect task execution if task uses multiple threads
- `Thread.CurrentThread.CurrentUICulture` (setter) - Can affect task execution if task uses multiple threads

#### Potentially Problematic APIs

**String Operations That Depend on Current Culture**:
- `string.ToUpper()` - Uses current culture for case conversion
- `string.ToLower()` - Uses current culture for case conversion
- `string.Compare(string, string)` - Uses current culture for comparison
- `Array.Sort(string[])` - Uses current culture for sorting
- `string.StartsWith(string)` - Uses current culture by default
- `string.EndsWith(string)` - Uses current culture by default
- `string.IndexOf(string)` - Uses current culture by default

**Numeric and DateTime Formatting**:
- `int.ToString()` - Uses current culture for formatting
- `double.ToString()` - Uses current culture for formatting
- `DateTime.ToString()` - Uses current culture for formatting
- `DateTime.Parse(string)` - Uses current culture for parsing
- `double.Parse(string)` - Uses current culture for parsing

#### Best Practices for Thread-Safe Tasks

1. **Always Use Explicit Culture**: Specify `CultureInfo.InvariantCulture` for all formatting and parsing operations
2. **Use Ordinal String Comparisons**: Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string comparisons
3. **Never Modify Process Culture**: Avoid setting `CultureInfo.CurrentCulture` or `CultureInfo.CurrentUICulture`
4. **Localized Output**: If you need localized output for user messages, use resource files and explicit culture specification rather than changing the process culture
5. **Culture-Aware Sorting**: If you need culture-aware sorting, create a `CultureInfo` instance explicitly rather than relying on the current culture