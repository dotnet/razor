// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Xunit.Sdk;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

internal class LogTestRunToConsoleAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
    {
        Console.WriteLine("Starting test: " + methodUnderTest.Name);

        base.Before(methodUnderTest);
    }

    public override void After(MethodInfo methodUnderTest)
    {
        base.After(methodUnderTest);

        Console.WriteLine("Finished test: " + methodUnderTest.Name);
    }
}
