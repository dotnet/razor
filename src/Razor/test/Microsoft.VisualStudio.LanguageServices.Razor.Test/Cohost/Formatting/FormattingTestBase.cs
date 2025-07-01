﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces.Settings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Razor.Settings;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.Formatting;

public abstract class FormattingTestBase : CohostEndpointTestBase
{
    private readonly FormattingTestContext _context;
    private readonly HtmlFormattingService _htmlFormattingService;

    private protected FormattingTestBase(FormattingTestContext context, HtmlFormattingService htmlFormattingService, ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        ITestOnlyLoggerExtensions.TestOnlyLoggingEnabled = true;

        _context = context;
        _htmlFormattingService = htmlFormattingService;
    }

    private protected async Task RunFormattingTestAsync(
        TestCode input,
        string expected,
        RazorFileKind? fileKind = null,
        bool inGlobalNamespace = false,
        bool codeBlockBraceOnNextLine = false,
        bool insertSpaces = true,
        int tabSize = 4,
        bool allowDiagnostics = false,
        bool debugAssertsEnabled = true,
        RazorCSharpSyntaxFormattingOptions? formattingOptionsOverride = null)
    {
        (input, expected) = ProcessFormattingContext(input, expected);

        var document = CreateProjectAndRazorDocument(input.Text, fileKind, inGlobalNamespace: inGlobalNamespace);
        if (!allowDiagnostics)
        {
            //TODO: Tests in LanguageServer have extra components that are not present in this project, like Counter, etc.
            //      so we can't validate for diagnostics here until we make them the same. Since the test inputs are all
            //      shared this doesn't really matter while the language server tests are present.
            //var snapshotManager = OOPExportProvider.GetExportedValue<RemoteSnapshotManager>();
            //var snapshot = snapshotManager.GetSnapshot(document);
            //var codeDocument = await snapshot.GetGeneratedOutputAsync(DisposalToken);
            //var csharpDocument = codeDocument.GetCSharpDocument();
            //Assert.False(csharpDocument.Diagnostics.Any(), "Error creating document:" + Environment.NewLine + string.Join(Environment.NewLine, csharpDocument.Diagnostics));
        }

        var formattingService = (RazorFormattingService)OOPExportProvider.GetExportedValue<IRazorFormattingService>();
        var accessor = formattingService.GetTestAccessor();
        accessor.SetDebugAssertsEnabled(debugAssertsEnabled);
        accessor.SetCSharpSyntaxFormattingOptionsOverride(formattingOptionsOverride);

        var generatedHtml = await RemoteServiceInvoker.TryInvokeAsync<IRemoteHtmlDocumentService, string?>(document.Project.Solution,
            (service, solutionInfo, ct) => service.GetHtmlDocumentTextAsync(solutionInfo, document.Id, ct),
            DisposalToken).ConfigureAwait(false);
        Assert.NotNull(generatedHtml);

        var uri = new Uri(document.CreateUri(), $"{document.FilePath}{FeatureOptions.HtmlVirtualDocumentSuffix}");
        var htmlEdits = await _htmlFormattingService.GetDocumentFormattingEditsAsync(LoggerFactory, uri, generatedHtml, insertSpaces, tabSize);

        var requestInvoker = new TestHtmlRequestInvoker([(Methods.TextDocumentFormattingName, htmlEdits)]);

        var clientSettingsManager = new ClientSettingsManager(changeTriggers: []);
        clientSettingsManager.Update(clientSettingsManager.GetClientSettings().AdvancedSettings with { CodeBlockBraceOnNextLine = codeBlockBraceOnNextLine });

        var span = input.TryGetNamedSpans(string.Empty, out var spans)
            ? spans.First()
            : default;
        var edits = await GetFormattingEditsAsync(span, insertSpaces, tabSize, document, requestInvoker, clientSettingsManager);

        if (edits is null)
        {
            AssertEx.EqualOrDiff(expected, input.Text);
            return;
        }

        var inputText = await document.GetTextAsync(DisposalToken);
        var changes = edits.Select(inputText.GetTextChange);
        var finalText = inputText.WithChanges(changes);

        AssertEx.EqualOrDiff(expected, finalText.ToString());
    }

    private protected async Task RunOnTypeFormattingTestAsync(
        TestCode input,
        string expected,
        char triggerCharacter,
        bool inGlobalNamespace = false,
        bool insertSpaces = true,
        int tabSize = 4,
        RazorFileKind? fileKind = null,
        int? expectedChangedLines = null)
    {
        (input, expected) = ProcessFormattingContext(input, expected);

        var document = CreateProjectAndRazorDocument(input.Text, fileKind: fileKind, inGlobalNamespace: inGlobalNamespace);
        var inputText = await document.GetTextAsync(DisposalToken);
        var position = inputText.GetPosition(input.Position);

        var generatedHtml = await RemoteServiceInvoker.TryInvokeAsync<IRemoteHtmlDocumentService, string?>(document.Project.Solution,
            (service, solutionInfo, ct) => service.GetHtmlDocumentTextAsync(solutionInfo, document.Id, ct),
            DisposalToken).ConfigureAwait(false);
        Assert.NotNull(generatedHtml);

        var uri = new Uri(document.CreateUri(), $"{document.FilePath}{FeatureOptions.HtmlVirtualDocumentSuffix}");
        var htmlEdits = await _htmlFormattingService.GetOnTypeFormattingEditsAsync(LoggerFactory, uri, generatedHtml, position, insertSpaces: true, tabSize: 4);

        var requestInvoker = new TestHtmlRequestInvoker([(Methods.TextDocumentOnTypeFormattingName, htmlEdits)]);

        var clientSettingsManager = new ClientSettingsManager(changeTriggers: []);

        var endpoint = new CohostOnTypeFormattingEndpoint(IncompatibleProjectService, RemoteServiceInvoker, requestInvoker, clientSettingsManager, LoggerFactory);

        var request = new DocumentOnTypeFormattingParams()
        {
            TextDocument = new TextDocumentIdentifier() { DocumentUri = new(document.CreateUri()) },
            Options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces
            },
            Character = triggerCharacter.ToString(),
            Position = position
        };

        var edits = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (edits is null)
        {
            Assert.Equal(expected, input.Text);
            return;
        }

        var changes = edits.Select(inputText.GetTextChange);
        var finalText = inputText.WithChanges(changes);

        AssertEx.EqualOrDiff(expected, finalText.ToString());

        if (expectedChangedLines is { } changedLines)
        {
            var firstLine = changes.Min(e => inputText.GetLinePositionSpan(e.Span).Start.Line);
            var lastLine = changes.Max(e => inputText.GetLinePositionSpan(e.Span).End.Line);
            var delta = lastLine - firstLine + changes.Count(e => e.NewText.AssumeNotNull().Contains(Environment.NewLine));
            Assert.Equal(changedLines, delta + 1);
        }
    }

    private (TestCode, string) ProcessFormattingContext(TestCode input, string expected)
    {
        Assert.True(_context.CreatedByFormattingDiscoverer, "Test class is using FormattingTestContext, but not using [FormattingTestFact] or [FormattingTestTheory]");

        if (_context.ShouldFlipLineEndings)
        {
            // flip the line endings of the stings (LF to CRLF and vice versa) and run again
            input = new TestCode(_context.FlipLineEndings(input.OriginalInput));
            expected = _context.FlipLineEndings(expected);
        }

        return (input, expected);
    }

    private async Task<TextEdit[]?> GetFormattingEditsAsync(TextSpan span, bool insertSpaces, int tabSize, TextDocument document, IHtmlRequestInvoker requestInvoker, IClientSettingsManager clientSettingsManager)
    {
        if (span.IsEmpty)
        {
            var endpoint = new CohostDocumentFormattingEndpoint(IncompatibleProjectService, RemoteServiceInvoker, requestInvoker, clientSettingsManager, LoggerFactory);
            var request = new DocumentFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { DocumentUri = new(document.CreateUri()) },
                Options = new FormattingOptions()
                {
                    TabSize = tabSize,
                    InsertSpaces = insertSpaces
                }
            };

            return await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);
        }

        var inputText = await document.GetTextAsync(DisposalToken);
        var rangeEndpoint = new CohostRangeFormattingEndpoint(IncompatibleProjectService, RemoteServiceInvoker, requestInvoker, clientSettingsManager, LoggerFactory);
        var rangeRequest = new DocumentRangeFormattingParams()
        {
            TextDocument = new TextDocumentIdentifier() { DocumentUri = new(document.CreateUri()) },
            Options = new FormattingOptions()
            {
                TabSize = 4,
                InsertSpaces = true
            },
            Range = inputText.GetRange(span)
        };

        return await rangeEndpoint.GetTestAccessor().HandleRequestAsync(rangeRequest, document, DisposalToken);
    }
}
