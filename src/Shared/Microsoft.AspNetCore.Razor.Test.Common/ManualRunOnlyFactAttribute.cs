// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Test.Common;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ManualRunOnlyFactAttribute : FactAttribute
{
    public ManualRunOnlyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("_IntegrationTestsRunningInCI") is not null)
        {
            Skip = "This test can only run manually";
        }
    }
}
