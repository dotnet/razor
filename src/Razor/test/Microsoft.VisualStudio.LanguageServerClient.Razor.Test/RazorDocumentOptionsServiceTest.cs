﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test
{
    public class RazorDocumentOptionsServiceTest : WorkspaceTestBase
    {
        [Fact]
        public async Task RazorDocumentOptionsService_ReturnsCorrectOptions_UseTabs()
        {
            // Arrange
            var editorSettings = new EditorSettings(indentWithTabs: true, indentSize: 4);
            var clientOptionsMonitor = new RazorLSPClientOptionsMonitor();
            clientOptionsMonitor.UpdateOptions(editorSettings);
            var optionsService = new RazorDocumentOptionsService(clientOptionsMonitor);

            var document = InitializeDocument(SourceText.From("text"));

            var useTabsOptionKey = GetUseTabsOptionKey(document);
            var tabSizeOptionKey = GetTabSizeOptionKey(document);
            var indentationSizeOptionKey = GetIndentationSizeOptionKey(document);

            // Act
            var documentOptions = await optionsService.GetOptionsForDocumentAsync(document, CancellationToken.None);
            documentOptions.TryGetDocumentOption(useTabsOptionKey, out var useTabs);
            documentOptions.TryGetDocumentOption(tabSizeOptionKey, out var tabSize);
            documentOptions.TryGetDocumentOption(indentationSizeOptionKey, out var indentationSize);

            // Assert
            Assert.True((bool)useTabs);
            Assert.Equal(4, (int)tabSize);
            Assert.Equal(4, (int)indentationSize);
        }

        [Fact]
        public async Task RazorDocumentOptionsService_ReturnsCorrectOptions_UseSpaces()
        {
            // Arrange
            var editorSettings = new EditorSettings(indentWithTabs: false, indentSize: 2);
            var clientOptionsMonitor = new RazorLSPClientOptionsMonitor();
            clientOptionsMonitor.UpdateOptions(editorSettings);
            var optionsService = new RazorDocumentOptionsService(clientOptionsMonitor);

            var document = InitializeDocument(SourceText.From("text"));

            var useTabsOptionKey = GetUseTabsOptionKey(document);
            var tabSizeOptionKey = GetTabSizeOptionKey(document);
            var indentationSizeOptionKey = GetIndentationSizeOptionKey(document);

            // Act
            var documentOptions = await optionsService.GetOptionsForDocumentAsync(document, CancellationToken.None);
            documentOptions.TryGetDocumentOption(useTabsOptionKey, out var useTabs);
            documentOptions.TryGetDocumentOption(tabSizeOptionKey, out var tabSize);
            documentOptions.TryGetDocumentOption(indentationSizeOptionKey, out var indentationSize);

            // Assert
            Assert.False((bool)useTabs);
            Assert.Equal(2, (int)tabSize);
            Assert.Equal(2, (int)indentationSize);
        }

        private static OptionKey GetUseTabsOptionKey(Document document)
            => new OptionKey(FormattingOptions.UseTabs, document.Project.Language);

        private static OptionKey GetTabSizeOptionKey(Document document)
            => new OptionKey(FormattingOptions.TabSize, document.Project.Language);

        private static OptionKey GetIndentationSizeOptionKey(Document document)
            => new OptionKey(FormattingOptions.IndentationSize, document.Project.Language);

        // Adapted from DocumentExcerptServiceTestBase's InitializeDocument.
        // Adds the text to a ProjectSnapshot, generates code, and updates the workspace.
        private Document InitializeDocument(SourceText sourceText)
        {
            var baseDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "c:\\users\\example\\src" : "/home/example";
            var hostProject = new HostProject(
                Path.Combine(baseDirectory, "SomeProject", "SomeProject.csproj"), RazorConfiguration.Default, "SomeProject");
            var hostDocument = new HostDocument(
                Path.Combine(baseDirectory, "SomeProject", "File1.cshtml"), "File1.cshtml", FileKinds.Legacy);

            var project = new DefaultProjectSnapshot(
                ProjectState.Create(Workspace.Services, hostProject)
                .WithAddedHostDocument(hostDocument, () => Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Create()))));

            var documentSnapshot = project.GetDocument(hostDocument.FilePath);

            var solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(Path.GetFileNameWithoutExtension(hostDocument.FilePath)),
                VersionStamp.Create(),
                Path.GetFileNameWithoutExtension(hostDocument.FilePath),
                Path.GetFileNameWithoutExtension(hostDocument.FilePath),
                LanguageNames.CSharp,
                hostDocument.FilePath));

            solution = solution.AddDocument(
                DocumentId.CreateNewId(solution.ProjectIds.Single(), hostDocument.FilePath),
                hostDocument.FilePath,
                new GeneratedDocumentTextLoader(documentSnapshot, hostDocument.FilePath));

            var document = solution.Projects.Single().Documents.Single();
            return document;
        }
    }
}
