# Thread Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization.

With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to prevent race conditions and ensure thread safety when multiple tasks access shared process state simultaneously.

To enable this multithreaded execution model, we introduce the `IThreadSafeTask` interface that tasks can implement to declare their thread-safety capabilities. Tasks implementing this interface must avoid using APIs that modify or depend on global process state, as such usage could cause conflicts when multiple tasks execute concurrently, see [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

Task authors should use the `ExecutionContext` property provided by the `IThreadSafeTask` interface to access thread-safe APIs for operations that would otherwise use global process state. For example, use `ExecutionContext.Path.GetFullPath(relativePath)` instead of the standard `Path.GetFullPath(relativePath)`.

## Option 1: Structured Interfaces

```csharp
public interface IThreadSafeTask<TExecutionContext> : ITask
    where TExecutionContext : ITaskExecutionContext
{
    TExecutionContext ExecutionContext { get; set; }
}
```

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
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.File` class:

```csharp
public interface IFile
{
    bool Exists(string path);
    string ReadAllText(string path);
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public interface IDirectory
{
    bool Exists(string path);
    DirectoryInfo CreateDirectory(string path);
    ... // Complete list of methods can be find below
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

Thread-safe alternative to `System.IO.Path` class. 
```csharp
public abstract class TaskPath
{
    public virtual string GetFullPath(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

**Note** the default implementations allow forward compatibility for the customers' that implement the class. 

Thread-safe alternative to `System.IO.File` class:
```csharp
public abstract class TaskFile
{    
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public abstract class TaskDirectory
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual DirectoryInfo CreateDirectory(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

### Versioning Pattern with Abstract Classes

With abstract classes, versioning is handled through version constants. There is no need to create a new class to add methods.

```csharp
public abstract class TaskFile
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    
    // Method added to the class:
    public virtual string ReadAllText(string path, Encoding encoding) => throw new NotImplementedException();
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
        var text = ExecutionContext.File.ReadAllText("file.txt", Encoding.UTF8);
        return true;
    }
}
```

**Question**: How can we check the version compatibility and gracefully fail if the required version is not available? It is not possible in the current set up.

## Methods Reference

### Path Methods

- `bool Exists(string path)`
- `string GetFullPath(string path)`

### File Methods

**TODO**: Generated with Copilot, review that it correctly mirrors all the functions in .NET class that use relative paths:

- `void AppendAllBytes(string path, byte[] bytes)`
- `void AppendAllLines(string path, IEnumerable<string> contents)`
- `void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)`
- `void AppendAllText(string path, string contents)`
- `void AppendAllText(string path, string contents, Encoding encoding)`
- `void AppendAllText(string path, ReadOnlySpan<char> contents)`
- `void AppendAllText(string path, ReadOnlySpan<char> contents, Encoding encoding)`
- `StreamWriter AppendText(string path)`
- `void Copy(string sourceFileName, string destFileName)`
- `void Copy(string sourceFileName, string destFileName, bool overwrite)`
- `FileStream Create(string path)`
- `FileStream Create(string path, int bufferSize)`
- `FileStream Create(string path, int bufferSize, FileOptions options)`
- `StreamWriter CreateText(string path)`
- `void Decrypt(string path)`
- `void Delete(string path)`
- `void Encrypt(string path)`
- `bool Exists(string path)`
- `FileSecurity GetAccessControl(string path)`
- `FileSecurity GetAccessControl(string path, AccessControlSections includeSections)`
- `FileAttributes GetAttributes(string path)`
- `DateTime GetCreationTime(string path)`
- `DateTime GetCreationTimeUtc(string path)`
- `DateTime GetLastAccessTime(string path)`
- `DateTime GetLastAccessTimeUtc(string path)`
- `DateTime GetLastWriteTime(string path)`
- `DateTime GetLastWriteTimeUtc(string path)`
- `void Move(string sourceFileName, string destFileName)`
- `void Move(string sourceFileName, string destFileName, bool overwrite)`
- `FileStream Open(string path, FileMode mode)`
- `FileStream Open(string path, FileMode mode, FileAccess access)`
- `FileStream Open(string path, FileMode mode, FileAccess access, FileShare share)`
- `FileStream OpenRead(string path)`
- `StreamReader OpenText(string path)`
- `FileStream OpenWrite(string path)`
- `byte[] ReadAllBytes(string path)`
- `string[] ReadAllLines(string path)`
- `string[] ReadAllLines(string path, Encoding encoding)`
- `string ReadAllText(string path)`
- `string ReadAllText(string path, Encoding encoding)`
- `IEnumerable<string> ReadLines(string path)`
- `IEnumerable<string> ReadLines(string path, Encoding encoding)`
- `void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)`
- `void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors)`
- `void SetAccessControl(string path, FileSecurity fileSecurity)`
- `void SetAttributes(string path, FileAttributes fileAttributes)`
- `void SetCreationTime(string path, DateTime creationTime)`
- `void SetCreationTimeUtc(string path, DateTime creationTimeUtc)`
- `void SetLastAccessTime(string path, DateTime lastAccessTime)`
- `void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)`
- `void SetLastWriteTime(string path, DateTime lastWriteTime)`
- `void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)`
- `void WriteAllBytes(string path, byte[] bytes)`
- `void WriteAllLines(string path, string[] contents)`
- `void WriteAllLines(string path, IEnumerable<string> contents)`
- `void WriteAllLines(string path, string[] contents, Encoding encoding)`
- `void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)`
- `void WriteAllText(string path, string contents)`
- `void WriteAllText(string path, string contents, Encoding encoding)`

**Note** In net core and framework and in different versions there is different set of the functions. Which exactly we will include
**Idea** We can use info from apisof.net to identify the most used API and we can drop not much used.

### Directory Methods

**TODO**: Generated with Copilot, review that it correctly mirrors all the functions in .NET class that use relative paths:

- `DirectoryInfo CreateDirectory(string path)`
- `void Delete(string path)`
- `void Delete(string path, bool recursive)`
- `IEnumerable<string> EnumerateDirectories(string path)`
- `IEnumerable<string> EnumerateDirectories(string path, string searchPattern)`
- `IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)`
- `IEnumerable<string> EnumerateFiles(string path)`
- `IEnumerable<string> EnumerateFiles(string path, string searchPattern)`
- `IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)`
- `IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)`
- `bool Exists(string path)`
- `DirectorySecurity GetAccessControl(string path)`
- `DirectorySecurity GetAccessControl(string path, AccessControlSections includeSections)`
- `DateTime GetCreationTime(string path)`
- `DateTime GetCreationTimeUtc(string path)`
- `string GetCurrentDirectory()`
- `string[] GetDirectories(string path)`
- `string[] GetDirectories(string path, string searchPattern)`
- `string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)`
- `string GetDirectoryRoot(string path)`
- `string[] GetFiles(string path)`
- `string[] GetFiles(string path, string searchPattern)`
- `string[] GetFiles(string path, string searchPattern, SearchOption searchOption)`
- `string[] GetFileSystemEntries(string path)`
- `string[] GetFileSystemEntries(string path, string searchPattern)`
- `string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)`
- `DateTime GetLastAccessTime(string path)`
- `DateTime GetLastAccessTimeUtc(string path)`
- `DateTime GetLastWriteTime(string path)`
- `DateTime GetLastWriteTimeUtc(string path)`
- `DirectoryInfo GetParent(string path)`
- `void Move(string sourceDirName, string destDirName)`
- `void SetAccessControl(string path, DirectorySecurity directorySecurity)`
- `void SetCreationTime(string path, DateTime creationTime)`
- `void SetCreationTimeUtc(string path, DateTime creationTimeUtc)`
- `void SetCurrentDirectory(string path)`
- `void SetLastAccessTime(string path, DateTime lastAccessTime)`
- `void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)`
- `void SetLastWriteTime(string path, DateTime lastWriteTime)`
- `void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)`

## Notes

**Note**: Our classes should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.
**TODO**: I want to prevent customers from setting or modifying the ExecutionContext, but I don't want to create it during task construction. 

