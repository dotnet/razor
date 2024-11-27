// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;
using Microsoft.VisualStudio.Threading;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class FormattingTestBase(ITestOutputHelper testOutputHelper)
    : CohostEndpointTestBase(testOutputHelper)
{
    // All of the formatting tests in the language server exercise the formatting engine and cover various edge cases
    // and provide regression prevention. The tests here are not exhaustive, but they validate the the cohost endpoints
    // call into the formatting engine at least, and handles C#, Html and Razor formatting changes correctly.

    [Theory]
    [CombinatorialData]
    public Task Formatting(bool fuse)
        => RunFormattingTestAsync(
            input: """
            @preservewhitespace    true

                        <div></div>

            @{
            <p>
                    @{
                            var t = 1;
            if (true)
            {
            
                        }
                    }
                    </p>
            <div>
             @{
                <div>
            <div>
                    This is heavily nested
            </div>
             </div>
                }
                    </div>
            }

            @code {
                            private void M(string thisIsMyString)
                {
                    var x = 5;

                                var y = "Hello";

                    M("Hello");
                }
            }

            """,
            expected: """
            @preservewhitespace true

            <div></div>

            @{
                <p>
                    @{
                        var t = 1;
                        if (true)
                        {
            
                        }
                    }
                </p>
                <div>
                    @{
                        <div>
                            <div>
                                This is heavily nested
                            </div>
                        </div>
                    }
                </div>
            }

            @code {
                private void M(string thisIsMyString)
                {
                    var x = 5;

                    var y = "Hello";

                    M("Hello");
                }
            }

            """,
            fuse: fuse);

    protected async Task RunFormattingTestAsync(
        string input,
        string expected,
        string? fileKind = null,
        bool fuse = false,
        bool inGlobalNamespace = false,
        bool codeBlockBraceOnNextLine = false,
        bool insertSpaces = true,
        int tabSize = 4,
        bool allowDiagnostics = false,
        bool skipFlipLineEndingTest = false)
    {
        UpdateClientInitializationOptions(opt => opt with { ForceRuntimeCodeGeneration = fuse });

        TestFileMarkupParser.GetSpans(input, out input, out ImmutableArray<TextSpan> _);

        var document = await CreateProjectAndRazorDocumentAsync(input, fileKind, inGlobalNamespace: inGlobalNamespace);
        if (!allowDiagnostics)
        {
            // TODO: This doesn't work, but should when the source generator is hooked up
            var compilation = await document.Project.GetCompilationAsync(DisposalToken);
            var diagnostics = compilation.AssumeNotNull().GetDiagnostics(DisposalToken);
            Assert.False(diagnostics.Any(), "Error creating document:" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics));
        }

        var inputText = await document.GetTextAsync(DisposalToken);

        var htmlDocumentPublisher = new HtmlDocumentPublisher(RemoteServiceInvoker, StrictMock.Of<TrackingLSPDocumentManager>(), StrictMock.Of<JoinableTaskContext>(), LoggerFactory);
        var generatedHtml = await htmlDocumentPublisher.GetHtmlSourceFromOOPAsync(document, DisposalToken);
        Assert.NotNull(generatedHtml);

        var uri = new Uri(document.CreateUri(), $"{document.FilePath}{FeatureOptions.HtmlVirtualDocumentSuffix}");
        using var service = new HtmlFormattingService();
        var htmlEdits = await service.GetDocumentFormattingEditsAsync(LoggerFactory, uri, generatedHtml, insertSpaces: true, tabSize: 4);

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentFormattingName, htmlEdits)]);

        var clientSettingsManager = new ClientSettingsManager(changeTriggers: []);
        clientSettingsManager.Update(clientSettingsManager.GetClientSettings().AdvancedSettings with { CodeBlockBraceOnNextLine = codeBlockBraceOnNextLine });

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

        var edits = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        var changes = edits.Select(inputText.GetTextChange);
        var finalText = inputText.WithChanges(changes);

        AssertEx.EqualOrDiff(expected, finalText.ToString());
    }
}
