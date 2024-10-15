// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Serialization.Json;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test;

public class SerializationTest : ToolingTestBase
{
    private readonly RazorConfiguration _configuration;
    private readonly ProjectWorkspaceState _projectWorkspaceState;

    public SerializationTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        var languageVersion = RazorLanguageVersion.Experimental;

        _configuration = new(languageVersion, "Custom", [new("TestExtension")]);
        _projectWorkspaceState = ProjectWorkspaceState.Create(
            tagHelpers: [TagHelperDescriptorBuilder.Create("Test", "TestAssembly").Build()],
            csharpLanguageVersion: LanguageVersion.LatestMajor);
    }

    [Fact]
    public void RazorProjectInfo_InvalidVersionThrows()
    {
        // Arrange
        var hostProject = new HostProject(
            filePath: "/path/to/project.csproj",
            intermediateOutputPath: "/path/to/obj/",
            _configuration,
            rootNamespace: "TestProject",
            displayName: "project");

        var projectInfo = new RazorProjectInfo(
            hostProject,
            _projectWorkspaceState,
            documents: []);

        var jsonText = JsonDataConvert.SerializeObject(projectInfo, ObjectWriters.WriteProperties);
        Assert.NotNull(jsonText);

        var serializedJObject = JObject.Parse(jsonText);
        serializedJObject[WellKnownPropertyNames.Version] = -1;

        var updatedJsonText = serializedJObject.ToString();
        Assert.NotNull(updatedJsonText);

        // Act
        RazorProjectInfo? deserializedProjectInfo = null;
        Assert.Throws<RazorProjectInfoSerializationException>(() =>
        {
            deserializedProjectInfo = JsonDataConvert.DeserializeObject(updatedJsonText, ObjectReaders.ReadProjectInfoFromProperties);
        });

        // Assert
        Assert.Null(deserializedProjectInfo);
    }

    [Fact]
    public void RazorProjectInfo_MissingVersionThrows()
    {
        // Arrange
        var hostProject = new HostProject(
            filePath: "/path/to/project.csproj",
            intermediateOutputPath: "/path/to/obj/",
            _configuration,
            rootNamespace: "TestProject",
            displayName: "project");

        var projectInfo = new RazorProjectInfo(
            hostProject,
            _projectWorkspaceState,
            documents: []);

        var jsonText = JsonDataConvert.SerializeObject(projectInfo, ObjectWriters.WriteProperties);
        Assert.NotNull(jsonText);

        var serializedJObject = JObject.Parse(jsonText);
        serializedJObject.Remove(WellKnownPropertyNames.Version);

        var updatedJsonText = serializedJObject.ToString();
        Assert.NotNull(updatedJsonText);

        // Act
        RazorProjectInfo? deserializedProjectInfo = null;
        Assert.Throws<RazorProjectInfoSerializationException>(() =>
        {
            deserializedProjectInfo = JsonDataConvert.DeserializeObject(updatedJsonText, ObjectReaders.ReadProjectInfoFromProperties);
        });

        // Assert
        Assert.Null(deserializedProjectInfo);
    }

    [Fact]
    public void RazorProjectInfo_CanRoundTrip()
    {
        // Arrange
        var hostProject = new HostProject(
            filePath: "/path/to/project.csproj",
            intermediateOutputPath: "/path/to/obj/",
            _configuration,
            rootNamespace: "TestProject",
            displayName: "project");

        var legacyDocument = new HostDocument("/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        var componentDocument = new HostDocument("/path/to/otherfile.razor", "otherfile.razor", FileKinds.Component);
        var projectInfo = new RazorProjectInfo(
            hostProject,
            _projectWorkspaceState,
            documents: [legacyDocument, componentDocument]);

        var jsonText = JsonDataConvert.SerializeObject(projectInfo, ObjectWriters.WriteProperties);
        Assert.NotNull(jsonText);

        // Act
        var deserializedProjectInfo = JsonDataConvert.DeserializeObject(jsonText, ObjectReaders.ReadProjectInfoFromProperties);
        Assert.NotNull(deserializedProjectInfo);

        // Assert
        Assert.Equal(projectInfo.HostProject, deserializedProjectInfo.HostProject);
        Assert.Equal(projectInfo.ProjectWorkspaceState, deserializedProjectInfo.ProjectWorkspaceState);
        Assert.Collection(projectInfo.Documents.OrderBy(static d => d.FilePath),
            document => Assert.Equal(document, legacyDocument),
            document => Assert.Equal(document, componentDocument));
    }

    [Fact]
    public void RazorConfiguration_CanRoundTrip()
    {
        // Arrange
        var jsonText = JsonDataConvert.SerializeObject(_configuration, ObjectWriters.WriteProperties);
        Assert.NotNull(jsonText);

        // Act
        var deserializedConfiguration = JsonDataConvert.DeserializeObject(jsonText, ObjectReaders.ReadConfigurationFromProperties);

        // Assert
        Assert.Equal(_configuration, deserializedConfiguration);
    }
}
