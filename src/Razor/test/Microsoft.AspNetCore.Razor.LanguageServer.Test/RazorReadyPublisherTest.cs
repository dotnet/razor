// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

using Moq;

using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    public class RazorReadyPublisherTest : LanguageServerTestBase
    {
        [Fact]
        public void ProjectSnapshotManager_WorkspaceNull_DoesNothing()
        {
            // Arrange
            var languageServer = new Mock<IClientLanguageServer>(MockBehavior.Strict);

            var razorReadyPublisher = new RazorReadyPublisher(Dispatcher, languageServer.Object);

            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            projectManager.AllowNotifyListeners = true;

            razorReadyPublisher.Initialize(projectManager);

            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            _ = document.TryGetText(out var text);
            _ = document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);

            // Act
            projectManager.ProjectAdded(document.ProjectInternal.HostProject);
            projectManager.DocumentAdded(document.ProjectInternal.HostProject, document.State.HostDocument, TextLoader.From(textAndVersion));

            // Assert
            // Should not have been called
            languageServer.Verify();
        }

        private const string _razorReadyEndpoint = "razor/razorReady";

        [Fact]
        public void ProjectSnapshotManager_WorkspacePopulated_SetsUIContext()
        {
            // Arrange
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            _ = responseRouterReturns.Setup(r => r.ReturningVoid(It.IsAny<CancellationToken>())).Returns(() => Task.CompletedTask);

            var languageServer = new Mock<IClientLanguageServer>(MockBehavior.Strict);
            _ = languageServer.Setup(l => l.SendRequest(_razorReadyEndpoint))
                .Returns(responseRouterReturns.Object);

            var razorReadyPublisher = new RazorReadyPublisher(Dispatcher, languageServer.Object);

            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            projectManager.AllowNotifyListeners = true;

            razorReadyPublisher.Initialize(projectManager);

            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            _ = document.TryGetText(out var text);
            _ = document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);

            // Act
            projectManager.ProjectAdded(document.ProjectInternal.HostProject);
            projectManager.ProjectWorkspaceStateChanged(document.ProjectInternal.HostProject.FilePath, CreateProjectWorkspace());

            // Assert
            languageServer.VerifyAll();
            responseRouterReturns.VerifyAll();
        }

        [Fact]
        public void ProjectSnapshotManager_WorkspacePopulated_DoesNotFireTwice()
        {
            // Arrange
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            _ = responseRouterReturns.Setup(r => r.ReturningVoid(It.IsAny<CancellationToken>()))
                .Returns(() => Task.CompletedTask);

            var languageServer = new Mock<IClientLanguageServer>(MockBehavior.Strict);
            _ = languageServer.Setup(l => l.SendRequest(_razorReadyEndpoint))
                .Returns(responseRouterReturns.Object);

            var razorReadyPublisher = new RazorReadyPublisher(Dispatcher, languageServer.Object);

            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            projectManager.AllowNotifyListeners = true;

            razorReadyPublisher.Initialize(projectManager);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            _ = document.TryGetText(out var text);
            _ = document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);

            projectManager.ProjectAdded(document.ProjectInternal.HostProject);

            // Act
            projectManager.ProjectWorkspaceStateChanged(document.ProjectInternal.HostProject.FilePath, CreateProjectWorkspace());

            languageServer.VerifyAll();
            responseRouterReturns.VerifyAll();

            projectManager.ProjectWorkspaceStateChanged(document.ProjectInternal.HostProject.FilePath, CreateProjectWorkspace());

            // Assert
            languageServer.VerifyAll();
            responseRouterReturns.Verify(r => r.ReturningVoid(It.IsAny<CancellationToken>()), Times.Once);
        }

        private ProjectWorkspaceState CreateProjectWorkspace()
        {
            var tagHelpers = new List<TagHelperDescriptor>();

            return new ProjectWorkspaceState(tagHelpers, default);
        }
    }
}
