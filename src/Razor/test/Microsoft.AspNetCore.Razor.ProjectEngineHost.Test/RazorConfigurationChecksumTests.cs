// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Xunit;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test;

public class RazorConfigurationChecksumTests
{
    [Fact]
    public void CheckSame()
    {
        var config1 = GetConfiguration();
        var config2 = GetConfiguration();

        Assert.Equal(config1.Checksum, config2.Checksum);
    }

    [Fact]
    public void Change_RazorLanguageVersion()
    {
        var config1 = GetConfiguration();
        var config2 = config1 with { LanguageVersion = RazorLanguageVersion.Version_2_1 };

        Assert.NotEqual(config1.Checksum, config2.Checksum);
    }

    [Fact]
    public void Change_ConfigurationName()
    {
        var config1 = GetConfiguration();
        var config2 = config1 with { ConfigurationName = "Configuration2" };

        Assert.NotEqual(config1.Checksum, config2.Checksum);
    }

    [Fact]
    public void Change_Extensions()
    {
        var config1 = GetConfiguration();
        var config2 = config1 with { Extensions = config1.Extensions.Add(new RazorExtension("TestExtension2")) };

        Assert.NotEqual(config1.Checksum, config2.Checksum);
    }

    [Fact]
    public void Change_UseConsolidatedMvcViews()
    {
        var config1 = GetConfiguration();
        var config2 = config1 with { UseConsolidatedMvcViews = !config1.UseConsolidatedMvcViews };

        Assert.NotEqual(config1.Checksum, config2.Checksum);
    }

    private RazorConfiguration GetConfiguration()
    {
        return new RazorConfiguration(
            RazorLanguageVersion.Latest,
            "Configuration",
            [new RazorExtension("TestExtension")],
            new LanguageServerFlags(ForceRuntimeCodeGeneration: true),
            UseConsolidatedMvcViews: false);
    }
}
