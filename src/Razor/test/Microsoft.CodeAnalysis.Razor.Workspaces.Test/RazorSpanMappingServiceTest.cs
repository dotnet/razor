﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor
{
    public class RazorSpanMappingServiceTest : WorkspaceTestBase
    {
        public RazorSpanMappingServiceTest()
        {
            HostProject = TestProjectData.SomeProject;
            HostDocument = TestProjectData.SomeProjectFile1;
        }

        private HostProject HostProject { get; }
        private HostDocument HostDocument { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            services.Add(new TestTagHelperResolver());
        }

        [Fact]
        public async Task TryGetMappedSpans_SpanMatchesSourceMapping_ReturnsTrue()
        {
            // Arrange
            var sourceText = SourceText.From(@"
@SomeProperty
");

            var project = new DefaultProjectSnapshot(
                ProjectState.Create(Workspace.Services, HostProject)
                .WithAddedHostDocument(HostDocument, () => Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Create()))));

            var document = project.GetDocument(HostDocument.FilePath);
            var service = new RazorSpanMappingService(document);

            var output = await document.GetGeneratedOutputAsync();
            var generated = output.GetCSharpDocument();

            var symbol = "SomeProperty";
            var span = new TextSpan(generated.GeneratedCode.IndexOf(symbol, StringComparison.Ordinal), symbol.Length);

            // Act
            var result = RazorSpanMappingService.TryGetMappedSpans(span, await document.GetTextAsync(), generated, out var mappedLinePositionSpan, out var mappedSpan);

            // Assert
            Assert.True(result);
            Assert.Equal(new LinePositionSpan(new LinePosition(1, 1), new LinePosition(1, 13)), mappedLinePositionSpan);
            Assert.Equal(new TextSpan(Environment.NewLine.Length + 1, symbol.Length), mappedSpan);
        }

        [Fact]
        public async Task TryGetMappedSpans_SpanMatchesSourceMappingAndPosition_ReturnsTrue()
        {
            // Arrange
            var sourceText = SourceText.From(@"
@SomeProperty
@SomeProperty
@SomeProperty
");

            var project = new DefaultProjectSnapshot(
                ProjectState.Create(Workspace.Services, HostProject)
                .WithAddedHostDocument(HostDocument, () => Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Create()))));

            var document = project.GetDocument(HostDocument.FilePath);
            var service = new RazorSpanMappingService(document);

            var output = await document.GetGeneratedOutputAsync();
            var generated = output.GetCSharpDocument();

            var symbol = "SomeProperty";
            // Second occurrence
            var span = new TextSpan(generated.GeneratedCode.IndexOf(symbol, generated.GeneratedCode.IndexOf(symbol, StringComparison.Ordinal) + symbol.Length, StringComparison.Ordinal), symbol.Length);

            // Act
            var result = RazorSpanMappingService.TryGetMappedSpans(span, await document.GetTextAsync(), generated, out var mappedLinePositionSpan, out var mappedSpan);

            // Assert
            Assert.True(result);
            Assert.Equal(new LinePositionSpan(new LinePosition(2, 1), new LinePosition(2, 13)), mappedLinePositionSpan);
            Assert.Equal(new TextSpan(Environment.NewLine.Length + 1 + symbol.Length + Environment.NewLine.Length + 1, symbol.Length), mappedSpan);
        }

        [Fact]
        public async Task TryGetMappedSpans_SpanWithinSourceMapping_ReturnsTrue()
        {
            // Arrange
            var sourceText = SourceText.From(@"
@{
    var x = SomeClass.SomeProperty;
}
");

            var project = new DefaultProjectSnapshot(
                ProjectState.Create(Workspace.Services, HostProject)
                .WithAddedHostDocument(HostDocument, () => Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Create()))));

            var document = project.GetDocument(HostDocument.FilePath);
            var service = new RazorSpanMappingService(document);

            var output = await document.GetGeneratedOutputAsync();
            var generated = output.GetCSharpDocument();

            var symbol = "SomeProperty";
            var span = new TextSpan(generated.GeneratedCode.IndexOf(symbol, StringComparison.Ordinal), symbol.Length);

            // Act
            var result = RazorSpanMappingService.TryGetMappedSpans(span, await document.GetTextAsync(), generated, out var mappedLinePositionSpan, out var mappedSpan);

            // Assert
            Assert.True(result);
            Assert.Equal(new LinePositionSpan(new LinePosition(2, 22), new LinePosition(2, 34)), mappedLinePositionSpan);
            Assert.Equal(new TextSpan(Environment.NewLine.Length + 2 + Environment.NewLine.Length + "    var x = SomeClass.".Length, symbol.Length), mappedSpan);
        }

        [Fact]
        public async Task TryGetMappedSpans_SpanOutsideSourceMapping_ReturnsFalse()
        {
            // Arrange
            var sourceText = SourceText.From(@"
@{
    var x = SomeClass.SomeProperty;
}
");

            var project = new DefaultProjectSnapshot(
                ProjectState.Create(Workspace.Services, HostProject)
                .WithAddedHostDocument(HostDocument, () => Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Create()))));

            var document = project.GetDocument(HostDocument.FilePath);
            var service = new RazorSpanMappingService(document);

            var output = await document.GetGeneratedOutputAsync();
            var generated = output.GetCSharpDocument();

            var symbol = "ExecuteAsync";
            var span = new TextSpan(generated.GeneratedCode.IndexOf(symbol, StringComparison.Ordinal), symbol.Length);

            // Act
            var result = RazorSpanMappingService.TryGetMappedSpans(span, await document.GetTextAsync(), generated, out var mappedLinePositionSpan, out var mappedSpan);

            // Assert
            Assert.False(result);
        }
    }
}
