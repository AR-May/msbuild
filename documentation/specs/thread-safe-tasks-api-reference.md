# Thread-Safe Tasks: API Analysis Reference

This document provides a list of .NET APIs that should not used or should be used with caution in thread-safe tasks. These APIs are problematic because they either rely on or modify process-level state, which can cause race conditions in multithreaded execution.

The APIs listed in this document will be detected by Roslyn analyzer and MSBuild BuildCheck to help identify potential threading issues in tasks that implement `IThreadSafeTask`.

**Note**: The analyzers focus on **static code analysis** and may not catch all dynamic scenarios (such as reflection-based API calls or dynamically loaded assemblies).

## API Issue Categories

The following categories represent the main types of threading issues found in .NET APIs:

1. **File System Operations with Relative Paths**
2. **Global Process State Modification**
   - `System.Environment` class methods that modify environment variables or current directory
   - `System.Diagnostics.Process` class methods that terminate the process
   - `System.Threading.ThreadPool` class methods that modify global thread pool settings
3. **Global Culture Modification**
4. **Culture-Dependent Operations**
5. **Static State**
   - Static fields or properties in custom classes
   - Non-thread-safe global caches

### Best Practices

Instead of the problematic APIs listed below, thread-safe tasks should:

1. **Use `ITaskExecutionContext.FileSystem`** for all file system operations
2. **Use `ITaskExecutionContext.CurrentDirectory`** instead of `Directory.GetCurrentDirectory()` or `Environment.CurrentDirectory`
3. **Use `ITaskExecutionContext.GetEnvironmentVariable()` and `ITaskExecutionContext.SetEnvironmentVariable()`** instead of `Environment` methods
4. **Always use absolute paths** when using standard .NET file system APIs
5. **Use `CultureInfo.InvariantCulture`** for all internal formatting and parsing operations
6. **Use `StringComparison.Ordinal`** for string comparisons
7. **Use thread-safe collections** (`ConcurrentDictionary`, `ConcurrentQueue`, etc.) for shared state
8. **Explicitly configure external processes** with working directory and environment variables
9. **Always Use Explicit Culture**: Specify `CultureInfo.InvariantCulture` for all internal operations
10. **Use Ordinal String Comparisons**: Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase`
11. **Never Modify Process Culture**: Avoid setting `CultureInfo.CurrentCulture` or `CultureInfo.CurrentUICulture`
12. **Localized Output**: Use resource files and explicit culture specification for user messages

### Additional Considerations

#### Assembly Loading

Tasks that load assemblies dynamically in the task host may cause version conflicts. Version conflicts in task assemblies will cause build failures (previously these might have been sporadic). Both dynamically loaded dependencies and static dependencies can cause issues

**Action**: Warn task authors about potential version conflicts and provide guidance on assembly loading best practices.

#### P/Invoke and Native Code

**Concerns**:
- P/Invoke calls may use process-level state like current working directory
- Native code may not be thread-safe
- Native APIs may modify global process state

**Actions**:
- Warn that customers need to review P/Invoke code for thread safety
- Provide documentation links for P/Invoke best practices in multithreaded scenarios
- Recommend using absolute paths in P/Invoke calls that accept file paths
- Consider providing helper methods that use task execution context for common native operations

## Detailed API Reference

The following tables list specific .NET APIs and their threading safety classification:

### System.IO.Path Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `Path.GetFullPath(string path)` | ERROR | Uses current working directory | When path is relative |
| `Path.GetRelativePath(string relativeTo, string path)` | ERROR | Uses current working directory | When relativeTo is relative |

### System.IO.File Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `File.Exists(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadAllText(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadAllText(string path, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllText(string path, string contents)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllText(string path, string contents, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.Copy(string sourceFileName, string destFileName)` | ERROR | Uses current working directory | When either path is relative |
| `File.Copy(string sourceFileName, string destFileName, bool overwrite)` | ERROR | Uses current working directory | When either path is relative |
| `File.Move(string sourceFileName, string destFileName)` | ERROR | Uses current working directory | When either path is relative |
| `File.Move(string sourceFileName, string destFileName, bool overwrite)` | ERROR | Uses current working directory | When either path is relative |
| `File.Delete(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.Create(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.Create(string path, int bufferSize)` | ERROR | Uses current working directory | When path is relative |
| `File.Create(string path, int bufferSize, FileOptions options)` | ERROR | Uses current working directory | When path is relative |
| `File.Open(string path, FileMode mode)` | ERROR | Uses current working directory | When path is relative |
| `File.Open(string path, FileMode mode, FileAccess access)` | ERROR | Uses current working directory | When path is relative |
| `File.Open(string path, FileMode mode, FileAccess access, FileShare share)` | ERROR | Uses current working directory | When path is relative |
| `File.OpenRead(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.OpenWrite(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadAllBytes(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllBytes(string path, byte[] bytes)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadAllLines(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadAllLines(string path, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllLines(string path, string[] contents)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllLines(string path, string[] contents, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllLines(string path, IEnumerable<string> contents)` | ERROR | Uses current working directory | When path is relative |
| `File.WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.AppendAllText(string path, string contents)` | ERROR | Uses current working directory | When path is relative |
| `File.AppendAllText(string path, string contents, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.AppendAllLines(string path, IEnumerable<string> contents)` | ERROR | Uses current working directory | When path is relative |
| `File.AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `File.GetAttributes(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.SetAttributes(string path, FileAttributes fileAttributes)` | ERROR | Uses current working directory | When path is relative |
| `File.GetCreationTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.GetCreationTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.SetCreationTime(string path, DateTime creationTime)` | ERROR | Uses current working directory | When path is relative |
| `File.SetCreationTimeUtc(string path, DateTime creationTimeUtc)` | ERROR | Uses current working directory | When path is relative |
| `File.GetLastAccessTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.GetLastAccessTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.SetLastAccessTime(string path, DateTime lastAccessTime)` | ERROR | Uses current working directory | When path is relative |
| `File.SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)` | ERROR | Uses current working directory | When path is relative |
| `File.GetLastWriteTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.GetLastWriteTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.SetLastWriteTime(string path, DateTime lastWriteTime)` | ERROR | Uses current working directory | When path is relative |
| `File.SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)` | ERROR | Uses current working directory | When path is relative |
| `File.OpenText(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.CreateText(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.AppendText(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadLines(string path)` | ERROR | Uses current working directory | When path is relative |
| `File.ReadLines(string path, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |

### System.IO.Directory Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `Directory.Exists(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.CreateDirectory(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.Delete(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.Delete(string path, bool recursive)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFiles(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFiles(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFiles(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetDirectories(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetDirectories(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetDirectories(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFileSystemEntries(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFileSystemEntries(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFiles(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFiles(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFiles(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateDirectories(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateDirectories(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFileSystemEntries(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFileSystemEntries(string path, string searchPattern)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)` | ERROR | Uses current working directory | When path is relative |
| `Directory.EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions)` | ERROR | Uses current working directory | When path is relative |
| `Directory.Move(string sourceDirName, string destDirName)` | ERROR | Uses current working directory | When either path is relative |
| `Directory.GetCurrentDirectory()` | ERROR | Accesses process-level state | Always problematic |
| `Directory.SetCurrentDirectory(string path)` | ERROR | Modifies process-level state | Always problematic |
| `Directory.GetParent(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetDirectoryRoot(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetCreationTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetCreationTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetCreationTime(string path, DateTime creationTime)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetCreationTimeUtc(string path, DateTime creationTimeUtc)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetLastAccessTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetLastAccessTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetLastAccessTime(string path, DateTime lastAccessTime)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetLastWriteTime(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.GetLastWriteTimeUtc(string path)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetLastWriteTime(string path, DateTime lastWriteTime)` | ERROR | Uses current working directory | When path is relative |
| `Directory.SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)` | ERROR | Uses current working directory | When path is relative |

### System.Environment Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `Environment.SetEnvironmentVariable(string variable, string value)` | ERROR | Modifies process-level state | Always problematic |
| `Environment.SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)` | ERROR | Modifies process-level state | When target is Process |
| `Environment.CurrentDirectory` (getter) | ERROR | Accesses process-level state | Always problematic |
| `Environment.CurrentDirectory` (setter) | ERROR | Modifies process-level state | Always problematic |
| `Environment.Exit(int exitCode)` | ERROR | Terminates entire process | Always problematic |
| `Environment.FailFast(string message)` | ERROR | Terminates entire process | Always problematic |
| `Environment.FailFast(string message, Exception exception)` | ERROR | Terminates entire process | Always problematic |

### System.IO.FileInfo Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `new FileInfo(string fileName)` | ERROR | Uses current working directory | When fileName is relative |

### System.IO.DirectoryInfo Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `new DirectoryInfo(string path)` | ERROR | Uses current working directory | When path is relative |

### System.IO.FileStream Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `new FileStream(string path, FileMode mode)` | ERROR | Uses current working directory | When path is relative |
| `new FileStream(string path, FileMode mode, FileAccess access)` | ERROR | Uses current working directory | When path is relative |
| `new FileStream(string path, FileMode mode, FileAccess access, FileShare share)` | ERROR | Uses current working directory | When path is relative |
| `new FileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)` | ERROR | Uses current working directory | When path is relative |
| `new FileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync)` | ERROR | Uses current working directory | When path is relative |
| `new FileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)` | ERROR | Uses current working directory | When path is relative |

### System.IO Stream Classes

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `new StreamReader(string path)` | ERROR | Uses current working directory | When path is relative |
| `new StreamReader(string path, bool detectEncodingFromByteOrderMarks)` | ERROR | Uses current working directory | When path is relative |
| `new StreamReader(string path, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `new StreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks)` | ERROR | Uses current working directory | When path is relative |
| `new StreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize)` | ERROR | Uses current working directory | When path is relative |
| `new StreamWriter(string path)` | ERROR | Uses current working directory | When path is relative |
| `new StreamWriter(string path, bool append)` | ERROR | Uses current working directory | When path is relative |
| `new StreamWriter(string path, bool append, Encoding encoding)` | ERROR | Uses current working directory | When path is relative |
| `new StreamWriter(string path, bool append, Encoding encoding, int bufferSize)` | ERROR | Uses current working directory | When path is relative |

### System.Diagnostics.Process Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `Process.GetCurrentProcess().Kill()` | ERROR | Terminates entire process | Always problematic |
| `Process.GetCurrentProcess().Kill(bool entireProcessTree)` | ERROR | Terminates entire process | Always problematic |
| `Process.Start(string fileName)` | ERROR | Inherits process state | Inherits environment and working directory |
| `Process.Start(string fileName, string arguments)` | ERROR | Inherits process state | Inherits environment and working directory |
| `Process.Start(ProcessStartInfo startInfo)` | ERROR | May inherit process state | When UseShellExecute=true without explicit settings |

### System.Threading.ThreadPool Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `ThreadPool.SetMinThreads(int workerThreads, int completionPortThreads)` | ERROR | Modifies process-wide settings | Always problematic |
| `ThreadPool.SetMaxThreads(int workerThreads, int completionPortThreads)` | ERROR | Modifies process-wide settings | Always problematic |

### System.Globalization.CultureInfo Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `CultureInfo.CurrentCulture` (setter) | ERROR | Modifies process-wide culture | Always problematic |
| `CultureInfo.CurrentUICulture` (setter) | ERROR | Modifies process-wide culture | Always problematic |
| `CultureInfo.DefaultThreadCurrentCulture` (setter) | ERROR | Affects new threads | Always problematic |
| `CultureInfo.DefaultThreadCurrentUICulture` (setter) | ERROR | Affects new threads | Always problematic |

### System.Threading.Thread Class

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `Thread.CurrentThread.CurrentCulture` (setter) | ERROR | Modifies thread culture | Can affect multi-threaded tasks |
| `Thread.CurrentThread.CurrentUICulture` (setter) | ERROR | Modifies thread culture | Can affect multi-threaded tasks |

### Culture-Dependent String Operations

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `string.ToUpper()` | WARNING | Culture-dependent operation | May produce inconsistent results |
| `string.ToLower()` | WARNING | Culture-dependent operation | May produce inconsistent results |
| `string.Compare(string strA, string strB)` | WARNING | Culture-dependent comparison | May produce inconsistent results |
| `string.Compare(string strA, string strB, bool ignoreCase)` | WARNING | Culture-dependent comparison | May produce inconsistent results |
| `string.CompareTo(string strB)` | WARNING | Culture-dependent comparison | May produce inconsistent results |
| `string.StartsWith(string value)` | WARNING | Culture-dependent operation | Uses current culture by default |
| `string.EndsWith(string value)` | WARNING | Culture-dependent operation | Uses current culture by default |
| `string.IndexOf(string value)` | WARNING | Culture-dependent operation | Uses current culture by default |
| `string.LastIndexOf(string value)` | WARNING | Culture-dependent operation | Uses current culture by default |
| `string.Contains(string value)` | WARNING | Culture-dependent operation | Uses current culture in some overloads |
| `Array.Sort(string[])` | WARNING | Culture-dependent sorting | May produce inconsistent results |
| `Array.Sort<T>(T[])` | WARNING | Culture-dependent sorting | When T is string |
| `List<string>.Sort()` | WARNING | Culture-dependent sorting | May produce inconsistent results |

### Culture-Dependent Formatting and Parsing

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| `byte.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `sbyte.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `short.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `ushort.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `int.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `uint.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `long.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `ulong.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `float.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `double.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `decimal.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `DateTime.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `DateTimeOffset.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `TimeSpan.ToString()` | WARNING | Culture-dependent formatting | May produce inconsistent results |
| `byte.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `sbyte.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `short.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `ushort.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `int.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `uint.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `long.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `ulong.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `float.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `double.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `decimal.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `DateTime.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `DateTimeOffset.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `TimeSpan.Parse(string s)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `Convert.ToInt32(string value)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `Convert.ToDouble(string value)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `Convert.ToDecimal(string value)` | WARNING | Culture-dependent parsing | May produce inconsistent results |
| `Convert.ToDateTime(string value)` | WARNING | Culture-dependent parsing | May produce inconsistent results |

### Static

| API | Level | Short Reason | Notes |
|-----|-------|--------------|-------|
| Static fields | WARNING | Shared across threads | Can cause race conditions |

## Safe Alternatives and Recommendations

### File System Operations
- Use `ITaskExecutionContext.FileSystem` for all file system operations
- Ensure all paths are absolute when using standard .NET file system APIs
- Use `ITaskExecutionContext.CurrentDirectory` instead of `Directory.GetCurrentDirectory()`

### Environment Variables
- Use `ITaskExecutionContext.GetEnvironmentVariable()` instead of `Environment.GetEnvironmentVariable()`
- Use `ITaskExecutionContext.SetEnvironmentVariable()` instead of `Environment.SetEnvironmentVariable()`

### Process Creation
- Always explicitly set `ProcessStartInfo.WorkingDirectory`
- Always explicitly set `ProcessStartInfo.Environment` or `ProcessStartInfo.EnvironmentVariables`
- Set `ProcessStartInfo.UseShellExecute = false` for better control

### Cultural Operations
- Use `CultureInfo.InvariantCulture` for all internal formatting and parsing
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string comparisons
- Specify culture explicitly: `value.ToString(CultureInfo.InvariantCulture)`
- Use explicit culture for parsing: `int.Parse(text, CultureInfo.InvariantCulture)`

### Shared State
- Use thread-safe collections (`ConcurrentDictionary<K,V>`, `ConcurrentQueue<T>`, etc.)
- Use immutable data structures
- Use instance-based state through `ITaskExecutionContext` instead of static state
- Use proper synchronization primitives (`lock`, `Mutex`, `Semaphore`) when shared state is necessary
