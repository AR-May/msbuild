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

The `ITaskExecutionContext` provides tasks with access to what was global process state in multi-process mode, such as environment variables and working directory:
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
    
    Dictionary<string, string> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
}
```

Thread-safe alternative to `System.IO.Path` class:
```csharp
public interface IPath
{
    bool Exists(string path);
    string GetFullPath(string path);
    // Additional path manipulation methods
}
```

Thread-safe alternative to `System.IO.File` class:

```csharp
public interface IFile
{
    bool Exists(string path);
    string ReadAllText(string path);
    // Additional file manipulation methods
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public interface IDirectory
{
    bool Exists(string path);
    DirectoryInfo CreateDirectory(string path);
    // Additional directory manipulation methods
}
```

### Interface Versioning Pattern

To handle future updates to interfaces without breaking existing implementations, we will use a versioning pattern. 

```csharp
public interface IFile2 : IFile
{
    string ReadAllText(string path, Encoding encoding)
    ... // Other new methods added in version 2
}
```

Unfortunately, `ITaskExecutionContext` will need a version update as well.
```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    new IPath2 Path { get; }
}
```

### Usage Examples

```csharp
// Tasks should use the minimum `ITaskExecutionContext` version that provides the needed functionality
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
**Note:** During the loading of the task assembly, we can check whether the needed version of the `ITaskExecutionContext` is present and gracefully fail if not. 

**Note:** Consider backporting this check to 17.14 branch as well.

## Option 2: Abstract Classes

This approach uses abstract classes instead of interfaces.

```csharp
public interface IThreadSafeTask : ITask
{
    TaskExecutionContext ExecutionContext { get; set; }
}
```

### TaskExecutionContext Abstract Class

The `TaskExecutionContext` provides tasks with access to what was global process state in multi-process mode, such as environment variables and working directory:

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
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string GetFullPath(string path) => throw new NotImplementedException();
    // Additional path manipulation methods
}
```

**Note:** The default implementations allow backward compatibility for customers that implement the class. 

Thread-safe alternative to `System.IO.File` class:
```csharp
public abstract class TaskFile
{    
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    // Additional file manipulation methods
}
```

Thread-safe alternative to `System.IO.Directory` class:
```csharp
public abstract class TaskDirectory
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual DirectoryInfo CreateDirectory(string path) => throw new NotImplementedException();
    // Additional directory manipulation methods
}
```

### Versioning Pattern

With abstract classes, there is no need to create a new type to add methods.

```csharp
public abstract class TaskFile
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    
    // Method added to the class:
    public virtual string ReadAllText(string path, Encoding encoding) => throw new NotImplementedException();
    // Additional methods can be added here
}
```

**Question:** How can we check the version compatibility and gracefully fail if the required version is not available? It is not possible in the current design.
**Note:** Consider adding to 17.14 branch a check whether the Task is an `IThreadSafeTask` and fail gracefully if so.


### Usage Examples

```csharp
public class MyTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt");
        return true;
    }
}

// Tasks that need newer functionality
public class AdvancedTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt", Encoding.UTF8);
        return true;
    }
}
```

## Option 3: Flat Interfaces with Strongly-Typed Paths

This approach eliminates the need for most file system APIs by using strongly-typed path classes with implicit conversions and enforcing their usage with file system APIs through a Roslyn analyzer. 

```csharp
public interface IThreadSafeTask : ITask
{
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

### Minimal Execution Context

The execution context provides only essential methods:

```csharp
public interface ITaskExecutionContext
{
    AbsolutePath CurrentDirectory { get; set; }

    string? GetEnvironmentVariable(string name);

    Dictionary<string, string> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
    
    AbsolutePath GetAbsolutePath(string path);
}
```

```csharp
public sealed class AbsolutePath
{
    public string Path { get; }
    
    // Will be banned in tasks by analyzers.
    public AbsolutePath(string path)
    {
        // Should we do that or should we believe the developer when passing absolute paths?
        Path = System.IO.Path.GetFullPath(path); 
    }

    public AbsolutePath(string path, ITaskExecutionContext context)
    {
        Path = context.GetAbsolutePath(path).Path;
    }
    
    public AbsolutePath(string path, AbsolutePath basePath)
    {
        Path = System.IO.Path.Combine(basePath.Path, path);
    }
    
    public static implicit operator string(AbsolutePath path) => path.Path;
}

public sealed class RelativePath
{
    public string Path { get; }
    
    public RelativePath(string path)
    {
        // Should we do that or should we believe the developer when passing relative paths?
        if (System.IO.Path.IsPathRooted(path))
            throw new ArgumentException("Path must be relative", nameof(path));
        Path = path;
    }
    
    public AbsolutePath ToAbsolute(AbsolutePath basePath) => 
        new AbsolutePath(System.IO.Path.Combine(basePath.Path, Path));
    
    public static implicit operator string(RelativePath path) => path.Path;
}
```

**Benefits:**
- Most file system operations are eliminated from interfaces.
- Compile-time distinction between absolute and relative paths prevents common path-related bugs
- Existing string-based APIs work seamlessly through implicit conversions

Drawbacks:
- Will need to adjust if this concept would ever be implemented in standard .NET API.  

## Backward Compatibility of Thread-Safe Tasks.

Task authors will need a way to publish NuGet packages that include MSBuild tasks capable of running multithreaded in newer MSBuild versions and remaining compatible with older MSBuild versions that don't support the `IThreadSafeTask` interface.

### Potential Solutions

#### Option 1: MSBuild Version-Specific Task Assemblies

Use MSBuild properties to conditionally load different task implementations based on MSBuild capabilities:

```xml
<!-- MyTask.targets -->
<Project>
  
  <!-- Detect MSBuild version capabilities -->
  <PropertyGroup>
    <MSBuildSupportsThreadSafeTasks Condition="'$(MSBuildVersion)' >= '17.15'">true</MSBuildSupportsThreadSafeTasks>
    <MSBuildSupportsThreadSafeTasks Condition="'$(MSBuildSupportsThreadSafeTasks)' == ''">false</MSBuildSupportsThreadSafeTasks>
  </PropertyGroup>

  <!-- Load appropriate task assembly based on MSBuild capabilities -->
  <UsingTask 
    TaskName="MyTask" 
    AssemblyFile="$(MSBuildThisFileDirectory)../lib/netstandard2.0/MyTask.ThreadSafe.dll" 
    Condition="'$(MSBuildSupportsThreadSafeTasks)' == 'true'" />
    
  <UsingTask 
    TaskName="MyTask" 
    AssemblyFile="$(MSBuildThisFileDirectory)../lib/netstandard2.0/MyTask.dll" 
    Condition="'$(MSBuildSupportsThreadSafeTasks)' == 'false'" />

</Project>
```

**Disadvantages:**
- Larger package size due to multiple assemblies
- More complex build setup requiring multiple builds

##### Option 1.1 Conditional Compilation

The most straightforward approach creates separate assemblies for different MSBuild package versions.

Example:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>$(MSBuildProjectName)</AssemblyName>
  </PropertyGroup>
  
  <ItemGroup Condition="'$(MSBuildPackageVersion)' == 'Legacy'">
    <!-- Minimum version for the implementation that does not support thread-safe tasks. -->
    <PackageReference Include="Microsoft.Build.Utilities.Core" Version="15.9.20" /> 
  </ItemGroup>

  <PropertyGroup Condition="'$(MSBuildPackageVersion)' == 'Modern'">
    <AssemblyName>$(MSBuildProjectName).Modern</AssemblyName>
    <DefineConstants>$(DefineConstants);MSBUILD_THREAD_SAFE</DefineConstants>
  </PropertyGroup>
  
  <ItemGroup Condition="'$(MSBuildPackageVersion)' == 'Modern'">
    <!-- Minimum version for the implementation that supports thread-safe tasks.  -->
    <PackageReference Include="Microsoft.Build.Utilities.Core" Version="17.15.0" /> 
  </ItemGroup>
</Project>
```

```csharp
public class MyTask : Task
#if MSBUILD_THREAD_SAFE
    , IThreadSafeTask
#endif
{
    public override bool Execute()
    {
#if MSBUILD_THREAD_SAFE
        // Use thread-safe ExecutionContext APIs 
#endif
        // Fallback to standard APIs for older MSBuild
    }
}
```

**Disadvantages:**
- Code duplication - authors will need to support two task implementations.

#### Option 1.2: Shim DLL.

We can create a shim assembly that defines the thread-safe interfaces for older MSBuild versions to avoid task code duplication. 
Task authors would need to include it when building for older MSBuild. 

```csharp
public class MyTask : Task, IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public override bool Execute()
    {
        // If ExecutionContext is null (older MSBuild), create fallback implementation
        if (ExecutionContext == null)
        {
            ExecutionContext = new TaskExecutionContext();
        }

        // Use ExecutionContext APIs consistently - works with both real and fallback implementations
    }
}
```

**Disadvantages:**
- Performance overhead for loading the shim DLL
- Versioning issues for shim DLLs.

#### Option 1.3: Code Generation

Use build-time code generation to create implementations in the task assembly.

**Disadvantages:**
- Requires code generation tooling.
- Versioning issue still stays, or, if we generate the shim code in its own namespaces, more code to load from task assemblies.

#### Option 1.4: Runtime Detection and Reflection

Use reflection to detect thread-safe capabilities at runtime in the task itself.
TODO: Think if it is possible to reach the correct ExecutionContext that should apply to the task.

### Option 2: Conditional Loading of Shim Dll
Authors will always produce the code using the shim assembly. However, during the task loading, on the newer versions of MSBuild the loading of the shim dll is silently skipped, since msbuild already has those classes. 

## Methods Reference

### Path Methods

- `bool Exists(string path)`
- `string GetFullPath(string path)`

### File Methods

**Question** In net core and net framework (and in different versions) there is different set of the functions. Which exactly should we take?

**Idea** We can use info from apisof.net to identify the most used API and we can drop not much used.

The following table lists System.IO.File APIs that may take relative paths as parameters:

| Signature | Available | Include? |
|-----------|-----------|----------|
| void AppendAllBytes(string path, byte[] bytes) | net10 | Yes |
| Task AppendAllBytes(string path, ReadOnlyMemory<byte> bytes) | net10 | No |
| void AppendAllLines(string path, IEnumerable<string> contents) | netstandard2.0 | Yes |
| void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding) | netstandard2.0 | Yes |
| void AppendAllText(string path, string contents) | netstandard2.0 | Yes |
| void AppendAllText(string path, string contents, Encoding encoding) | netstandard2.0 | Yes |
| void AppendAllText(string path, ReadOnlySpan<char> contents) | net10 | No |
| void AppendAllText(string path, ReadOnlySpan<char> contents, Encoding encoding) | net10 | No |
| StreamWriter AppendText(string path) | netstandard2.0 | Yes |
| void Copy(string sourceFileName, string destFileName) | netstandard2.0 | Yes |
| void Copy(string sourceFileName, string destFileName, bool overwrite) | netstandard2.0 | Yes |
| FileStream Create(string path) | netstandard2.0 | Yes |
| FileStream Create(string path, int bufferSize) | netstandard2.0 | No |
| FileStream Create(string path, int bufferSize, FileOptions options) | netstandard2.0 | No |
| FileStream Create(string path, int bufferSize, FileOptions options, FileSecurity fileSecurity) | net472 | No |
| FileSystemInfo CreateSymbolicLink(string path, string pathToTarget) | net10 | No |
| StreamWriter CreateText(string path) | netstandard2.0 | Yes |
| void Decrypt(string path) | netstandard2.0 | No |
| void Delete(string path) | netstandard2.0 | Yes |
| void Encrypt(string path) | netstandard2.0 | No |
| bool Exists(string path) | netstandard2.0 | Yes |
| FileSecurity GetAccessControl(string path) | net472 | No |
| FileSecurity GetAccessControl(string path, AccessControlSections includeSections) | net472 | No |
| FileAttributes GetAttributes(string path) | netstandard2.0 | Yes |
| DateTime GetCreationTime(string path) | netstandard2.0 | Yes |
| DateTime GetCreationTimeUtc(string path) | netstandard2.0 | Yes |
| DateTime GetLastAccessTime(string path) | netstandard2.0 | Yes |
| DateTime GetLastAccessTimeUtc(string path) | netstandard2.0 | Yes |
| DateTime GetLastWriteTime(string path) | netstandard2.0 | Yes |
| DateTime GetLastWriteTimeUtc(string path) | netstandard2.0 | Yes |
| UnixFileMode GetUnixFileMode(string path) | net10 | No |
| void Move(string sourceFileName, string destFileName) | netstandard2.0 | Yes |
| void Move(string sourceFileName, string destFileName, bool overwrite) | net10 | Yes |
| FileStream Open(string path, FileMode mode) | netstandard2.0 | Yes |
| FileStream Open(string path, FileMode mode, FileAccess access) | netstandard2.0 | Yes |
| FileStream Open(string path, FileMode mode, FileAccess access, FileShare share) | netstandard2.0 | Yes |
| FileStream Open(string path, FileStreamOptions options) | net10 | No |
| SafeFileHandle OpenHandle(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options, long preallocationSize) | net10 | No |
| FileStream OpenRead(string path) | netstandard2.0 | Yes |
| StreamReader OpenText(string path) | netstandard2.0 | Yes |
| FileStream OpenWrite(string path) | netstandard2.0 | Yes |
| byte[] ReadAllBytes(string path) | netstandard2.0 | Yes |
| string[] ReadAllLines(string path) | netstandard2.0 | Yes |
| string[] ReadAllLines(string path, Encoding encoding) | netstandard2.0 | Yes |
| string ReadAllText(string path) | netstandard2.0 | Yes |
| string ReadAllText(string path, Encoding encoding) | netstandard2.0 | Yes |
| IEnumerable<string> ReadLines(string path) | netstandard2.0 | Yes |
| IEnumerable<string> ReadLines(string path, Encoding encoding) | netstandard2.0 | Yes |
| void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName) | netstandard2.0 | Yes |
| void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors) | netstandard2.0 | Yes |
| FileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget) | net10 | No |
| void SetAccessControl(string path, FileSecurity fileSecurity) | net472 | No |
| void SetAttributes(string path, FileAttributes fileAttributes) | netstandard2.0 | Yes |
| void SetCreationTime(string path, DateTime creationTime) | netstandard2.0 | Yes |
| void SetCreationTimeUtc(string path, DateTime creationTimeUtc) | netstandard2.0 | Yes |
| void SetLastAccessTime(string path, DateTime lastAccessTime) | netstandard2.0 | Yes |
| void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) | netstandard2.0 | Yes |
| void SetLastWriteTime(string path, DateTime lastWriteTime) | netstandard2.0 | Yes |
| void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) | netstandard2.0 | Yes |
| void SetUnixFileMode(string path, UnixFileMode mode) | net10 | No |
| void WriteAllBytes(string path, byte[] bytes) | netstandard2.0 | Yes |
| void WriteAllBytes(string path, ReadOnlySpan<byte> bytes) | net10 | No |
| void WriteAllLines(string path, IEnumerable<string> contents) | netstandard2.0 | Yes |
| void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding) | netstandard2.0 | Yes |
| void WriteAllLines(string path, string[] contents) | netstandard2.0 | Yes |
| void WriteAllLines(string path, string[] contents, Encoding encoding) | netstandard2.0 | Yes |
| void WriteAllText(string path, string contents) | netstandard2.0 | Yes |
| void WriteAllText(string path, string contents, Encoding encoding) | netstandard2.0 | Yes |
| void WriteAllText(string path, ReadOnlySpan<char> contents) | net10 | No |
| void WriteAllText(string path, ReadOnlySpan<char> contents, Encoding encoding) | net10 | No |

**Warning**: Async methods and SafeFileHandle-based methods are excluded from this spec but could be added in future versions.

### Directory Methods

The following table lists System.IO.Directory APIs that may take relative paths as parameters:

| Signature | Available | Include? |
|-----------|-----------|----------|
| DirectoryInfo CreateDirectory(string path) | netstandard2.0 | Yes |
| DirectoryInfo CreateDirectory(string path, DirectorySecurity directorySecurity) | net472 | No |
| DirectoryInfo CreateDirectory(string path, UnixFileMode unixCreateMode) | net10 | No |
| FileSystemInfo CreateSymbolicLink(string path, string pathToTarget) | net10 | No |
| DirectoryInfo CreateTempSubdirectory(string prefix) | net10 | No |
| void Delete(string path) | netstandard2.0 | Yes |
| void Delete(string path, bool recursive) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateDirectories(string path) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateDirectories(string path, string searchPattern) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| IEnumerable<string> EnumerateFiles(string path) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateFiles(string path, string searchPattern) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| IEnumerable<string> EnumerateFileSystemEntries(string path) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) | netstandard2.0 | Yes |
| IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| bool Exists(string path) | netstandard2.0 | Yes |
| DirectorySecurity GetAccessControl(string path) | net472 | No |
| DirectorySecurity GetAccessControl(string path, AccessControlSections includeSections) | net472 | No |
| DateTime GetCreationTime(string path) | netstandard2.0 | Yes |
| DateTime GetCreationTimeUtc(string path) | netstandard2.0 | Yes |
| string GetCurrentDirectory() | netstandard2.0 | Yes |
| string[] GetDirectories(string path) | netstandard2.0 | Yes |
| string[] GetDirectories(string path, string searchPattern) | netstandard2.0 | Yes |
| string[] GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| string GetDirectoryRoot(string path) | netstandard2.0 | No |
| string[] GetFiles(string path) | netstandard2.0 | Yes |
| string[] GetFiles(string path, string searchPattern) | netstandard2.0 | Yes |
| string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| string[] GetFiles(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| string[] GetFileSystemEntries(string path) | netstandard2.0 | Yes |
| string[] GetFileSystemEntries(string path, string searchPattern) | netstandard2.0 | Yes |
| string[] GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) | net10 | No |
| string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) | netstandard2.0 | No |
| DateTime GetLastAccessTime(string path) | netstandard2.0 | Yes |
| DateTime GetLastAccessTimeUtc(string path) | netstandard2.0 | Yes |
| DateTime GetLastWriteTime(string path) | netstandard2.0 | Yes |
| DateTime GetLastWriteTimeUtc(string path) | netstandard2.0 | Yes |
| DirectoryInfo GetParent(string path) | netstandard2.0 | Yes |
| void Move(string sourceDirName, string destDirName) | netstandard2.0 | Yes |
| FileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget) | net10 | No |
| void SetAccessControl(string path, DirectorySecurity directorySecurity) | net472 | No |
| void SetCreationTime(string path, DateTime creationTime) | netstandard2.0 | Yes |
| void SetCreationTimeUtc(string path, DateTime creationTimeUtc) | netstandard2.0 | Yes |
| void SetCurrentDirectory(string path) | netstandard2.0 | Yes |
| void SetLastAccessTime(string path, DateTime lastAccessTime) | netstandard2.0 | Yes |
| void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) | netstandard2.0 | Yes |
| void SetLastWriteTime(string path, DateTime lastWriteTime) | netstandard2.0 | Yes |
| void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) | netstandard2.0 | Yes |

## Notes

**Note**: Our implementations should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.
