# Thread Safe Tasks

## Overview

In the traditional MSBuild execution model, tasks operate under the assumption that they have exclusive control over the entire process during execution. This allows them to freely modify global process state, including environment variables, the current working directory, and other process-level resources. This design was well-suited for MSBuild's historical approach of using separate processes for parallel execution.

However, with the introduction of multithreaded MSBuild execution mode, multiple tasks can now run concurrently within the same process. This change requires a new approach to task design to prevent race conditions and ensure thread safety. To enable tasks to opt into this multithreaded execution model, we introduce a new interface that tasks should implement and utilize to declare their thread-safety.

Tasks that implement the following `IThreadSafeTask` interface should avoid using APIs that modify global process state or rely on process-level state that could cause conflicts when multiple tasks execute simultaneously. For a list of such APIs and their safe alternatives, refer to [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

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

## Alternative Approach: Simple Interfaces

**Note**: The interfaces below represent a simpler approach, but they have the cascading versioning problem you mentioned. They are kept here for reference and comparison with the generic pattern above.

## ITaskExecutionContext Interface

The `ITaskExecutionContext` provides tasks with access to execution environment information that was previously accessed through global process state:

```csharp
/// <summary>
/// Provides access to task execution context and environment information.
/// </summary>
public interface ITaskExecutionContext
{    
    string CurrentDirectory { get; set; }

    IEnvironment Environment { get; }

    IFileSystem FileSystem { get; }
}
```

### Questions and Notes:
1. ~~Should we flatten the interface? Avoid IFileSystem and place them in the ITaskExecutionContext, and/or remove IPath, IFile, IDirectory?~~ The generic pattern above addresses this by making the structure flexible.
1. ~~Should we consider using classes?~~ The generic pattern works well with interfaces and provides better testability and flexibility.

## IEnvironment Interface

The `IEnvironment` provides thread-safe access to environment variables:

```csharp
public interface IEnvironment
{
    string? GetEnvironmentVariable(string name);
    
    Dictionary<string, string?> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
}
```

## ITaskContextFileSystem Interface

```csharp
/// <summary>
/// Context-aware File System. All Path/File/Directory calls should be used through it.
/// Automatically uses the current working directory from the execution context.
/// </summary>
public interface IFileSystem
{
    IPath Path { get; }
    
    IFile File { get; }
    
    IDirectory Directory { get; }
}
```

### Questions and Notes:
1. ~~Should we flatten the interface? Avoid IFileSystem and place them in the ITaskExecutionContext, and/or remove IPath, IFile, IDirectory?~~ The generic pattern addresses this concern.
1. ~~Should we consider using classes?~~ The generic pattern works well with interfaces.

## IPath Interface

Thread-safe alternative to `System.IO.Path` class:

```csharp
public interface IPath
{
    string GetFullPath(string path);
}
```

## IFile Interface

Thread-safe alternative to `System.IO.File` class:

**TODO** Generated with copilot, look that it correctly mirrors all the functions in .NET class that use relative paths.

```csharp
public interface IFile
{
    bool Exists(string path);
    
    string ReadAllText(string path);
    
    string ReadAllText(string path, Encoding encoding);
    
    byte[] ReadAllBytes(string path);
    
    string[] ReadAllLines(string path);
    
    string[] ReadAllLines(string path, Encoding encoding);
    
    IEnumerable<string> ReadLines(string path);
    
    IEnumerable<string> ReadLines(string path, Encoding encoding);
    
    void WriteAllText(string path, string contents);
    
    void WriteAllText(string path, string contents, Encoding encoding);
    
    void WriteAllBytes(string path, byte[] bytes);
    
    void WriteAllLines(string path, string[] contents);
    
    void WriteAllLines(string path, IEnumerable<string> contents);
    
    void WriteAllLines(string path, string[] contents, Encoding encoding);
    
    void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding);
    
    void AppendAllText(string path, string contents);
    
    void AppendAllText(string path, string contents, Encoding encoding);
    
    void AppendAllLines(string path, IEnumerable<string> contents);
    
    void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding);
    
    void Copy(string sourceFileName, string destFileName);

    void Copy(string sourceFileName, string destFileName, bool overwrite);
    
    void Move(string sourceFileName, string destFileName);
    
    void Move(string sourceFileName, string destFileName, bool overwrite);
    
    void Delete(string path);
    
    FileAttributes GetAttributes(string path);
    
    void SetAttributes(string path, FileAttributes fileAttributes);
    
    DateTime GetCreationTime(string path);
    
    DateTime GetCreationTimeUtc(string path);
    
    DateTime GetLastAccessTime(string path);
    
    DateTime GetLastAccessTimeUtc(string path);
    
    DateTime GetLastWriteTime(string path);
    
    DateTime GetLastWriteTimeUtc(string path);
    
    void SetCreationTime(string path, DateTime creationTime);

    void SetCreationTimeUtc(string path, DateTime creationTimeUtc);
    
    void SetLastAccessTime(string path, DateTime lastAccessTime);
    
    void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
    
    void SetLastWriteTime(string path, DateTime lastWriteTime);
    
    void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);
    
    FileStream OpenRead(string path);
    
    FileStream OpenWrite(string path);
    
    FileStream Open(string path, FileMode mode);
    
    FileStream Open(string path, FileMode mode, FileAccess access);
    
    FileStream Open(string path, FileMode mode, FileAccess access, FileShare share);
    
    FileStream Create(string path);
    
    FileStream Create(string path, int bufferSize);
    
    FileStream Create(string path, int bufferSize, FileOptions options);
    
    StreamReader OpenText(string path);
    
    StreamWriter CreateText(string path);
    
    StreamWriter AppendText(string path);
    
    void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName);
    
    void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);
}
```

## IDirectory Interface

Thread-safe alternative to `System.IO.Directory` class:

**TODO** Generated with Copilot, look that it correctly mirrors all the functions in .NET class that use relative paths.

```csharp
public interface IDirectory
{
    bool Exists(string path);
    
    DirectoryInfo CreateDirectory(string path);
    
    void Delete(string path);
    
    void Delete(string path, bool recursive);
    
    void Move(string sourceDirName, string destDirName);
    
    string[] GetFiles(string path);
    
    string[] GetFiles(string path, string searchPattern);
    
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    
    string[] GetDirectories(string path);
    
    string[] GetDirectories(string path, string searchPattern);
    
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
    
    string[] GetFileSystemEntries(string path);
    
    string[] GetFileSystemEntries(string path, string searchPattern);
    
    string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateFiles(string path);
    
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateDirectories(string path);
    
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern);
    
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
    
    DateTime GetCreationTime(string path);
    
    DateTime GetCreationTimeUtc(string path);
    
    DateTime GetLastAccessTime(string path);
    
    DateTime GetLastAccessTimeUtc(string path);
    
    DateTime GetLastWriteTime(string path);
    
    DateTime GetLastWriteTimeUtc(string path);
    
    void SetCreationTime(string path, DateTime creationTime);
    
    void SetCreationTimeUtc(string path, DateTime creationTimeUtc);
    
    void SetLastAccessTime(string path, DateTime lastAccessTime);
    
    void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
    
    void SetLastWriteTime(string path, DateTime lastWriteTime);
    
    void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);
    
    DirectoryInfo GetParent(string path);
    
    string GetDirectoryRoot(string path);
    
    string GetCurrentDirectory();
    
    void SetCurrentDirectory(string path);
}
```

## Generic Versioning Pattern (Recommended Solution)

To avoid the cascading versioning problem when `IFile` needs to be upgraded to `IFile2`, we can use a generic pattern similar to the one used with `BuildSubmissionBase` in MSBuild:

### Base Interfaces (Non-Generic)

```csharp
/// <summary>
/// Base interface for tasks that support multithreaded execution in MSBuild.
/// </summary>
public interface IThreadSafeTask : ITask
{
    /// <summary>
    /// Execution context for the task, providing thread-safe
    /// access to environment variables, working directory, and other build context.
    /// This property will be set by the MSBuild engine before execution is called.
    /// </summary>
    ITaskExecutionContextBase ExecutionContext { get; set; }
}

/// <summary>
/// Base interface for task execution context.
/// </summary>
public interface ITaskExecutionContextBase
{
    string CurrentDirectory { get; set; }
    IEnvironment Environment { get; }
    IFileSystemBase FileSystem { get; }
}

/// <summary>
/// Base interface for file system access.
/// </summary>
public interface IFileSystemBase
{
    IPathBase Path { get; }
    IFileBase File { get; }
    IDirectoryBase Directory { get; }
}

/// <summary>
/// Base interface for file operations.
/// </summary>
public interface IFileBase
{
    bool Exists(string path);
    string ReadAllText(string path);
    // ... core methods that rarely change
}

/// <summary>
/// Base interface for path operations.
/// </summary>
public interface IPathBase
{
    string GetFullPath(string path);
}

/// <summary>
/// Base interface for directory operations.
/// </summary>
public interface IDirectoryBase
{
    bool Exists(string path);
    DirectoryInfo CreateDirectory(string path);
    // ... core methods that rarely change
}
```

### Generic Interfaces (Version-Aware)

```csharp
/// <summary>
/// Generic task execution context that allows for versioned components.
/// </summary>
public interface ITaskExecutionContext<TPath, TFile, TDirectory> : ITaskExecutionContextBase
    where TPath : IPathBase
    where TFile : IFileBase  
    where TDirectory : IDirectoryBase
{
    new IFileSystem<TPath, TFile, TDirectory> FileSystem { get; }
}

/// <summary>
/// Generic file system interface that can work with different versions of file/path/directory interfaces.
/// </summary>
public interface IFileSystem<TPath, TFile, TDirectory> : IFileSystemBase
    where TPath : IPathBase
    where TFile : IFileBase
    where TDirectory : IDirectoryBase
{
    new TPath Path { get; }
    new TFile File { get; }
    new TDirectory Directory { get; }
}
```

### Versioned Interfaces

```csharp
/// <summary>
/// Version 1 of file interface (current).
/// </summary>
public interface IFile : IFileBase
{
    string ReadAllText(string path, Encoding encoding);
    byte[] ReadAllBytes(string path);
    // ... existing methods from current spec
}

/// <summary>
/// Version 2 of file interface (future - adds new methods without breaking existing code).
/// </summary>
public interface IFile2 : IFile
{
    // New methods added in version 2
    Task<string> ReadAllTextAsync(string path);
    Task<string> ReadAllTextAsync(string path, Encoding encoding);
    Task<byte[]> ReadAllBytesAsync(string path);
    // ... other new async methods
}

/// <summary>
/// Version 1 of path interface.
/// </summary>
public interface IPath : IPathBase
{
    string GetDirectoryName(string path);
    string GetFileName(string path);
    // ... existing methods
}

/// <summary>
/// Version 1 of directory interface.
/// </summary>
public interface IDirectory : IDirectoryBase
{
    void Delete(string path);
    void Delete(string path, bool recursive);
    // ... existing methods
}
```

### Type Aliases for Current Version

```csharp
/// <summary>
/// Current version of task execution context.
/// </summary>
public interface ITaskExecutionContext : ITaskExecutionContext<IPath, IFile, IDirectory>
{
}

/// <summary>
/// Current version of file system.
/// </summary>  
public interface IFileSystem : IFileSystem<IPath, IFile, IDirectory>
{
}
```

### Usage Examples

```csharp
// Current tasks use the non-generic interfaces (which map to current versions)
public class MyTask : IThreadSafeTask
{
    public ITaskExecutionContextBase ExecutionContext { get; set; }
    
    public bool Execute()
    {
        // Works with current version
        var content = ((ITaskExecutionContext)ExecutionContext).FileSystem.File.ReadAllText("file.txt");
        return true;
    }
}

// Advanced tasks that need specific versions can be explicit
public class AdvancedTask : Task
{
    public ITaskExecutionContext<IPath, IFile2, IDirectory> ExecutionContext { get; set; }
    
    public override bool Execute()
    {
        // Uses version 2 file interface with async methods
        var content = await ExecutionContext.FileSystem.File.ReadAllTextAsync("file.txt");
        return true;
    }
}

// MSBuild engine implementation can provide the appropriate version
public class TaskExecutionContextV2 : ITaskExecutionContext<IPath, IFile2, IDirectory>
{
    public string CurrentDirectory { get; set; }
    public IEnvironment Environment { get; }
    public IFileSystem<IPath, IFile2, IDirectory> FileSystem { get; }
    
    // Explicit interface implementations for base interfaces
    IFileSystemBase ITaskExecutionContextBase.FileSystem => FileSystem;
}
```

### Benefits of This Pattern

1. **No Cascading Updates**: When `IFile2` is introduced, existing interfaces (`ITaskExecutionContext`, `IFileSystem`) don't need to be versioned.

2. **Backward Compatibility**: Existing tasks continue to work without modification.

3. **Forward Compatibility**: New tasks can opt into newer interface versions explicitly.

4. **Flexible Mixing**: Tasks can mix and match interface versions (e.g., use `IFile2` with `IPath` v1).

5. **Type Safety**: Compile-time checking ensures compatible interface versions are used together.

### Migration Path

1. **Phase 1**: Introduce base interfaces and current versioned interfaces
2. **Phase 2**: Update MSBuild engine to provide generic implementations  
3. **Phase 3**: When new features are needed, add `IFile2`, `IPath2`, etc. without touching higher-level interfaces
4. **Phase 4**: Tasks can gradually opt into newer versions as needed

This approach eliminates the cascading versioning problem while maintaining full backward compatibility and providing a clear upgrade path for the future.

## Notes

**Note**: Our classes should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.
