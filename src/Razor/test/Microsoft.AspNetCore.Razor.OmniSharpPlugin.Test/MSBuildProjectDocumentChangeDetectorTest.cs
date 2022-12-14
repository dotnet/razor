﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin;

public class MSBuildProjectDocumentChangeDetectorTest : OmniSharpTestBase
{
    public MSBuildProjectDocumentChangeDetectorTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void FileSystemWatcher_RazorDocumentEvent_InvokesOutputListeners()
    {
        // Arrange
        var projectInstance = new ProjectInstance(ProjectRootElement.Create());
        void AssertCallbackArgs(RazorFileChangeEventArgs args)
        {
            Assert.Equal("/path/to/file.cshtml", args.FilePath);
            Assert.Equal(RazorFileChangeKind.Removed, args.Kind);
            Assert.Same(projectInstance, args.UnevaluatedProjectInstance);
        }

        var listener1 = new Mock<IRazorDocumentChangeListener>(MockBehavior.Strict);
        listener1.Setup(listener => listener.RazorDocumentChanged(It.IsAny<RazorFileChangeEventArgs>()))
            .Callback<RazorFileChangeEventArgs>((args) => AssertCallbackArgs(args))
            .Verifiable();
        var listener2 = new Mock<IRazorDocumentChangeListener>(MockBehavior.Strict);
        listener2.Setup(listener => listener.RazorDocumentChanged(It.IsAny<RazorFileChangeEventArgs>()))
            .Callback<RazorFileChangeEventArgs>((args) => AssertCallbackArgs(args))
            .Verifiable();
        var detector = new MSBuildProjectDocumentChangeDetector(
            new[] { listener1.Object, listener2.Object },
            Enumerable.Empty<IRazorDocumentOutputChangeListener>());

        // Act
        detector.FileSystemWatcher_RazorDocumentEvent("/path/to/file.cshtml", projectInstance, RazorFileChangeKind.Removed);

        // Assert
        listener1.VerifyAll();
        listener2.VerifyAll();
    }

    [Fact]
    public void FileSystemWatcher_RazorDocumentOutputEvent_InvokesOutputListeners()
    {
        // Arrange
        var projectInstance = new ProjectInstance(ProjectRootElement.Create());
        void AssertCallbackArgs(RazorFileChangeEventArgs args)
        {
            Assert.Equal("/path/to/file.cshtml", args.FilePath);
            Assert.Equal(RazorFileChangeKind.Removed, args.Kind);
            Assert.Same(projectInstance, args.UnevaluatedProjectInstance);
        }

        var listener1 = new Mock<IRazorDocumentOutputChangeListener>(MockBehavior.Strict);
        listener1.Setup(listener => listener.RazorDocumentOutputChanged(It.IsAny<RazorFileChangeEventArgs>()))
            .Callback<RazorFileChangeEventArgs>((args) => AssertCallbackArgs(args))
            .Verifiable();
        var listener2 = new Mock<IRazorDocumentOutputChangeListener>(MockBehavior.Strict);
        listener2.Setup(listener => listener.RazorDocumentOutputChanged(It.IsAny<RazorFileChangeEventArgs>()))
            .Callback<RazorFileChangeEventArgs>((args) => AssertCallbackArgs(args))
            .Verifiable();
        var detector = new MSBuildProjectDocumentChangeDetector(
            Enumerable.Empty<IRazorDocumentChangeListener>(),
            new[] { listener1.Object, listener2.Object });

        // Act
        detector.FileSystemWatcher_RazorDocumentOutputEvent("/path/to/file.cshtml", projectInstance, RazorFileChangeKind.Removed);

        // Assert
        listener1.VerifyAll();
        listener2.VerifyAll();
    }
}
