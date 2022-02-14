// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    // Sets the FileName static variable.
    // Finds the test method name using reflection, and uses
    // that to find the expected input/output test files in the file system.
    [IntializeTestFile]

    // These tests must be run serially due to the test specific FileName static var.
    [Collection("FormattingTestSerialRuns")]
    public class FormattingTestBase : RazorIntegrationTestBase
    {
        private static readonly AsyncLocal<string> s_fileName = new AsyncLocal<string>();
        private static readonly IReadOnlyList<TagHelperDescriptor> s_defaultComponents = GetDefaultRuntimeComponents();

        public FormattingTestBase(ITestOutputHelper output)
        {
            TestProjectPath = GetProjectDirectory();
            FilePathNormalizer = new FilePathNormalizer();
            LoggerFactory = new FormattingTestLoggerFactory(output);
        }

        public static string? TestProjectPath { get; private set; }

        protected FilePathNormalizer FilePathNormalizer { get; }

        protected ILoggerFactory LoggerFactory { get; }

        // Used by the test framework to set the 'base' name for test files.
        public static string FileName
        {
            get { return s_fileName.Value!; }
            set { s_fileName.Value = value; }
        }

        protected async Task RunFormattingTestAsync(
            string input,
            string expected,
            int tabSize = 4,
            bool insertSpaces = true,
            string? fileKind = null,
            IReadOnlyList<TagHelperDescriptor>? tagHelpers = null,
            bool useSourceTextDiffer = false,
            bool allowDiagnostics = false)
        {
            // Arrange
            fileKind ??= FileKinds.Component;

            TestFileMarkupParser.GetSpans(input, out input, out ImmutableArray<TextSpan> spans);
            var span = spans.IsEmpty ? new TextSpan(0, input.Length) : spans.Single();

            var source = SourceText.From(input);
            var range = span.AsRange(source);

            var path = "file:///path/to/Document." + fileKind;
            var uri = new Uri(path);
            var (codeDocument, documentSnapshot) = CreateCodeDocumentAndSnapshot(source, uri.AbsolutePath, tagHelpers, fileKind, allowDiagnostics);
            var options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces,
            };

            if (useSourceTextDiffer)
            {
                options["UseSourceTextDiffer"] = true;
            }

            var formattingService = CreateFormattingService(codeDocument);

            // Act
            var edits = await formattingService.FormatAsync(uri, documentSnapshot, range, options, CancellationToken.None);

            // Assert
            var edited = ApplyEdits(source, edits);
            var actual = edited.ToString();

            new XUnitVerifier().EqualOrDiff(expected, actual);
        }

        protected async Task RunOnTypeFormattingTestAsync(
            string input,
            string expected,
            char triggerCharacter,
            int tabSize = 4,
            bool insertSpaces = true,
            string? fileKind = null)
        {
            // Arrange
            fileKind ??= FileKinds.Component;

            TestFileMarkupParser.GetPosition(input, out input, out var positionAfterTrigger);

            var razorSourceText = SourceText.From(input);
            var path = "file:///path/to/Document.razor";
            var uri = new Uri(path);
            var (codeDocument, documentSnapshot) = CreateCodeDocumentAndSnapshot(razorSourceText, uri.AbsolutePath, fileKind: fileKind);

            var mappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var languageKind = mappingService.GetLanguageKind(codeDocument, positionAfterTrigger);

            if (!mappingService.TryMapToProjectedDocumentPosition(codeDocument, positionAfterTrigger, out _, out var projectedIndex))
            {
                throw new InvalidOperationException("Could not map from Razor document to generated document");
            }

            var projectedEdits = Array.Empty<TextEdit>();
            if (languageKind == RazorLanguageKind.CSharp)
            {
                projectedEdits = await GetFormattedCSharpEditsAsync(
                    codeDocument, triggerCharacter, projectedIndex, insertSpaces, tabSize).ConfigureAwait(false);
            }
            else if (languageKind == RazorLanguageKind.Html)
            {
                throw new NotImplementedException("OnTypeFormatting is not yet supported for HTML in Razor.");
            }

            var formattingService = CreateFormattingService(codeDocument);
            var options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces,
            };

            // Act
            var edits = await formattingService.FormatOnTypeAsync(uri, documentSnapshot, languageKind, projectedEdits, options, CancellationToken.None);

            // Assert
            if (input.Equals(expected))
            {
                Assert.Empty(edits);
            }
            else
            {
                var edited = ApplyEdits(razorSourceText, edits);
                var actual = edited.ToString();

                new XUnitVerifier().EqualOrDiff(expected, actual);
            }
        }

        protected async Task RunCodeActionFormattingTestAsync(
            string input,
            TextEdit[] codeActionEdits,
            string expected,
            int tabSize = 4,
            bool insertSpaces = true,
            string? fileKind = null)
        {
            if (codeActionEdits is null)
            {
                throw new NotImplementedException("Code action formatting must provide edits.");
            }

            // Arrange
            fileKind ??= FileKinds.Component;

            TestFileMarkupParser.GetPosition(input, out input, out var positionAfterTrigger);

            var razorSourceText = SourceText.From(input);
            var path = "file:///path/to/Document.razor";
            var uri = new Uri(path);
            var (codeDocument, documentSnapshot) = CreateCodeDocumentAndSnapshot(razorSourceText, uri.AbsolutePath, fileKind: fileKind);

#pragma warning disable CS0618 // Type or member is obsolete
            var mappingService = new DefaultRazorDocumentMappingService();
#pragma warning restore CS0618 // Type or member is obsolete
            var languageKind = mappingService.GetLanguageKind(codeDocument, positionAfterTrigger);
            if (languageKind == RazorLanguageKind.Html)
            {
                throw new NotImplementedException("Code action formatting is not yet supported for HTML in Razor.");
            }

            if (!mappingService.TryMapToProjectedDocumentPosition(codeDocument, positionAfterTrigger, out _, out var _))
            {
                throw new InvalidOperationException("Could not map from Razor document to generated document");
            }

            var formattingService = CreateFormattingService(codeDocument);
            var options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces,
            };

            // Act
            var edits = await formattingService.FormatCodeActionAsync(uri, documentSnapshot, languageKind, codeActionEdits, options, CancellationToken.None);

            // Assert
            var edited = ApplyEdits(razorSourceText, edits);
            var actual = edited.ToString();

            new XUnitVerifier().EqualOrDiff(expected, actual);
        }

        protected static TextEdit Edit(int startLine, int startChar, int endLine, int endChar, string newText)
            => new TextEdit()
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(startLine, startChar, endLine, endChar),
                NewText = newText
            };

        private static async Task<TextEdit[]> GetFormattedCSharpEditsAsync(
            RazorCodeDocument codeDocument,
            char typedChar,
            int position,
            bool insertSpaces,
            int tabSize)
        {
            var generatedCode = codeDocument.GetCSharpDocument().GeneratedCode;
            var csharpSourceText = SourceText.From(generatedCode);
            var document = GenerateRoslynCSharpDocument(csharpSourceText);
            var documentOptions = await GetDocumentOptionsAsync(document, insertSpaces, tabSize);
            var formattingChanges = await RazorCSharpFormattingInteractionService.GetFormattingChangesAsync(
                document, typedChar, position, documentOptions, CancellationToken.None).ConfigureAwait(false);

            var textEdits = formattingChanges.Select(change => change.AsTextEdit(csharpSourceText)).ToArray();
            return textEdits;

            Document GenerateRoslynCSharpDocument(SourceText csharpSourceText)
            {
                var workspace = TestWorkspace.Create();
                var project = workspace.CurrentSolution.AddProject("TestProject", "TestAssembly", LanguageNames.CSharp);
                var document = project.AddDocument("TestDocument", csharpSourceText);
                return document;
            }

            async Task<DocumentOptionSet> GetDocumentOptionsAsync(Document document, bool insertSpaces, int tabSize)
            {
                var documentOptions = await document.GetOptionsAsync().ConfigureAwait(false);
                documentOptions = documentOptions.WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.TabSize, tabSize)
                    .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.IndentationSize, tabSize)
                    .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.UseTabs, !insertSpaces);
                return documentOptions;
            }
        }

        private RazorFormattingService CreateFormattingService(RazorCodeDocument codeDocument)
        {
            var mappingService = new DefaultRazorDocumentMappingService(LoggerFactory);

            var client = new FormattingLanguageServerClient();
            client.AddCodeDocument(codeDocument);
            var passes = new List<IFormattingPass>()
            {
                new HtmlFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new CSharpFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new CSharpOnTypeFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new RazorFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new FormattingDiagnosticValidationPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new FormattingContentValidationPass(mappingService, FilePathNormalizer, client, LoggerFactory),
            };

            return new DefaultRazorFormattingService(passes, LoggerFactory, TestAdhocWorkspaceFactory.Instance);
        }

        private static SourceText ApplyEdits(SourceText source, TextEdit[] edits)
        {
            var changes = edits.Select(e => e.AsTextChange(source));
            return source.WithChanges(changes);
        }

        private static (RazorCodeDocument, DocumentSnapshot) CreateCodeDocumentAndSnapshot(SourceText text, string path, IReadOnlyList<TagHelperDescriptor>? tagHelpers = null, string? fileKind = default, bool allowDiagnostics = false)
        {
            fileKind ??= FileKinds.Component;
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            if (fileKind == FileKinds.Component)
            {
                tagHelpers = tagHelpers.Concat(s_defaultComponents).ToArray();
            }

            var sourceDocument = text.GetRazorSourceDocument(path, path);

            // Yes I know "BlazorServer_31 is weird, but thats what is in the taghelpers.json file
            const string DefaultImports = @"
@using BlazorServer_31
@using BlazorServer_31.Pages
@using BlazorServer_31.Shared
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
";

            var importsPath = new Uri("file:///path/to/_Imports.razor").AbsolutePath;
            var importsSourceText = SourceText.From(DefaultImports);
            var importsDocument = importsSourceText.GetRazorSourceDocument(importsPath, importsPath);
            var importsSnapshot = new Mock<DocumentSnapshot>(MockBehavior.Strict);
            importsSnapshot.Setup(d => d.GetTextAsync()).Returns(Task.FromResult(importsSourceText));
            importsSnapshot.Setup(d => d.FilePath).Returns(importsPath);
            importsSnapshot.Setup(d => d.TargetPath).Returns(importsPath);

            var projectEngine = RazorProjectEngine.Create(builder =>
            {
                builder.SetRootNamespace("Test");
                builder.Features.Add(new DefaultTypeNameFeature());
                RazorExtensions.Register(builder);
            });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, new[] { importsDocument }, tagHelpers);

            if (!allowDiagnostics)
            {
                Assert.False(codeDocument.GetCSharpDocument().Diagnostics.Any(), "Error creating document:" + Environment.NewLine + string.Join(Environment.NewLine, codeDocument.GetCSharpDocument().Diagnostics));
            }

            var documentSnapshot = new Mock<DocumentSnapshot>(MockBehavior.Strict);
            documentSnapshot.Setup(d => d.GetGeneratedOutputAsync()).Returns(Task.FromResult(codeDocument));
            documentSnapshot.Setup(d => d.GetImports()).Returns(new[] { importsSnapshot.Object });
            documentSnapshot.Setup(d => d.Project.GetProjectEngine()).Returns(projectEngine);
            documentSnapshot.Setup(d => d.FilePath).Returns(path);
            documentSnapshot.Setup(d => d.TargetPath).Returns(path);
            documentSnapshot.Setup(d => d.Project.TagHelpers).Returns(tagHelpers);
            documentSnapshot.Setup(d => d.FileKind).Returns(fileKind);

            return (codeDocument, documentSnapshot.Object);
        }

        private static string GetProjectDirectory()
        {
            var repoRoot = SearchUp(AppContext.BaseDirectory, "global.json");
            if (repoRoot is null)
            {
                repoRoot = AppContext.BaseDirectory;
            }

            var assemblyName = typeof(FormattingTestBase).Assembly.GetName().Name;
            var projectDirectory = Path.Combine(repoRoot, "src", "Razor", "test", assemblyName!);

            return projectDirectory;
        }

        private static string? SearchUp(string baseDirectory, string fileName)
        {
            var directoryInfo = new DirectoryInfo(baseDirectory);
            do
            {
                var fileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, fileName));
                if (fileInfo.Exists)
                {
                    return fileInfo.DirectoryName;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo?.Parent != null);

            return null;
        }

        private static IReadOnlyList<TagHelperDescriptor> GetDefaultRuntimeComponents()
        {
            var testFileName = "test.taghelpers.json";
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, testFileName)))
            {
                current = current.Parent;
            }

            var tagHelperFilePath = Path.Combine(current!.FullName, testFileName);
            var buffer = File.ReadAllBytes(tagHelperFilePath);
            var serializer = new JsonSerializer();
            serializer.Converters.Add(new TagHelperDescriptorJsonConverter());

            using var stream = new MemoryStream(buffer);
            using var streamReader = new StreamReader(stream);
            using var reader = new JsonTextReader(streamReader);

            return serializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader)!;
        }
    }
}
