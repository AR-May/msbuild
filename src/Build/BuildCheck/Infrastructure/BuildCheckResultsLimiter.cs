// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Wrapper around BuildCheck results message reporting. Allows to centrally control emission of BuildCheck results, limiting results number. 
/// </summary>
internal class BuildCheckResultsLimiter
{
    private readonly int _maxMessageCountPerRule;
    private readonly Dictionary<string, int> _messageCountPerRule = new Dictionary<string, int>();

    public BuildCheckResultsLimiter(int maxMessageCount) => _maxMessageCountPerRule = maxMessageCount;

    public BuildCheckResultsLimiter() => _maxMessageCountPerRule = 10;

    public void ProcessAndReportResult<T>(BuildCheckDataContext<T> context, CheckRule rule, IMSBuildElementLocation location, params string[] messageArgs) where T : CheckData
    {
        if (!_messageCountPerRule.ContainsKey(rule.Id))
        {
            _messageCountPerRule[rule.Id] = 0;
        }

        if (_messageCountPerRule[rule.Id] >= _maxMessageCountPerRule)
        {
            return;
        }

        _messageCountPerRule[rule.Id]++;
        context.ReportResult(BuildCheckResult.Create(
            rule,
            location,
            messageArgs));
    }
}
