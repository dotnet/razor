// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;
using Microsoft.VisualStudio.Threading;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class FormattingTestBase : CohostEndpointTestBase
{
    private readonly FormattingTestContext _context;
    private readonly HtmlFormattingService _htmlFormattingService;

    internal FormattingTestBase(FormattingTestContext context, HtmlFormattingService htmlFormattingService, ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        ITestOnlyLoggerExtensions.TestOnlyLoggingEnabled = true;

        _context = context;
        _htmlFormattingService = htmlFormattingService;
    }

    private protected async Task RunFormattingTestAsync(
        TestCode input,
        string expected,
        string? fileKind = null,
        bool inGlobalNamespace = false,
        bool codeBlockBraceOnNextLine = false,
        bool insertSpaces = true,
        int tabSize = 4,
        bool allowDiagnostics = false)
    {
        Assert.True(_context.CreatedByFormattingDiscoverer, "Test class is using FormattingTestContext, but not using [FormattingTestFact] or [FormattingTestTheory]");

        UpdateClientInitializationOptions(opt => opt with { ForceRuntimeCodeGeneration = _context.ForceRuntimeCodeGeneration });

        if (_context.ShouldFlipLineEndings)
        {
            // flip the line endings of the stings (LF to CRLF and vice versa) and run again
            input = new TestCode(_context.FlipLineEndings(input.OriginalInput));
            expected = _context.FlipLineEndings(expected);
        }

        var document = await CreateProjectAndRazorDocumentAsync(input.Text, fileKind, inGlobalNamespace: inGlobalNamespace);
        if (!allowDiagnostics)
        {
            // TODO: This doesn't work, but should when the source generator is hooked up
            var compilation = await document.Project.GetCompilationAsync(DisposalToken);
            var diagnostics = compilation.AssumeNotNull().GetDiagnostics(DisposalToken);
            Assert.False(diagnostics.Any(), "Error creating document:" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics));
        }

        var htmlDocumentPublisher = new HtmlDocumentPublisher(RemoteServiceInvoker, StrictMock.Of<TrackingLSPDocumentManager>(), StrictMock.Of<JoinableTaskContext>(), LoggerFactory);
        var generatedHtml = await htmlDocumentPublisher.GetHtmlSourceFromOOPAsync(document, DisposalToken);
        Assert.NotNull(generatedHtml);

        var uri = new Uri(document.CreateUri(), $"{document.FilePath}{FeatureOptions.HtmlVirtualDocumentSuffix}");
        var htmlEdits = await _htmlFormattingService.GetDocumentFormattingEditsAsync(LoggerFactory, uri, generatedHtml, insertSpaces: true, tabSize: 4);

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentFormattingName, htmlEdits)]);

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

    private async Task<TextEdit[]?> GetFormattingEditsAsync(TextSpan span, bool insertSpaces, int tabSize, TextDocument document, LSPRequestInvoker requestInvoker, IClientSettingsManager clientSettingsManager)
    {
        if (span.IsEmpty)
        {
            var endpoint = new CohostDocumentFormattingEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker, clientSettingsManager, LoggerFactory);
            var request = new DocumentFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = document.CreateUri() },
                Options = new FormattingOptions()
                {
                    TabSize = tabSize,
                    InsertSpaces = insertSpaces
                }
            };

            return await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);
        }

        var inputText = await document.GetTextAsync(DisposalToken);
        var rangeEndpoint = new CohostRangeFormattingEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker, clientSettingsManager, LoggerFactory);
        var rangeRequest = new DocumentRangeFormattingParams()
        {
            TextDocument = new TextDocumentIdentifier() { Uri = document.CreateUri() },
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
