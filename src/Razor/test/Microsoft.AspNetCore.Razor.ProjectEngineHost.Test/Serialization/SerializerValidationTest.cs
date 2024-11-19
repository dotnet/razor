// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

///////////////////////////////////////////////////////////////////////////////
//
// Note: The JSON files used for testing are very large. When making
// significant changes to the JSON format for tag helpers or RazorProjectInfo, it
// can be helpful to update the ObjectWriters first and the write new JSON files
// before updating the ObjectReaders. This avoids having to make a series of
// manual edits to the JSON resource files.
//
// 1. Update ObjectWriters to write the new JSON format.
// 2. Uncomment the WRITE_JSON_FILES #define below.
// 3. Run the VerifyJson_RazorProjectInfo and VerifyJson_TagHelpers tests.
//    This will create JSON files on your desktop in the new format.
// 4. Replace the old JSON resource files in the "Test.Common.Tooling" project
//    with the newly generated versions. (Tip: Be sure to run them through a
//    JSON formatter to make diffs more sane.)
// 5. Update ObjectReaders to read the new JSON format.
// 6. Comment the WRITE_JSON_FILES #define again.
// 7. Run all of the tests in SerializerValidationTest to ensure that the
//    new JSON files deserialize correctly.
//
///////////////////////////////////////////////////////////////////////////////

//#define WRITE_JSON_FILES

using System.Collections.Immutable;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Serialization.Json;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test.Serialization;

public class SerializerValidationTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Theory]
    [InlineData("Kendo.Mvc.Examples.project.razor.json", "Telerik")]
    [InlineData("project.razor.json")]
    public void VerifyMessagePack_RazorProjectInfo(string resourceName, string? folderName = null)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, folderName);

        // Read tag helpers from JSON
        var originalProjectInfo = JsonDataConvert.DeserializeProjectInfo(resourceBytes);

        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                RazorProjectInfoResolver.Instance,
                StandardResolver.Instance));

        // Act
        var bytes = MessagePackConvert.Serialize(originalProjectInfo, options);
        var actualProjectInfo = MessagePackConvert.Deserialize<RazorProjectInfo>(bytes, options);

        // Assert
        Assert.Equal(originalProjectInfo, actualProjectInfo);
    }

    [Theory]
    [InlineData("Kendo.Mvc.Examples.taghelpers.json", "Telerik")]
    [InlineData("taghelpers.json")]
    public void VerifyMessagePack_TagHelpers(string resourceName, string? folderName = null)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, folderName);

        // Read tag helpers from JSON
        var originalTagHelpers = JsonDataConvert.DeserializeTagHelperArray(resourceBytes);

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
    [InlineData("Kendo.Mvc.Examples.project.razor.json", "Telerik")]
    [InlineData("project.razor.json")]
    public void VerifyJson_RazorProjectInfo(string resourceName, string? folderName = null)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, folderName);

        // Read tag helpers from JSON
        var originalProjectInfo = JsonDataConvert.DeserializeProjectInfo(resourceBytes);

#if WRITE_JSON_FILES
        JsonDataConvert.SerializeToFile(originalProjectInfo, GetDesktopFilePath(resourceName), indented: true);
#endif

        // Act

        // Serialize to JSON
        var jsonText = JsonDataConvert.Serialize(originalProjectInfo);

        // Deserialize from JSON
        var actualProjectInfo = JsonDataConvert.DeserializeProjectInfo(jsonText);
        Assert.NotNull(actualProjectInfo);

        // Assert
        Assert.Equal(originalProjectInfo.ProjectKey, actualProjectInfo.ProjectKey);
        Assert.Equal(originalProjectInfo.FilePath, actualProjectInfo.FilePath);
        Assert.Equal(originalProjectInfo.Configuration, actualProjectInfo.Configuration);
        Assert.Equal(originalProjectInfo.RootNamespace, actualProjectInfo.RootNamespace);
        Assert.Equal(originalProjectInfo.ProjectWorkspaceState, actualProjectInfo.ProjectWorkspaceState);
        Assert.Equal<DocumentSnapshotHandle>(originalProjectInfo.Documents, actualProjectInfo.Documents);
    }

    [Theory]
    [InlineData("Kendo.Mvc.Examples.taghelpers.json", "Telerik")]
    [InlineData("taghelpers.json")]
    [InlineData("BlazorServerApp.TagHelpers.json")]
    public void VerifyJson_TagHelpers(string resourceName, string? folderName = null)
    {
        // Arrange
        var resourceBytes = RazorTestResources.GetResourceBytes(resourceName, folderName);

        // Read tag helpers from JSON
        var originalTagHelpers = JsonDataConvert.DeserializeTagHelperArray(resourceBytes);

#if WRITE_JSON_FILES
        JsonDataConvert.SerializeToFile(originalTagHelpers, GetDesktopFilePath(resourceName), indented: true);
#endif

        // Act

        // Serialize to JSON
        var jsonText = JsonDataConvert.Serialize(originalTagHelpers);

        // Deserialize from JSON
        var actualTagHelpers = JsonDataConvert.DeserializeTagHelperArray(jsonText);

        // Assert
        Assert.Equal<TagHelperDescriptor>(originalTagHelpers, actualTagHelpers);
    }

#if WRITE_JSON_FILES
    private static string GetDesktopFilePath(string fileName)
        => System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), fileName);
#endif
}
