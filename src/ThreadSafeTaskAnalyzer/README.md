# Microsoft.Build.ThreadSafeTaskAnalyzer

This is a Roslyn analyzer that checks MSBuild tasks implementing `IMultiThreadableTask` or marked with `MSBuildMultiThreadableTaskAttribute` for thread-safety violations.

## Overview

When tasks are marked as thread-safe for multithreaded MSBuild execution, they must avoid using APIs that rely on or modify process-level state. This analyzer helps identify such violations at compile time.

## Diagnostics

| ID | Title | Severity | Description |
|----|-------|----------|-------------|
| MSB0001 | Do not modify environment variables | Error | Detects `Environment.SetEnvironmentVariable` and related methods |
| MSB0002 | Do not access current directory | Error | Detects `Environment.CurrentDirectory` usage |
| MSB0003 | Do not terminate process | Error | Detects `Environment.Exit` and `FailFast` calls |
| MSB0004 | Do not use Path.GetFullPath | Error | Detects `Path.GetFullPath` which uses current working directory |
| MSB0005 | Use absolute paths | Warning | Detects file system APIs that may use relative paths |
| MSB0006 | Do not use Process.Start | Error | Detects `Process.Start` which may inherit process state |
| MSB0007 | Do not modify default culture | Error | Detects `CultureInfo.DefaultThreadCurrentCulture` modification |
| MSB0008 | Do not modify ThreadPool | Error | Detects `ThreadPool.SetMinThreads/SetMaxThreads` |
| MSB0009 | Assembly loading warning | Warning | Detects dynamic assembly loading that may cause conflicts |
| MSB0010 | Do not kill current process | Error | Detects `Process.GetCurrentProcess().Kill()` |
| MSB0011 | Static fields warning | Warning | Detects mutable static fields that may cause race conditions |

## Usage

Reference this analyzer package in your task project:

```xml
<PackageReference Include="Microsoft.Build.ThreadSafeTaskAnalyzer" Version="*" PrivateAssets="all" />
```

## More Information

See the [Thread-Safe Tasks specification](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md) for detailed guidelines on thread-safe task development.
