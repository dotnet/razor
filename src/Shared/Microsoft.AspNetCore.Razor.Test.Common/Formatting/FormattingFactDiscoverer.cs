// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Test.Common;

internal class FormattingFactDiscoverer(IMessageSink diagnosticMessageSink)
    : FactDiscoverer(diagnosticMessageSink)
{
    public override IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
    {
        return CreateTestCases(discoveryOptions, testMethod, factAttribute, DiagnosticMessageSink);
    }

    public static IEnumerable<IXunitTestCase> CreateTestCases(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute, IMessageSink messageSink, object[]? dataRow = null)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        if (factAttribute.GetNamedArgument<bool>(nameof(FormattingTestFactAttribute.SkipFlipLineEnding)))
        {
            return [new FormattingTestCase(shouldFlipLineEndings: false, messageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, dataRow)];
        }

        return [
            new FormattingTestCase(shouldFlipLineEndings: true, messageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, dataRow),
            new FormattingTestCase(shouldFlipLineEndings: false, messageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, dataRow)
        ];
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
