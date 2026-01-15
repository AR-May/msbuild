// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Microsoft.Build.ThreadSafeTaskAnalyzer.UnitTests.CSharpAnalyzerVerifier<Microsoft.Build.ThreadSafeTaskAnalyzer.ThreadSafeTaskAnalyzer>;

namespace Microsoft.Build.ThreadSafeTaskAnalyzer.UnitTests;

/// <summary>
/// Unit tests for the ThreadSafeTaskAnalyzer.
/// </summary>
public class ThreadSafeTaskAnalyzerTests
{
    /// <summary>
    /// Helper to create complete test source with framework stubs.
    /// </summary>
    private static string CreateTestSource(string taskCode, bool useAttribute = false)
    {
        var baseClass = useAttribute
            ? ": Microsoft.Build.Framework.ITask"
            : ": Microsoft.Build.Framework.ITask, Microsoft.Build.Framework.IMultiThreadableTask";

        var attributeDecl = useAttribute
            ? "[Microsoft.Build.Framework.MSBuildMultiThreadableTask]"
            : "";

        var taskEnvProp = useAttribute
            ? ""
            : "public Microsoft.Build.Framework.TaskEnvironment TaskEnvironment { get; set; }";

        return $@"
using System;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Reflection;

namespace Microsoft.Build.Framework
{{
    public interface IBuildEngine {{ }}
    public interface ITaskHost {{ }}

    public interface ITask
    {{
        IBuildEngine BuildEngine {{ get; set; }}
        ITaskHost HostObject {{ get; set; }}
        bool Execute();
    }}

    public class TaskEnvironment
    {{
        public string GetAbsolutePath(string path) => path;
    }}

    public interface IMultiThreadableTask : ITask
    {{
        TaskEnvironment TaskEnvironment {{ get; set; }}
    }}

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MSBuildMultiThreadableTaskAttribute : System.Attribute {{ }}
}}

{attributeDecl}
public class MyTask {baseClass}
{{
    public Microsoft.Build.Framework.IBuildEngine BuildEngine {{ get; set; }}
    public Microsoft.Build.Framework.ITaskHost HostObject {{ get; set; }}
    {taskEnvProp}

    public bool Execute()
    {{
        {taskCode}
        return true;
    }}
}}
";
    }

    /// <summary>
    /// Helper to create a regular task (not multi-threadable) for negative tests.
    /// </summary>
    private static string CreateRegularTaskSource(string taskCode)
    {
        return $@"
using System;
using System.IO;

namespace Microsoft.Build.Framework
{{
    public interface IBuildEngine {{ }}
    public interface ITaskHost {{ }}

    public interface ITask
    {{
        IBuildEngine BuildEngine {{ get; set; }}
        ITaskHost HostObject {{ get; set; }}
        bool Execute();
    }}
}}

public class RegularTask : Microsoft.Build.Framework.ITask
{{
    public Microsoft.Build.Framework.IBuildEngine BuildEngine {{ get; set; }}
    public Microsoft.Build.Framework.ITaskHost HostObject {{ get; set; }}

    public bool Execute()
    {{
        {taskCode}
        return true;
    }}
}}
";
    }

    /// <summary>
    /// Helper to create a task with a static field for testing static field detection.
    /// </summary>
    private static string CreateTaskWithStaticField(bool isReadonly, bool isConst)
    {
        var modifier = isConst ? "const" : (isReadonly ? "static readonly" : "static");
        var initializer = isConst ? " = \"constant\"" : " = \"value\"";

        return $@"
using System;

namespace Microsoft.Build.Framework
{{
    public interface IBuildEngine {{ }}
    public interface ITaskHost {{ }}

    public interface ITask
    {{
        IBuildEngine BuildEngine {{ get; set; }}
        ITaskHost HostObject {{ get; set; }}
        bool Execute();
    }}

    public class TaskEnvironment
    {{
        public string GetAbsolutePath(string path) => path;
    }}

    public interface IMultiThreadableTask : ITask
    {{
        TaskEnvironment TaskEnvironment {{ get; set; }}
    }}
}}

public class MyTask : Microsoft.Build.Framework.ITask, Microsoft.Build.Framework.IMultiThreadableTask
{{
    private {modifier} string _field{initializer};

    public Microsoft.Build.Framework.IBuildEngine BuildEngine {{ get; set; }}
    public Microsoft.Build.Framework.ITaskHost HostObject {{ get; set; }}
    public Microsoft.Build.Framework.TaskEnvironment TaskEnvironment {{ get; set; }}

    public bool Execute()
    {{
        var value = _field;
        return true;
    }}
}}
";
    }

    [Fact]
    public async Task NoCode_NoDiagnostics()
    {
        var test = string.Empty;
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RegularTask_NoDiagnostics()
    {
        var test = CreateRegularTaskSource(@"
            Environment.SetEnvironmentVariable(""TEST"", ""value"");
            var cwd = Environment.CurrentDirectory;
        ");
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentSetEnvironmentVariable_ReportsError()
    {
        var test = CreateTestSource(@"Environment.SetEnvironmentVariable(""TEST"", ""value"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.EnvironmentModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentGetEnvironmentVariable_ReportsError()
    {
        var test = CreateTestSource(@"var value = Environment.GetEnvironmentVariable(""TEST"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.EnvironmentModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CurrentDirectoryGetter_ReportsError()
    {
        var test = CreateTestSource(@"var cwd = Environment.CurrentDirectory;");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CurrentDirectorySetter_ReportsError()
    {
        var test = CreateTestSource(@"Environment.CurrentDirectory = ""C:\\temp"";");
        // Assignment triggers both member access and assignment analysis
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0),
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentExit_ReportsError()
    {
        var test = CreateTestSource(@"Environment.Exit(1);");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.ProcessTermination.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentFailFast_ReportsError()
    {
        var test = CreateTestSource(@"Environment.FailFast(""fatal error"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.ProcessTermination.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_PathGetFullPath_ReportsError()
    {
        var test = CreateTestSource(@"var fullPath = Path.GetFullPath(""relative/path"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.PathGetFullPath.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileExists_ReportsWarning()
    {
        var test = CreateTestSource(@"var exists = File.Exists(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.RelativePathWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_ThreadPoolSetMinThreads_ReportsError()
    {
        var test = CreateTestSource(@"ThreadPool.SetMinThreads(10, 10);");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.ThreadPoolModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_StaticField_ReportsWarning()
    {
        var test = CreateTaskWithStaticField(isReadonly: false, isConst: false);
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.StaticFieldWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_StaticReadonlyField_NoDiagnostic()
    {
        var test = CreateTaskWithStaticField(isReadonly: true, isConst: false);
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultiThreadableTask_ConstField_NoDiagnostic()
    {
        var test = CreateTaskWithStaticField(isReadonly: false, isConst: true);
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task TaskWithAttribute_EnvironmentSetEnvironmentVariable_ReportsError()
    {
        var test = CreateTestSource(@"Environment.SetEnvironmentVariable(""TEST"", ""value"");", useAttribute: true);
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.EnvironmentModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_AssemblyLoadFrom_ReportsWarning()
    {
        var test = CreateTestSource(@"var assembly = Assembly.LoadFrom(""MyAssembly.dll"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.AssemblyLoadingWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileInfoConstructor_ReportsWarning()
    {
        var test = CreateTestSource(@"var fileInfo = new FileInfo(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.RelativePathWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CultureInfoDefaultThreadCurrentCulture_ReportsError()
    {
        var test = CreateTestSource(@"CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;");
        // Assignment triggers both member access and assignment analysis
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CultureModification.Id, 0, 0),
            new DiagnosticResult(DiagnosticDescriptors.CultureModification.Id, 0, 0));
    }
}
