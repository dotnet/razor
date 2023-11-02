// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

public class GeneratedDocumentTextLoaderTest(ITestOutputHelper testOutput) : WorkspaceTestBase(testOutput)
{
    private readonly HostProject _hostProject = TestProjectData.SomeProject;
    private readonly HostDocument _hostDocument = TestProjectData.SomeProjectFile1;

    [Fact, WorkItem("https://github.com/dotnet/aspnetcore/issues/7997")]
    public async Task LoadAsync_SpecifiesEncoding()
    {
        // Arrange
        var project = new ProjectSnapshot(
            ProjectState.Create(_hostProject, ProjectEngineFactory)
            .WithAddedHostDocument(_hostDocument, () => Task.FromResult(TextAndVersion.Create(SourceText.From(""), VersionStamp.Create()))));

        var document = project.GetDocument(_hostDocument.FilePath);
        Assert.NotNull(document);

        var loader = new GeneratedDocumentTextLoader(document, "file.cshtml");

        // Act
        var textAndVersion = await loader.LoadTextAndVersionAsync(default, default);

        // Assert
        Assert.True(textAndVersion.Text.CanBeEmbedded);
        Assert.Same(Encoding.UTF8, textAndVersion.Text.Encoding);
    }
}
