﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

public class RazorSemanticTokensLegendTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public void RazorModifiers_MustStartAfterRoslyn()
    {
        var clientCapabilitiesService = new TestClientCapabilitiesService(new VisualStudio.LanguageServer.Protocol.VSInternalClientCapabilities());
        var service = new RazorSemanticTokensLegendService(clientCapabilitiesService);

        var expected = Math.Pow(RazorSemanticTokensAccessor.GetTokenModifiers().Length, 2);

        Assert.Equal(expected, service.TokenModifiers.RazorCodeModifier);
    }
}
