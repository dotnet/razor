﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Tooltip;

public class BoundAttributeDescriptionInfoTest(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    [Fact]
    public void ResolveTagHelperTypeName_ExtractsTypeName_SimpleReturnType()
    {
        // Arrange & Act
        var typeName = BoundAttributeDescriptionInfo.ResolveTagHelperTypeName("SomePropertyName", "string SomeTypeName.SomePropertyName");

        // Assert
        Assert.Equal("SomeTypeName", typeName);
    }

    [Fact]
    public void ResolveTagHelperTypeName_ExtractsTypeName_ComplexReturnType()
    {
        // Arrange & Act
        var typeName = BoundAttributeDescriptionInfo.ResolveTagHelperTypeName("SomePropertyName", "SomeReturnTypeName SomeTypeName.SomePropertyName");

        // Assert
        Assert.Equal("SomeTypeName", typeName);
    }
}
