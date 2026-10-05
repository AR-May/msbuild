// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests;

public class ManifestResolveFiles_Tests
{
    private readonly ITestOutputHelper _output;

    public ManifestResolveFiles_Tests(ITestOutputHelper output) => _output = output;

    [WindowsOnlyFact]
    public void DriveRelativePathUsesProjectDirectoryForFileInfo()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder project = env.CreateFolder();
        env.CreateFile(host, "payload.txt", "host");
        string projectFile = env.CreateFile(project, "payload.txt", "project contents").Path;
        env.SetCurrentDirectory(host.Path);

        TaskEnvironment taskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(project.Path);
        string sourcePath = Path.GetPathRoot(project.Path)!.Substring(0, 2) + "payload.txt";
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(sourcePath);
        reference.TargetPath = Path.Combine("publish", "renamed.txt");

        manifest.ResolveFiles([taskEnvironment.ProjectDirectory]);
        reference.ResolvedPath.ShouldBe(projectFile);
        manifest.UpdateFileInfo("4.5");

        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(reference, projectFile);
        reference.SourcePath.ShouldBe(sourcePath);
        reference.TargetPath.ShouldBe(Path.Combine("publish", "renamed.txt"));
    }

    [WindowsOnlyFact]
    public void DriveRelativePathDoesNotFallBackToProcessDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder project = env.CreateFolder();
        env.CreateFile(host, "payload.txt", "host");
        env.SetCurrentDirectory(host.Path);

        string sourcePath = Path.GetPathRoot(project.Path)!.Substring(0, 2) + "payload.txt";
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(sourcePath);

        manifest.ResolveFiles([project.Path]);

        reference.ResolvedPath.ShouldBeNull();
        manifest.OutputMessages.ErrorCount.ShouldBe(1);
    }

    [WindowsOnlyFact]
    public void Wave18_13OptOutPreservesLegacyDriveRelativeResolution()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: false);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder project = env.CreateFolder();
        string hostFile = env.CreateFile(host, "payload.txt", "host contents").Path;
        env.CreateFile(project, "payload.txt", "project contents");
        env.SetCurrentDirectory(host.Path);

        string sourcePath = Path.GetPathRoot(project.Path)!.Substring(0, 2) + "payload.txt";
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(sourcePath);

        manifest.ResolveFiles([project.Path]);
        reference.ResolvedPath.ShouldBe(sourcePath);
        manifest.UpdateFileInfo("4.5");

        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(reference, hostFile);
    }

    [WindowsOnlyFact]
    public void DifferentDriveRelativePathSearchesRelativeDirectoriesInOrder()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder first = env.CreateFolder(Path.Combine(host.Path, "first"));
        TransientTestFolder second = env.CreateFolder(Path.Combine(host.Path, "second"));
        string firstFile = env.CreateFile(first, "payload.txt", "first candidate").Path;
        env.CreateFile(second, "payload.txt", "second candidate");
        env.SetCurrentDirectory(host.Path);

        string currentDrive = Path.GetPathRoot(host.Path)!.Substring(0, 2);
        string differentDrive = currentDrive.Equals("Z:", StringComparison.OrdinalIgnoreCase) ? "Y:" : "Z:";
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(differentDrive + "payload.txt");

        manifest.ResolveFiles(["missing", "first", "second"]);

        reference.ResolvedPath.ShouldBe(firstFile);
        manifest.UpdateFileInfo("4.5");
        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(reference, firstFile);
    }

    [WindowsOnlyFact]
    public void RootRelativePathUsesSearchDirectoryRoot()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder project = env.CreateFolder();
        string file = env.CreateFile(project, "payload.txt", "root-relative contents").Path;
        env.SetCurrentDirectory(env.CreateFolder().Path);

        string sourcePath = file.Substring(2);
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(sourcePath);

        manifest.ResolveFiles([project.Path]);
        reference.ResolvedPath.ShouldBe(file);
        manifest.UpdateFileInfo("4.5");

        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(reference, file);
        reference.SourcePath.ShouldBe(sourcePath);
        reference.TargetPath.ShouldBe("payload.txt");
    }

    [Fact]
    public void FullyQualifiedPathIsNotNormalized()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder folder = env.CreateFolder();
        string file = env.CreateFile(folder, "payload.txt", "absolute contents").Path;
        string sourcePath = Path.Combine(folder.Path, ".", "payload.txt");
        var manifest = new ApplicationManifest();
        FileReference reference = manifest.FileReferences.Add(sourcePath);

        manifest.ResolveFiles([]);

        reference.ResolvedPath.ShouldBeSameAs(sourcePath);
        manifest.UpdateFileInfo("4.5");
        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(reference, file);
    }

    [Fact]
    public void RelativePathKeepsSearchOrderAndTargetPathFallback()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder first = env.CreateFolder(Path.Combine(host.Path, "first"));
        TransientTestFolder second = env.CreateFolder(Path.Combine(host.Path, "second"));
        string firstFile = env.CreateFile(first, "payload.txt", "first contents").Path;
        env.CreateFile(second, "payload.txt", "second contents");
        env.SetCurrentDirectory(host.Path);

        var manifest = new ApplicationManifest();
        FileReference sourceReference = manifest.FileReferences.Add("payload.txt");
        FileReference targetReference = manifest.FileReferences.Add("");
        targetReference.TargetPath = "payload.txt";

        manifest.ResolveFiles(["missing", "first", second.Path]);

        sourceReference.ResolvedPath.ShouldBe(firstFile);
        targetReference.ResolvedPath.ShouldBe(firstFile);
        manifest.UpdateFileInfo("4.5");
        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        AssertFileInfo(sourceReference, firstFile);
        AssertFileInfo(targetReference, firstFile);
        sourceReference.SourcePath.ShouldBe("payload.txt");
        sourceReference.TargetPath.ShouldBe("payload.txt");
        targetReference.SourcePath.ShouldBe("");
        targetReference.TargetPath.ShouldBe("payload.txt");
    }

    [WindowsOnlyFact]
    public void DriveRelativeAssemblyUsesResolvedPathForIdentityAndFileInfo()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetChangeWave(env, enabled: true);
        TransientTestFolder host = env.CreateFolder();
        TransientTestFolder project = env.CreateFolder();
        File.Copy(typeof(Manifest).Assembly.Location, Path.Combine(host.Path, "reference.dll"));
        string projectFile = Path.Combine(project.Path, "reference.dll");
        File.Copy(typeof(TaskEnvironment).Assembly.Location, projectFile);
        env.SetCurrentDirectory(host.Path);

        string sourcePath = Path.GetPathRoot(project.Path)!.Substring(0, 2) + "reference.dll";
        var manifest = new ApplicationManifest();
        AssemblyReference reference = manifest.AssemblyReferences.Add(sourcePath);
        reference.ReferenceType = AssemblyReferenceType.ManagedAssembly;
        reference.TargetPath = "published.dll";

        manifest.ResolveFiles([project.Path]);
        reference.ResolvedPath.ShouldBe(projectFile);
        manifest.UpdateFileInfo("4.5");

        manifest.OutputMessages.ErrorCount.ShouldBe(0);
        reference.AssemblyIdentity.Name.ShouldBe(typeof(TaskEnvironment).Assembly.GetName().Name);
        AssertFileInfo(reference, projectFile);
        reference.SourcePath.ShouldBe(sourcePath);
        reference.TargetPath.ShouldBe("published.dll");
    }

    private static void SetChangeWave(TestEnvironment env, bool enabled)
    {
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", enabled ? null : ChangeWaves.Wave18_13.ToString());
        ChangeWaves.ResetStateForTests();
    }

    private static void AssertFileInfo(BaseReference reference, string expectedFile)
    {
        byte[] contents = File.ReadAllBytes(expectedFile);
        using SHA256 algorithm = SHA256.Create();
        reference.Size.ShouldBe(contents.LongLength);
        reference.Hash.ShouldBe(Convert.ToBase64String(algorithm.ComputeHash(contents)));
    }
}
