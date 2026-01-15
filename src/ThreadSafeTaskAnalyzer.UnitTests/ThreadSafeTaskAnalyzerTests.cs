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
    /// Core helper to create task source with optional [MSBuildMultiThreadableTask] attribute.
    /// </summary>
    private static string CreateTaskSource(string taskCode, bool isMultiThreadable = true, string additionalMembers = "")
    {
        var attribute = isMultiThreadable ? "[Microsoft.Build.Framework.MSBuildMultiThreadableTask]" : "";

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

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MSBuildMultiThreadableTaskAttribute : System.Attribute {{ }}
}}

{attribute}
public class MyTask : Microsoft.Build.Framework.ITask
{{
    {additionalMembers}

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

    private static string CreateMultiThreadableTaskSource(string taskCode) => CreateTaskSource(taskCode, isMultiThreadable: true);

    private static string CreateRegularTaskSource(string taskCode) => CreateTaskSource(taskCode, isMultiThreadable: false);

    private static string CreateTaskWithStaticField(bool isReadonly, bool isConst)
    {
        var modifier = isConst ? "const" : (isReadonly ? "static readonly" : "static");
        var initializer = isConst ? " = \"constant\"" : " = \"value\"";
        var fieldDecl = $"private {modifier} string _field{initializer};";

        return CreateTaskSource("var value = _field;", isMultiThreadable: true, additionalMembers: fieldDecl);
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
        var test = CreateMultiThreadableTaskSource(@"Environment.SetEnvironmentVariable(""TEST"", ""value"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.EnvironmentModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentGetEnvironmentVariable_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var value = Environment.GetEnvironmentVariable(""TEST"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.EnvironmentModification.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CurrentDirectoryGetter_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var cwd = Environment.CurrentDirectory;");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CurrentDirectorySetter_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"Environment.CurrentDirectory = ""C:\\temp"";");
        // Assignment triggers both member access and assignment analysis
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0),
            new DiagnosticResult(DiagnosticDescriptors.CurrentDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentExit_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"Environment.Exit(1);");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.ProcessTermination.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_EnvironmentFailFast_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"Environment.FailFast(""fatal error"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.ProcessTermination.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_PathGetFullPath_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var fullPath = Path.GetFullPath(""relative/path"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.PathGetFullPath.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileExists_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var exists = File.Exists(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileReadAllText_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var content = File.ReadAllText(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileWriteAllText_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"File.WriteAllText(""file.txt"", ""content"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileCopy_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"File.Copy(""source.txt"", ""dest.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileDelete_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"File.Delete(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileMove_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"File.Move(""source.txt"", ""dest.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_DirectoryExists_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var exists = Directory.Exists(""subdir"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_DirectoryCreateDirectory_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"Directory.CreateDirectory(""newdir"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_DirectoryDelete_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"Directory.Delete(""subdir"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_DirectoryGetFiles_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"var files = Directory.GetFiles(""subdir"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.FileDirectoryUsage.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_ThreadPoolSetMinThreads_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"ThreadPool.SetMinThreads(10, 10);");
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
    public async Task MultiThreadableTask_AssemblyLoadFrom_ReportsWarning()
    {
        var test = CreateMultiThreadableTaskSource(@"var assembly = Assembly.LoadFrom(""MyAssembly.dll"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.AssemblyLoadingWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_FileInfoConstructor_ReportsWarning()
    {
        var test = CreateMultiThreadableTaskSource(@"var fileInfo = new FileInfo(""file.txt"");");
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.RelativePathWarning.Id, 0, 0));
    }

    [Fact]
    public async Task MultiThreadableTask_CultureInfoDefaultThreadCurrentCulture_ReportsError()
    {
        var test = CreateMultiThreadableTaskSource(@"CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;");
        // Assignment triggers both member access and assignment analysis
        await VerifyCS.VerifyAnalyzerAsync(test,
            new DiagnosticResult(DiagnosticDescriptors.CultureModification.Id, 0, 0),
            new DiagnosticResult(DiagnosticDescriptors.CultureModification.Id, 0, 0));
    }
}
