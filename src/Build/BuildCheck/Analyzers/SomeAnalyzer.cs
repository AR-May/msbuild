// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Analyzers;

internal sealed class SomeAnalyzer : BuildAnalyzer
{
    private const string RuleId = "BC0101";
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule(RuleId, "ConflictingOutputPath",
        "Two projects should not share their OutputPath nor IntermediateOutputPath locations",
        "Projects {0} and {1} have conflicting output paths: {2}.",
        new BuildAnalyzerConfiguration() { RuleId = RuleId, Severity = BuildAnalyzerResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.SharedOutputPathAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        string projectPath = context.Data.ProjectFilePath ?? "";
        for (int i = 0; i < 100; i++)
        {
            context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    ElementLocation.EmptyLocation,
                    Path.GetFileName(projectPath),
                    Path.GetFileName(projectPath),
                    projectPath!));
        }
    }
}
