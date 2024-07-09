// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Text;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Test.Cohost;

public class CohostTestBase(ITestOutputHelper testOutputHelper) : WorkspaceTestBase(testOutputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    private IRemoteServiceProvider? _remoteServiceProvider;

    internal IRemoteServiceProvider RemoteServiceProvider => _remoteServiceProvider.AssumeNotNull();

    protected override Task InitializeAsync()
    {
        _remoteServiceProvider = new ShortCircuitingRemoteServiceProvider(_testOutputHelper);

        return base.InitializeAsync();
    }

    protected TextDocument CreateRazorDocument(string input)
    {
        var hostProject = TestProjectData.SomeProject;
        var hostDocument = TestProjectData.SomeProjectComponentFile1;

        var sourceText = SourceText.From(input);

        var solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(Path.GetFileNameWithoutExtension(hostProject.FilePath)),
            VersionStamp.Create(),
            Path.GetFileNameWithoutExtension(hostDocument.FilePath),
            Path.GetFileNameWithoutExtension(hostDocument.FilePath),
            LanguageNames.CSharp,
            hostDocument.FilePath));

        solution = solution.AddAdditionalDocument(
            DocumentId.CreateNewId(solution.ProjectIds.Single(), hostDocument.FilePath),
            hostDocument.FilePath,
            sourceText,
            filePath: hostDocument.FilePath);

        var document = solution.Projects.Single().AdditionalDocuments.Single();

        return document;
    }
}
