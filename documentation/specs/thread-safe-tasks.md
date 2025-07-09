# Thread Safe Tasks

## Overview

In the traditional MSBuild execution model, tasks operate under the assumption that they have exclusive control over the entire process during execution. This allows them to freely modify global process state, including environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's historical approach of using separate processes for parallel execution.

However, with the introduction of multithreaded MSBuild execution mode, multiple tasks can now run concurrently within the same process. This change requires a new approach to task design to prevent race conditions and ensure thread safety. To enable tasks to opt into this multithreaded execution model, we introduce a new interface that tasks should implement and utilize to declare their thread-safety.

Tasks that implement the following `IThreadSafeTask` interface declares itself to be thread-safe. It should avoid using APIs that modify or rely on global process state, as that could cause conflicts when multiple tasks execute simultaneously. For a list of such APIs, refer to [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

Customers should be using the `ExecutionContext` property of the `IThreadSafeTask` interface to access the safe API that uses or modifies the global process state.
Example: Task authores should be able to use `ExecutionContext.Path.GetFullPath(relativePath)` instead of `Path.GetFullPath(relativePath)`.

## Option 1: Structured Interfaces

```csharp
public interface IThreadSafeTask<TExecutionContext> : ITask
    where TExecutionContext : ITaskExecutionContext
{
    TExecutionContext ExecutionContext { get; set; }
}
```

### ITaskExecutionContext Interface

The `ITaskExecutionContext` provides tasks with access to what was in multi-process mode the global process state, such as environment variables and working directory:
```csharp
public interface ITaskExecutionContext
{    
    string CurrentDirectory { get; set; }

    IEnvironment Environment { get; }

    IPath Path { get; }

    IFile File { get; }

    IDirectory Directory { get; }
}
```

The `IEnvironment` provides thread-safe access to environment variables:
```csharp
public interface IEnvironment
{
    string? GetEnvironmentVariable(string name);
    
    Dictionary<string, string?> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
}
```

Thread-safe alternative to `System.IO.Path` class:
```csharp
public interface IPath
{
    string GetFullPath(string path);
}
```

Thread-safe alternative to `System.IO.File` class:

```csharp
public interface IFile
{
    bool Exists(string path);
    string ReadAllText(string path);
    ... // For complete list of methods, see "IFile Methods" section below
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public interface IDirectory
{
    bool Exists(string path);
    DirectoryInfo CreateDirectory(string path);
    ... // For complete list of methods, see "IDirectory Methods" section below
}
```

### Interface Versioning Pattern

To handle future updates to interfaces without breaking existing implementations, we wil use a versioning pattern. 

```csharp
public interface IFile2 : IFile
{
    string ReadAllText(string path, Encoding encoding)
    ... // Other new methods added in version 2
}
```

Unfortunatelly, `ITaskExecutionContext` will need a version update as well.
```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    new IPath2 Path { get; }
    // Other methods can be added here
}
```

### Usage Examples

```csharp
// Tasks should use minimum `ITaskExecutionContext` version that provides the needed functionality
public class MyTask : IThreadSafeTask<ITaskExecutionContext>
{
    public ITaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt");
        return true;
    }
}

// Tasks that need newer functionality
public class AdvancedTask : IThreadSafeTask<ITaskExecutionContext2>
{
    public ITaskExecutionContext2 ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt", Encoding.UTF8);
        return true;
    }
}
```
**Note** During the loading of the task assembly, we can check whether the needed version of the `ITaskExecutionContext` is present and gracefully fail if not.
**Note** Consider backporting this check to 17.14 branch as well.

## Option 2: Abstract Classes

This approach uses abstract classes instead of interfaces, providing version control through a version property and allowing for default implementations.

```csharp
public interface IThreadSafeTask : ITask
{
    TaskExecutionContext ExecutionContext { get; set; }
}
```

### TaskExecutionContext Abstract Class

The `TaskExecutionContext` provides tasks with access to what was in multi-process mode the global process state, such as environment variables and working directory:

```csharp
public abstract class TaskExecutionContext
{
    // Initial version
    public const int Version1 = 1;
    public virtual int Version => Version1;

    // Properties for the execution context
    public virtual string CurrentDirectory { get; set; }
    public virtual TaskEnvironment Environment { get; }
    public virtual TaskPath Path { get; }
    public virtual TaskFile File { get; }
    public virtual TaskDirectory Directory { get; }
}
```

The `TaskEnvironment` provides thread-safe access to environment variables:
```csharp
public abstract class TaskEnvironment
{
    public virtual string? GetEnvironmentVariable(string name) => throw new NotImplementedException();
    public virtual Dictionary<string, string?> GetEnvironmentVariables() => throw new NotImplementedException();
    public virtual void SetEnvironmentVariable(string name, string? value) => throw new NotImplementedException();
}
```

Thread-safe alternative to `System.IO.Path` class:
```csharp
public abstract class TaskPath
{
    public const int Version1 = 1;
    public virtual int Version => Version1;

    public virtual string GetFullPath(string path) => throw new NotImplementedException();
}
```

Thread-safe alternative to `System.IO.File` class:
```csharp
public abstract class TaskFile
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    // For complete list of methods, see "TaskFile Methods" section below
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public abstract class TaskDirectory
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual DirectoryInfo CreateDirectory(string path) => throw new NotImplementedException();
    // For complete list of methods, see "TaskDirectory Methods" section below
}
```

### Versioning Pattern with Abstract Classes

With abstract classes, versioning is handled through version constants. There is no need to create a new class to add methods.

```csharp
public abstract class TaskPath
{
    public const int Version1 = 1;
    public const int Version2 = 2; // adding version
    public virtual string GetFullPath(string path) => throw new NotImplementedException();

    public virtual int Version => Version2; // pointing to the new version
    public virtual string GetFullPath2(string path) => throw new NotImplementedException();
    // Other methods can be added here
#endif
}
```

### Usage Examples

```csharp
// Tasks use the abstract class-based approach
public class MyTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt");
        return true;
    }
}

// Tasks can check version at runtime
public class VersionAwareTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        if (ExecutionContext.Path.Version >= TaskPath.Version2)
        {
            ... // Use newer functionality if available
        }
        else
        {
            ... // Fall back to older functionality
        }
        return true;
    }
}
```

**Note**: During the loading of the task assembly, we can check the version compatibility and gracefully fail if the required version is not available.

## Methods Reference

### Path Methods

- `string GetFullPath(string path)`

### File Methods

**TODO**: Generated with Copilot, review that it correctly mirrors all the functions in .NET class that use relative paths:

- `bool Exists(string path)`
- `string ReadAllText(string path)`
- `string ReadAllText(string path, Encoding encoding)`
- `byte[] ReadAllBytes(string path)`
- `string[] ReadAllLines(string path)`
- `string[] ReadAllLines(string path, Encoding encoding)`
- `IEnumerable<string> ReadLines(string path)`
- `IEnumerable<string> ReadLines(string path, Encoding encoding)`
- `void WriteAllText(string path, string contents)`
- `void WriteAllText(string path, string contents, Encoding encoding)`
- `void WriteAllBytes(string path, byte[] bytes)`
- `void WriteAllLines(string path, string[] contents)`
- `void WriteAllLines(string path, IEnumerable<string> contents)`
- `void WriteAllLines(string path, string[] contents, Encoding encoding)`
- `void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)`
- `void AppendAllText(string path, string contents)`
- `void AppendAllText(string path, string contents, Encoding encoding)`
- `void AppendAllLines(string path, IEnumerable<string> contents)`
- `void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)`
- `void Copy(string sourceFileName, string destFileName)`
- `void Copy(string sourceFileName, string destFileName, bool overwrite)`
- `void Move(string sourceFileName, string destFileName)`
- `void Move(string sourceFileName, string destFileName, bool overwrite)`
- `void Delete(string path)`
- `FileAttributes GetAttributes(string path)`
- `void SetAttributes(string path, FileAttributes fileAttributes)`
- `DateTime GetCreationTime(string path)`
- `DateTime GetCreationTimeUtc(string path)`
- `DateTime GetLastAccessTime(string path)`
- `DateTime GetLastAccessTimeUtc(string path)`
- `DateTime GetLastWriteTime(string path)`
- `DateTime GetLastWriteTimeUtc(string path)`
- `void SetCreationTime(string path, DateTime creationTime)`
- `void SetCreationTimeUtc(string path, DateTime creationTimeUtc)`
- `void SetLastAccessTime(string path, DateTime lastAccessTime)`
- `void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)`
- `void SetLastWriteTime(string path, DateTime lastWriteTime)`
- `void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)`
- `FileStream OpenRead(string path)`
- `FileStream OpenWrite(string path)`
- `FileStream Open(string path, FileMode mode)`
- `FileStream Open(string path, FileMode mode, FileAccess access)`
- `FileStream Open(string path, FileMode mode, FileAccess access, FileShare share)`
- `FileStream Create(string path)`
- `FileStream Create(string path, int bufferSize)`
- `FileStream Create(string path, int bufferSize, FileOptions options)`
- `StreamReader OpenText(string path)`
- `StreamWriter CreateText(string path)`
- `StreamWriter AppendText(string path)`
- `void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)`
- `void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors)`

### Directory Methods

**TODO**: Generated with Copilot, review that it correctly mirrors all the functions in .NET class that use relative paths:

- `bool Exists(string path)`
- `DirectoryInfo CreateDirectory(string path)`
- `void Delete(string path)`
- `void Delete(string path, bool recursive)`
- `void Move(string sourceDirName, string destDirName)`
- `string[] GetFiles(string path)`
- `string[] GetFiles(string path, string searchPattern)`
- `string[] GetFiles(string path, string searchPattern, SearchOption searchOption)`
- `string[] GetDirectories(string path)`
- `string[] GetDirectories(string path, string searchPattern)`
- `string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)`
- `string[] GetFileSystemEntries(string path)`
- `string[] GetFileSystemEntries(string path, string searchPattern)`
- `string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)`
- `IEnumerable<string> EnumerateFiles(string path)`
- `IEnumerable<string> EnumerateFiles(string path, string searchPattern)`
- `IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)`
- `IEnumerable<string> EnumerateDirectories(string path)`
- `IEnumerable<string> EnumerateDirectories(string path, string searchPattern)`
- `IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)`
- `DateTime GetCreationTime(string path)`
- `DateTime GetCreationTimeUtc(string path)`
- `DateTime GetLastAccessTime(string path)`
- `DateTime GetLastAccessTimeUtc(string path)`
- `DateTime GetLastWriteTime(string path)`
- `DateTime GetLastWriteTimeUtc(string path)`
- `void SetCreationTime(string path, DateTime creationTime)`
- `void SetCreationTimeUtc(string path, DateTime creationTimeUtc)`
- `void SetLastAccessTime(string path, DateTime lastAccessTime)`
- `void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)`
- `void SetLastWriteTime(string path, DateTime lastWriteTime)`
- `void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)`
- `DirectoryInfo GetParent(string path)`
- `string GetDirectoryRoot(string path)`
- `string GetCurrentDirectory()`
- `void SetCurrentDirectory(string path)`


## Notes

**Note**: Our classes should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.
**TODO**: I want to prevent customers from setting or modifying the ExecutionContext, but I don't want to create it during task construction. 

