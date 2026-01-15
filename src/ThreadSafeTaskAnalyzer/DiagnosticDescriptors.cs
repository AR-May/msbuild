// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.Build.ThreadSafeTaskAnalyzer;

/// <summary>
/// Diagnostic descriptors for the Thread-Safe Task Analyzer.
/// These diagnostics help identify API usages that violate thread-safety requirements
/// in MSBuild tasks implementing <c>IMultiThreadableTask</c> or marked with <c>MSBuildMultiThreadableTaskAttribute</c>.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "ThreadSafety";
    private const string HelpLinkBase = "https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md";

    /// <summary>
    /// MSB0001: Usage of Environment class properties/methods that modify process-level state.
    /// </summary>
    public static readonly DiagnosticDescriptor EnvironmentModification = new(
        id: "MSB0001",
        title: "Do not modify environment variables in thread-safe tasks",
        messageFormat: "'{0}' modifies process-level environment state and should not be used in thread-safe tasks. Use TaskEnvironment instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Thread-safe tasks must not modify process-level environment state. Use TaskEnvironment for environment variable access.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0002: Usage of Environment.CurrentDirectory.
    /// </summary>
    public static readonly DiagnosticDescriptor CurrentDirectoryUsage = new(
        id: "MSB0002",
        title: "Do not access or modify current directory in thread-safe tasks",
        messageFormat: "'{0}' accesses or modifies the process current directory. Use TaskEnvironment.GetAbsolutePath() instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Thread-safe tasks must not access or modify the current directory. Use TaskEnvironment.GetAbsolutePath() for path resolution.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0003: Usage of Environment.Exit or Environment.FailFast.
    /// </summary>
    public static readonly DiagnosticDescriptor ProcessTermination = new(
        id: "MSB0003",
        title: "Do not terminate process in thread-safe tasks",
        messageFormat: "'{0}' terminates the entire process. Return false from Execute() or throw an exception instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Thread-safe tasks must not terminate the process. Return false from Execute() or throw an exception to indicate failure.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0004: Usage of Path.GetFullPath which uses current working directory.
    /// </summary>
    public static readonly DiagnosticDescriptor PathGetFullPath = new(
        id: "MSB0004",
        title: "Do not use Path.GetFullPath in thread-safe tasks",
        messageFormat: "'{0}' uses the current working directory for path resolution. Use TaskEnvironment.GetAbsolutePath() instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Path.GetFullPath uses the current working directory which is process-global. Use TaskEnvironment.GetAbsolutePath() instead.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0005: Relative path usage with file system APIs.
    /// </summary>
    public static readonly DiagnosticDescriptor RelativePathWarning = new(
        id: "MSB0005",
        title: "Use absolute paths with file system APIs in thread-safe tasks",
        messageFormat: "'{0}' may use relative paths which depend on current working directory. Ensure absolute paths are used",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "File system APIs with relative paths depend on the current working directory. Always use absolute paths in thread-safe tasks.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0006: Usage of Process.Start without explicit configuration.
    /// </summary>
    public static readonly DiagnosticDescriptor ProcessStartUsage = new(
        id: "MSB0006",
        title: "Do not use Process.Start in thread-safe tasks without explicit configuration",
        messageFormat: "'{0}' may inherit process state. Use TaskEnvironment for process execution",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Process.Start may inherit process state. Use TaskEnvironment for spawning external processes.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0007: Modification of CultureInfo defaults.
    /// </summary>
    public static readonly DiagnosticDescriptor CultureModification = new(
        id: "MSB0007",
        title: "Do not modify default culture in thread-safe tasks",
        messageFormat: "'{0}' modifies default thread culture which affects new threads. Modify the current thread's culture instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DefaultThreadCurrentCulture and DefaultThreadCurrentUICulture affect all new threads. Modify the current thread's culture instead.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0008: Modification of ThreadPool settings.
    /// </summary>
    public static readonly DiagnosticDescriptor ThreadPoolModification = new(
        id: "MSB0008",
        title: "Do not modify ThreadPool settings in thread-safe tasks",
        messageFormat: "'{0}' modifies process-wide ThreadPool settings",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ThreadPool settings are process-wide and should not be modified by tasks.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0009: Assembly loading that may cause version conflicts.
    /// </summary>
    public static readonly DiagnosticDescriptor AssemblyLoadingWarning = new(
        id: "MSB0009",
        title: "Assembly loading may cause version conflicts",
        messageFormat: "'{0}' loads assemblies dynamically which may cause version conflicts. Ensure absolute paths are used",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Dynamic assembly loading may cause version conflicts in the task host. Be aware of potential conflicts and use absolute paths.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0010: Process termination via Process.Kill.
    /// </summary>
    public static readonly DiagnosticDescriptor ProcessKill = new(
        id: "MSB0010",
        title: "Do not kill the current process",
        messageFormat: "'{0}' may terminate the entire MSBuild process",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Calling Kill() on the current process will terminate MSBuild. Return false from Execute() or throw an exception instead.",
        helpLinkUri: HelpLinkBase);

    /// <summary>
    /// MSB0011: Static field usage warning.
    /// </summary>
    public static readonly DiagnosticDescriptor StaticFieldWarning = new(
        id: "MSB0011",
        title: "Static fields may cause race conditions",
        messageFormat: "Static field '{0}' may cause race conditions in thread-safe tasks. Consider using instance fields or thread-safe patterns",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Static fields are shared across threads and may cause race conditions. Use instance fields or thread-safe patterns.",
        helpLinkUri: HelpLinkBase);
}
