// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.Client;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public class MSBuildClientAppTests : IDisposable
    {
#if USE_MSBUILD_DLL_EXTN
        private const string MSBuildExeName = "MSBuild.dll";
#else
        private const string MSBuildExeName = "MSBuild.exe";
#endif

        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;

        public MSBuildClientAppTests(ITestOutputHelper output)
        {
            _output = output;
            _env = UnitTests.TestEnvironment.Create(_output);
        }

        [Fact]
        public void ErrorCommandLine()
        {
            // Environment.SetEnvironmentVariable("RUN_MSBUILD_IN_SERVER", "1");
            string oldValueForMSBuildLoadMicrosoftTargetsReadOnly = Environment.GetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly");
#if FEATURE_GET_COMMANDLINE
            MSBuildClientApp.Execute(@"c:\bin\msbuild.exe -junk").ShouldBe(MSBuildApp.ExitType.SwitchError);

            MSBuildClientApp.Execute(@"msbuild.exe -t").ShouldBe(MSBuildApp.ExitType.SwitchError);

            MSBuildClientApp.Execute(@"msbuild.exe @bogus.rsp").ShouldBe(MSBuildApp.ExitType.InitializationError);
#else
            MSBuildClientApp.Execute(new[] { @"c:\bin\msbuild.exe", "-junk" }).ShouldBe(MSBuildApp.ExitType.SwitchError);

            MSBuildClientApp.Execute(new[] { @"msbuild.exe", "-t" }).ShouldBe(MSBuildApp.ExitType.SwitchError);

            MSBuildClientApp.Execute(new[] { @"msbuild.exe", "@bogus.rsp" }).ShouldBe(MSBuildApp.ExitType.InitializationError);
#endif
            Environment.SetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly", oldValueForMSBuildLoadMicrosoftTargetsReadOnly);
        }

        /// <summary>
        /// Tests that the environment gets passed on to the node during build.
        /// </summary>
        [Fact]
        public void TestEnvironmentTest()
        {
            string projectString = ObjectModelHelpers.CleanupFileContents(
                   @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                    <Target Name=""t""><Error Text='Error' Condition=""'$(MyEnvVariable)' == ''""/></Target>
                    </Project>");
            string tempdir = Path.GetTempPath();
            string projectFileName = Path.Combine(tempdir, "msbEnvironmenttest.proj");
            string quotedProjectFileName = "\"" + projectFileName + "\"";

            try
            {
                Environment.SetEnvironmentVariable("RUN_MSBUILD_IN_SERVER", "1");
                Environment.SetEnvironmentVariable("MyEnvVariable", "1");
                using (StreamWriter sw = FileUtilities.OpenWrite(projectFileName, false))
                {
                    sw.WriteLine(projectString);
                }
                // Should pass
#if FEATURE_GET_COMMANDLINE
                MSBuildClientApp.Execute(@"c:\bin\msbuild.exe " + quotedProjectFileName).ShouldBe(MSBuildApp.ExitType.Success);
#else
                MSBuildClientApp.Execute(new[] { @"c:\bin\msbuild.exe", quotedProjectFileName }).ShouldBe(MSBuildApp.ExitType.Success);
#endif
            }
            finally
            {
                Environment.SetEnvironmentVariable("MyEnvVariable", null);
                File.Delete(projectFileName);
            }
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
