﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck.Acquisition;

internal interface IBuildCheckAcquisitionModule
{
    /// <summary>
    /// Creates a list of factory delegates for building check rules instances from a given assembly path.
    /// </summary>
    List<CheckFactory> CreateCheckFactories(CheckAcquisitionData checkAcquisitionData, ICheckContext checkContext);
}
