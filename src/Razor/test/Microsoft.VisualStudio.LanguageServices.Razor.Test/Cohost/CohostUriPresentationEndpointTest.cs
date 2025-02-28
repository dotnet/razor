﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostUriPresentationEndpointTest(FuseTestContext context, ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper), IClassFixture<FuseTestContext>
{
    [FuseFact]
    public async Task RandomFile()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            uris: [FileUri("SomeRandomFile.txt")],
            // In reality this would actual insert the full path, but the Html server does that for us, and we
            // have other tests that validate that we insert what the Html server tells us
            expected: null);
    }

    [FuseFact]
    public async Task HtmlResponse_TranslatesVirtualDocumentUri()
    {
        var siteCssFileUriString = "file:///C:/path/to/site.css";
        var htmlTag = $"""<link src="{siteCssFileUriString}" rel="stylesheet" />""";

        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            uris: [new(siteCssFileUriString)],
            htmlResponse: new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new()
                    {
                        TextDocument = new()
                        {
                            Uri = FileUri("File1.razor.g.html")
                        },
                        Edits = [LspFactory.CreateTextEdit(position: (0, 0), htmlTag)]
                    }
                }
            },
            expected: htmlTag);
    }

    [FuseFact]
    public async Task Component()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            uris: [FileUri("Component.razor")],
            expected: "<Component />");
    }

    [FuseFact]
    public async Task ImportsFile()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            uris: [FileUri("_Imports.razor")],
            expected: null);
    }

    [FuseFact]
    public async Task Html_IntoCSharp_NoTag()
    {
        var siteCssFileUriString = "file:///C:/path/to/site.css";
        var htmlTag = $"""<link src="{siteCssFileUriString}" rel="stylesheet" />""";

        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                </div>

                @code {
                    [||]
                }
                """,
            uris: [new(siteCssFileUriString)],
            htmlResponse: new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new()
                    {
                        TextDocument = new()
                        {
                            Uri = FileUri("File1.razor.g.html")
                        },
                        Edits = [LspFactory.CreateTextEdit(position: (0, 0), htmlTag)]
                    }
                }
            },
            expected: null);
    }

    [FuseFact]
    public async Task Component_IntoCSharp_NoTag()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                </div>

                @code {
                    [||]
                }
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            uris: [FileUri("Component.razor")],
            expected: null);
    }

    [FuseFact]
    public async Task Component_WithChildFile()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            uris: [
                FileUri("Component.razor"),
                FileUri("Component.razor.css"),
                FileUri("Component.razor.js")
            ],
            expected: "<Component />");
    }

    [FuseFact]
    public async Task Component_WithChildFile_RazorNotFirst()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            uris: [
                FileUri("Component.razor.css"),
                FileUri("Component.razor"),
                FileUri("Component.razor.js")
            ],
            expected: "<Component />");
    }

    [FuseFact]
    public async Task Component_RequiredParameter()
    {
        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"),
                    """
                    @code
                    {
                        [Parameter]
                        [EditorRequired]
                        public string RequiredParameter { get; set; }
                    
                        [Parameter]
                        public string NormalParameter { get; set; }
                    }
                    """)
            ],
            uris: [FileUri("Component.razor")],
            expected: """<Component RequiredParameter="" />""");
    }

    private async Task VerifyUriPresentationAsync(string input, Uri[] uris, string? expected, WorkspaceEdit? htmlResponse = null, (string fileName, string contents)[]? additionalFiles = null)
    {
        UpdateClientInitializationOptions(c => c with { ForceRuntimeCodeGeneration = context.ForceRuntimeCodeGeneration });

        TestFileMarkupParser.GetSpan(input, out input, out var span);
        var document = CreateProjectAndRazorDocument(input, additionalFiles: additionalFiles);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var requestInvoker = new TestLSPRequestInvoker([(VSInternalMethods.TextDocumentUriPresentationName, htmlResponse)]);

        var endpoint = new CohostUriPresentationEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, FilePathService, requestInvoker);

        var request = new VSInternalUriPresentationParams()
        {
            TextDocument = new TextDocumentIdentifier()
            {
                Uri = document.CreateUri()
            },
            Range = sourceText.GetRange(span),
            Uris = uris
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (expected is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);
            Assert.NotNull(result.DocumentChanges);
            Assert.Equal(expected, ((TextEdit)result.DocumentChanges.Value.First[0].Edits[0]).NewText);
            Assert.Equal(document.CreateUri(), result.DocumentChanges.Value.First[0].TextDocument.Uri);
        }
    }
}
