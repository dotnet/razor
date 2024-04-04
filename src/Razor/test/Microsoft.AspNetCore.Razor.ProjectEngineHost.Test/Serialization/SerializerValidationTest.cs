// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Serialization.Json;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test.Serialization;

public class SerializerValidationTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Theory(Skip = "https://github.com/dotnet/razor/issues/8202")]
    [InlineData("Kendo.Mvc.Examples.project.razor.json")]
    [InlineData("project.razor.json")]
    public void VerifyMessagePack_RazorProjectInfo(string resourceName)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, "Benchmarking");

        // Read tag helpers from JSON
        var originalProjectInfo = DeserializeProjectInfoFromJsonBytes(resourceBytes);

        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                RazorProjectInfoResolver.Instance,
                StandardResolver.Instance));

        // Act
        var bytes = MessagePackConvert.Serialize(originalProjectInfo, options);
        var actualProjectInfo = MessagePackConvert.Deserialize<RazorProjectInfo>(bytes, options);

        // Assert
        Assert.Equal(originalProjectInfo.SerializedFilePath, actualProjectInfo.SerializedFilePath);
        Assert.Equal(originalProjectInfo.FilePath, actualProjectInfo.FilePath);
        Assert.Equal(originalProjectInfo.Configuration, actualProjectInfo.Configuration);
        Assert.Equal(originalProjectInfo.RootNamespace, actualProjectInfo.RootNamespace);
        Assert.Equal(originalProjectInfo.ProjectWorkspaceState, actualProjectInfo.ProjectWorkspaceState);
        Assert.Equal<DocumentSnapshotHandle>(originalProjectInfo.Documents, actualProjectInfo.Documents);
    }

    [Theory]
    [InlineData("Kendo.Mvc.Examples.taghelpers.json")]
    [InlineData("taghelpers.json")]
    public void VerifyMessagePack_TagHelpers(string resourceName)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, "Benchmarking");

        // Read tag helpers from JSON
        var originalTagHelpers = ReadTagHelpersFromJsonBytes(resourceBytes);

        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(
                FetchTagHelpersResultResolver.Instance,
                StandardResolver.Instance));

        // Act
        var bytes = MessagePackConvert.Serialize(originalTagHelpers, options);
        var actualTagHelpers = MessagePackConvert.Deserialize<ImmutableArray<TagHelperDescriptor>>(bytes, options);

        // Assert
        Assert.Equal<TagHelperDescriptor>(originalTagHelpers, actualTagHelpers);
    }

    [Theory]
    [InlineData("Kendo.Mvc.Examples.project.razor.json")]
    [InlineData("project.razor.json")]
    public void VerifyJson_RazorProjectInfo(string resourceName)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, "Benchmarking");

        // Read tag helpers from JSON
        var originalProjectInfo = DeserializeProjectInfoFromJsonBytes(resourceBytes);

        // Act

        // Serialize to JSON
        var jsonText = JsonDataConvert.SerializeObject(originalProjectInfo, ObjectWriters.WriteProperties);

        // Deserialize from message pack
        var actualProjectInfo = JsonDataConvert.DeserializeObject(jsonText, ObjectReaders.ReadProjectInfoFromProperties);
        Assert.NotNull(actualProjectInfo);

        // Assert
        Assert.Equal(originalProjectInfo.SerializedFilePath, actualProjectInfo.SerializedFilePath);
        Assert.Equal(originalProjectInfo.FilePath, actualProjectInfo.FilePath);
        Assert.Equal(originalProjectInfo.Configuration, actualProjectInfo.Configuration);
        Assert.Equal(originalProjectInfo.RootNamespace, actualProjectInfo.RootNamespace);
        Assert.Equal(originalProjectInfo.ProjectWorkspaceState, actualProjectInfo.ProjectWorkspaceState);
        Assert.Equal<DocumentSnapshotHandle>(originalProjectInfo.Documents, actualProjectInfo.Documents);
    }

    [Theory]
    [InlineData("Kendo.Mvc.Examples.taghelpers.json", "Benchmarking")]
    [InlineData("taghelpers.json", "Benchmarking")]
    [InlineData("BlazorServerApp.TagHelpers.json")]
    public void VerifyJson_TagHelpers(string resourceName, string? folderName = null)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, folderName);

        // Read tag helpers from JSON
        var originalTagHelpers = ReadTagHelpersFromJsonBytes(resourceBytes);

        // Act

        // Serialize to JSON
        var jsonText = JsonDataConvert.SerializeData(
            dataWriter => dataWriter.WriteArray(originalTagHelpers, ObjectWriters.Write));

        // Deserialize from JSON
        var actualTagHelpers = JsonDataConvert.DeserializeData(jsonText,
            r => r.ReadImmutableArray(
                static r => ObjectReaders.ReadTagHelper(r, useCache: false)));

        // Assert
        Assert.Equal<TagHelperDescriptor>(originalTagHelpers, actualTagHelpers);
    }

    /// <summary>
    /// When set to <c>true</c>, the test will generate a message pack baseline file to use for future testing.
    /// This should only be used when Microsoft.AspNetCore.Razor.Serialization.MessagePack.SerializationFormat.Version has
    /// changed and the baseline needs to be updated.
    /// </summary>
    const bool GenerateBaseline = false;

    [Fact]
    public void ValidateGenerateBaselineIsFalse()
    {
        Assert.False(GenerateBaseline);
    }

    [Fact]
    public void VerifyMessagePack_DeserializeMessagepack()
    {
        // Arrange
        var resourceName = "test.project.razor.bin";
        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                RazorProjectInfoResolver.Instance,
                StandardResolver.Instance));

        var directory = TestProject.GetProjectDirectory(typeof(SerializerValidationTest), layer: TestProject.Layer.Tooling, useCurrentDirectory: true);
        var path = Path.Combine(directory, resourceName);

        if (GenerateBaseline)
        {
#pragma warning disable CS0162 // Unreachable code detected
            var projectBytes = RazorTestResources.GetResourceBytes("project.razor.json", "Benchmarking");
            var projectInfo = DeserializeProjectInfoFromJsonBytes(projectBytes);

            File.WriteAllBytes(path, MessagePackConvert.Serialize(projectInfo, options).ToArray());
            return;
#pragma warning restore CS0162 // Unreachable code detected
        }

        var testBytes = File.ReadAllBytes(path);

        // Act
        var actualProjectInfo = MessagePackConvert.Deserialize<RazorProjectInfo>(testBytes, options);

        // Assert
        Assert.NotNull(actualProjectInfo);
        Assert.NotNull(actualProjectInfo.DisplayName);
    }

    private static RazorProjectInfo DeserializeProjectInfoFromJsonBytes(byte[] resourceBytes)
    {
        using var stream = new MemoryStream(resourceBytes);
        using var streamReader = new StreamReader(stream);

        var originalProjectInfo = JsonDataConvert.DeserializeObject(streamReader, ObjectReaders.ReadProjectInfoFromProperties);
        Assert.NotNull(originalProjectInfo);

        return originalProjectInfo;
    }

    private static ImmutableArray<TagHelperDescriptor> ReadTagHelpersFromJsonBytes(byte[] resourceBytes)
    {
        using var stream = new MemoryStream(resourceBytes);
        using var streamReader = new StreamReader(stream);

        return JsonDataConvert.DeserializeData(streamReader,
            static r => r.ReadImmutableArray(
                static r => ObjectReaders.ReadTagHelper(r, useCache: false)));
    }
}
