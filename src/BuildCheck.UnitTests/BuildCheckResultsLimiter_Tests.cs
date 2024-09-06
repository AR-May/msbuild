// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckResultsLimiter_Tests
{
    private readonly VeryVerboseCheckRuleMock _check;

    private readonly MockBuildCheckRegistrationContext _registrationContext;

    public BuildCheckResultsLimiter_Tests()
    {
        _check = new VeryVerboseCheckRuleMock("VerboseCheckRuleMock");
        _check.Initialize();
        _registrationContext = new MockBuildCheckRegistrationContext();
        _check.RegisterActions(_registrationContext);
    }

    private EvaluatedPropertiesCheckData MakeEvaluatedPropertiesAction(
        string projectFile,
        Dictionary<string, string>? evaluatedProperties,
        IReadOnlyDictionary<string, (string EnvVarValue, string File, int Line, int Column)>? evaluatedEnvVars)
    {
        return new EvaluatedPropertiesCheckData(
            projectFile,
            null,
            evaluatedProperties ?? new Dictionary<string, string>());
    }

    [Fact]
    public void TestBuildCheckMessageDispatcher()
    {
        string projectFile = "/fake/project.proj";
        _registrationContext.TriggerEvaluatedPropertiesAction(MakeEvaluatedPropertiesAction(
            projectFile,
            null,
            null));

        _registrationContext.Results.Count.ShouldBe(
            Math.Max(VeryVerboseCheckRuleMock.Rule1Count, VeryVerboseCheckRuleMock.MaxCountPerRule) +
            Math.Max(VeryVerboseCheckRuleMock.Rule2Count, VeryVerboseCheckRuleMock.MaxCountPerRule) +
            Math.Max(VeryVerboseCheckRuleMock.Rule3Count, VeryVerboseCheckRuleMock.MaxCountPerRule));
    }

    private sealed class VeryVerboseCheckRuleMock : Check
    {
        public static CheckRule SupportedRule1 = new CheckRule(
            "V001",
            "Rule1",
            "Description",
            "Message format: {0}",
            new CheckConfiguration());

        public static CheckRule SupportedRule2 = new CheckRule(
            "V002",
            "Rule2",
            "Description",
            "Message format: {0}",
            new CheckConfiguration());

        public static CheckRule SupportedRule3 = new CheckRule(
            "V003",
            "Rule3",
            "Description",
            "Message format: {0}",
            new CheckConfiguration());

        public const int Rule1Count = 3;
        public const int Rule2Count = 20;
        public const int Rule3Count = 2;
        public const int MaxCountPerRule = 3;

        public BuildCheckResultsLimiter? ResultsLimiter;

        internal VeryVerboseCheckRuleMock(string friendlyName)
        {
            FriendlyName = friendlyName;
        }

        public override string FriendlyName { get; }

        public override IReadOnlyList<CheckRule> SupportedRules { get; } = new List<CheckRule>() { SupportedRule1, SupportedRule2, SupportedRule3 };

        public override void Initialize(ConfigurationContext configurationContext)
        {
            ResultsLimiter = new BuildCheckResultsLimiter(MaxCountPerRule);
        }

        public void Initialize()
        {
            ResultsLimiter = new BuildCheckResultsLimiter(3);
        }

        public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
        {
            registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
        }

        private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
        {
            for (int i = 0; i < Rule1Count; i++)
            {
                ResultsLimiter?.ProcessAndReportResult(
                    context,
                    SupportedRule1,
                    ElementLocation.EmptyLocation,
                    "Argument for the message format");
            }
            for (int i = 0; i < Rule2Count; i++)
            {
                ResultsLimiter?.ProcessAndReportResult(
                    context,
                    SupportedRule2,
                    ElementLocation.EmptyLocation,
                    "Argument for the message format");
            }
            for (int i = 0; i < Rule3Count; i++)
            {
                ResultsLimiter?.ProcessAndReportResult(
                    context,
                    SupportedRule3,
                    ElementLocation.EmptyLocation,
                    "Argument for the message format");
            }
        }
    }
}
