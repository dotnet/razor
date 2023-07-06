// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class DefaultDocumentContextFactoryTest : LanguageServerTestBase
{
    private readonly DocumentVersionCache _documentVersionCache;

    public DefaultDocumentContextFactoryTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _documentVersionCache = new DefaultDocumentVersionCache();
    }

    [Fact]
    public async Task TryCreateAsync_CanNotResolveDocument_ReturnsNull()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.cshtml");
        var factory = new DefaultDocumentContextFactory(Dispatcher, CreateSnapshotResolver(), _documentVersionCache, LoggerFactory);

        // Act
        var documentContext = await factory.TryCreateAsync(uri, DisposalToken);

        // Assert
        Assert.Null(documentContext);
    }

    [Fact]
    public async Task TryCreateForOpenDocumentAsync_CanNotResolveDocument_ReturnsNull()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.cshtml");
        var factory = new DefaultDocumentContextFactory(Dispatcher, CreateSnapshotResolver(), _documentVersionCache, LoggerFactory);

        // Act
        var documentContext = await factory.TryCreateForOpenDocumentAsync(uri, DisposalToken);

        // Assert
        Assert.Null(documentContext);
    }

    [Fact]
    public async Task TryCreateForOpenDocumentAsync_CanNotResolveVersion_ReturnsNull()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.cshtml");
        var documentSnapshot = TestDocumentSnapshot.Create(uri.GetAbsoluteOrUNCPath());
        var documentResolver = CreateSnapshotResolver(documentSnapshot);
        var factory = new DefaultDocumentContextFactory(Dispatcher, documentResolver, _documentVersionCache, LoggerFactory);

        // Act
        var documentContext = await factory.TryCreateForOpenDocumentAsync(uri, DisposalToken);

        // Assert
        Assert.Null(documentContext);
    }

    [Fact]
    public async Task TryCreateAsync_ResolvesContent()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.cshtml");
        var documentSnapshot = TestDocumentSnapshot.Create(uri.GetAbsoluteOrUNCPath());
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create(string.Empty, documentSnapshot.FilePath));
        documentSnapshot.With(codeDocument);
        var documentResolver = CreateSnapshotResolver(documentSnapshot);
        var factory = new DefaultDocumentContextFactory(Dispatcher, documentResolver, _documentVersionCache, LoggerFactory);

        // Act
        var documentContext = await factory.TryCreateAsync(uri, DisposalToken);

        // Assert
        Assert.NotNull(documentContext);
        Assert.Equal(uri, documentContext.Uri);
        Assert.Equal(documentSnapshot.FilePath, documentContext.Snapshot.FilePath);
        Assert.Equal(documentSnapshot.FileKind, documentContext.Snapshot.FileKind);
        Assert.Equal(documentSnapshot.State.HostDocument, ((DocumentSnapshot)documentContext.Snapshot).State.HostDocument);
    }

    [Fact]
    public async Task TryCreateForOpenDocumentAsync_ResolvesContent()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.cshtml");
        var documentSnapshot = TestDocumentSnapshot.Create(uri.GetAbsoluteOrUNCPath());
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create(string.Empty, documentSnapshot.FilePath));
        documentSnapshot.With(codeDocument);
        var documentResolver = CreateSnapshotResolver(documentSnapshot);
        await Dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            Assert.True(documentResolver.TryResolveDocument(documentSnapshot.HostDocument.FilePath, includeMiscellaneous: false, out var snapshot));
            _documentVersionCache.TrackDocumentVersion(snapshot, version: 1337);
        }
        , DisposalToken);

        var factory = new DefaultDocumentContextFactory(Dispatcher, documentResolver, _documentVersionCache, LoggerFactory);

        // Act
        var documentContext = await factory.TryCreateForOpenDocumentAsync(uri, DisposalToken);

        // Assert
        Assert.NotNull(documentContext);
        Assert.Equal(1337, documentContext.Version);
        Assert.Equal(uri, documentContext.Uri);
        Assert.Equal(documentSnapshot.FilePath, documentContext.Snapshot.FilePath);
        Assert.Equal(documentSnapshot.FileKind, documentContext.Snapshot.FileKind);
        Assert.Equal(documentSnapshot.State.HostDocument, ((DocumentSnapshot)documentContext.Snapshot).State.HostDocument);
    }

    private SnapshotResolver CreateSnapshotResolver(params TestDocumentSnapshot[] snapshots)
    {
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter));

        foreach (var project in snapshots.Select(s => s.Project).Cast<TestProjectSnapshot>().Distinct())
        {
            projectSnapshotManagerAccessor.Instance.ProjectAdded(project.HostProject);
        }

        foreach (var document in snapshots)
        {
            var project = (TestProjectSnapshot)document.ProjectInternal;
            projectSnapshotManagerAccessor.Instance.DocumentAdded(project.HostProject, document.HostDocument, new EmptyTextLoader(document.FilePath));
        }

        return new SnapshotResolver(projectSnapshotManagerAccessor, new LoggerAdapter(new[] {Logger}, null, null));
    }
}
