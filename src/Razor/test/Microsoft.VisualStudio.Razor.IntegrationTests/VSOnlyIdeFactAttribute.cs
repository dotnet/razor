// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class VSOnlyIdeFactAttribute : IdeFactAttribute
{
    public VSOnlyIdeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VisualStudioVersion") is null)
        {
            Skip = "This test can only run in Visual Studio";
        }
    }
}
