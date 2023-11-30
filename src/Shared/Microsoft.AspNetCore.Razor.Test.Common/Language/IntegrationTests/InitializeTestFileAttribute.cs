﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class InitializeTestFileAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
    {
        if (typeof(IntegrationTestBase).GetTypeInfo().IsAssignableFrom(methodUnderTest.DeclaringType.GetTypeInfo()))
        {
            var typeName = methodUnderTest.ReflectedType.Name;
            IntegrationTestBase.FileName = $"TestFiles/IntegrationTests/{typeName}/{methodUnderTest.Name}";
        }
    }

    public override void After(MethodInfo methodUnderTest)
    {
        if (typeof(IntegrationTestBase).GetTypeInfo().IsAssignableFrom(methodUnderTest.DeclaringType.GetTypeInfo()))
        {
            IntegrationTestBase.FileName = null;
        }
    }
}
