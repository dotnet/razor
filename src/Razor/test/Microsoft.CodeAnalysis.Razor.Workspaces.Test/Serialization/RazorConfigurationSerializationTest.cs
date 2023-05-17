﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Serialization;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Serialization;

public class RazorConfigurationSerializationTest : TestBase
{
    public RazorConfigurationSerializationTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void RazorConfigurationJsonConverter_Serialization_CanRoundTrip()
    {
        // Arrange
        var configuration = new ProjectSystemRazorConfiguration(
            RazorLanguageVersion.Version_1_1,
            "Test",
            new[]
            {
                new ProjectSystemRazorExtension("Test-Extension1"),
                new ProjectSystemRazorExtension("Test-Extension2"),
            });

        // Act
        var json = JsonConvertUtility.Serialize(configuration);
        Assert.NotNull(json);

        var obj = JsonConvertUtility.DeserializeConfiguration(json);
        Assert.NotNull(obj);

        // Assert
        Assert.Equal(configuration.ConfigurationName, obj.ConfigurationName);
        Assert.Collection(
            configuration.Extensions,
            e => Assert.Equal("Test-Extension1", e.ExtensionName),
            e => Assert.Equal("Test-Extension2", e.ExtensionName));
        Assert.Equal(configuration.LanguageVersion, obj.LanguageVersion);
    }

    [Fact]
    public void RazorConfigurationJsonConverter_Serialization_MVC3_CanRead()
    {
        // Arrange
        var configurationJson = """
            {
              "ConfigurationName": "MVC-3.0",
              "LanguageVersion": "3.0",
              "Extensions": [
                {
                  "ExtensionName": "MVC-3.0"
                }
              ]
            }
            """;

        // Act
        var obj = JsonConvertUtility.DeserializeConfiguration(configurationJson);
        Assert.NotNull(obj);

        // Assert
        Assert.Equal("MVC-3.0", obj.ConfigurationName);
        var extension = Assert.Single(obj.Extensions);
        Assert.Equal("MVC-3.0", extension.ExtensionName);
        Assert.Equal(RazorLanguageVersion.Parse("3.0"), obj.LanguageVersion);
    }

    [Fact]
    public void RazorConfigurationJsonConverter_Serialization_MVC2_CanRead()
    {
        // Arrange
        var configurationJson = """
            {
              "ConfigurationName": "MVC-2.1",
              "LanguageVersion": "2.1",
              "Extensions": [
                {
                  "ExtensionName": "MVC-2.1"
                }
              ]
            }
            """;

        // Act
        var obj = JsonConvertUtility.DeserializeConfiguration(configurationJson);
        Assert.NotNull(obj);

        // Assert
        Assert.Equal("MVC-2.1", obj.ConfigurationName);
        var extension = Assert.Single(obj.Extensions);
        Assert.Equal("MVC-2.1", extension.ExtensionName);
        Assert.Equal(RazorLanguageVersion.Parse("2.1"), obj.LanguageVersion);
    }

    [Fact]
    public void RazorConfigurationJsonConverter_Serialization_MVC1_CanRead()
    {
        // Arrange
        var configurationJson = """
            {
              "ConfigurationName": "MVC-1.1",
              "Extensions": [
                {
                  "ExtensionName": "MVC-1.1"
                }
              ],
              "LanguageVersion": {
                "Major": 1,
                "Minor": 1
              }
            }
            """;

        // Act
        var obj = JsonConvertUtility.DeserializeConfiguration(configurationJson);
        Assert.NotNull(obj);

        // Assert
        Assert.Equal("MVC-1.1", obj.ConfigurationName);
        var extension = Assert.Single(obj.Extensions);
        Assert.Equal("MVC-1.1", extension.ExtensionName);
        Assert.Equal(RazorLanguageVersion.Parse("1.1"), obj.LanguageVersion);
    }
}
