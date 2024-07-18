// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostUriPresentationEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
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
            uris: [new("file:///C:/path/to/SomeRandomFile.txt")],
            // In reality this would actual insert the full path, but the Html server does that for us, and we
            // have other tests that validate that we insert what the Html server tells us
            expected: null);
    }

    [Fact]
    public async Task HtmlResponse_TranslatesVirtualDocumentUri()
    {
        var htmlTag = """<link src="file:///C:/path/to/site.css" rel="stylesheet" />""";

        await VerifyUriPresentationAsync(
            input: """
                This is a Razor document.

                <div>
                    [||]
                </div>

                The end.
                """,
            uris: [new("file:///C:/path/to/site.css")],
            htmlResponse: new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new()
                    {
                        TextDocument = new()
                        {
                            Uri = new("file:///c:/users/example/src/SomeProject/File1.razor.g.html")
                        },
                        Edits = [new() { NewText = htmlTag}]
                    }
                }
            },
            expected: htmlTag);
    }

    [Fact]
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
                // The source generator isn't hooked up to our test project, so we have to manually "compile" the razor file
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.cs"), """
                    namespace SomeProject;

                    public class Component : Microsoft.AspNetCore.Components.ComponentBase
                    {
                    }
                    """),
                // The above will make the component exist, but the .razor file needs to exist too for Uri presentation
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"), """
                    This doesn't matter
                    """)
            ],
            uris: [new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"))],
            expected: "<Component />");
    }

    [Fact]
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
                // The source generator isn't hooked up to our test project, so we have to manually "compile" the razor file
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.cs"), """
                    namespace SomeProject;

                    public class Component : Microsoft.AspNetCore.Components.ComponentBase
                    {
                    }
                    """),
                // The above will make the component exist, but the .razor file needs to exist too for Uri presentation
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"), """
                    This doesn't matter
                    """)
            ],
            uris: [new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"))],
            expected: null);
    }

    [Fact]
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
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.cs"), """
                    namespace SomeProject;

                    public class Component : Microsoft.AspNetCore.Components.ComponentBase
                    {
                    }
                    """),
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"), """
                    This doesn't matter
                    """)
            ],
            uris: [
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor")),
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor.css")),
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor.js"))
            ],
            expected: "<Component />");
    }

    [Fact]
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
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.cs"), """
                    namespace SomeProject;

                    public class Component : Microsoft.AspNetCore.Components.ComponentBase
                    {
                    }
                    """),
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"), """
                    This doesn't matter
                    """)
            ],
            uris: [
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor.css")),
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor")),
                new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor.js"))
            ],
            expected: "<Component />");
    }

    [Fact]
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
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.cs"), """
                    using Microsoft.AspNetCore.Components;

                    namespace SomeProject;

                    public class Component : ComponentBase
                    {
                        [Parameter]
                        [EditorRequired]
                        public string RequiredParameter { get; set; }

                        [Parameter]
                        public string NormalParameter { get; set; }
                    }
                    """),
                (Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"), """
                    This doesn't matter
                    """)
            ],
            uris: [new(Path.Combine(TestProjectData.SomeProjectPath, "Component.razor"))],
            expected: """<Component RequiredParameter="" />""");
    }

    private async Task VerifyUriPresentationAsync(string input, Uri[] uris, string? expected, WorkspaceEdit? htmlResponse = null, (string fileName, string contents)[]? additionalFiles = null)
    {
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
            Range = span.ToRange(sourceText),
            Uris = uris
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (result is null)
        {
            Assert.Null(expected);
        }
        else
        {
            Assert.Equal(expected, result!.DocumentChanges!.Value.First[0].Edits[0].NewText);
            Assert.Equal(document.CreateUri(), result?.DocumentChanges?.First[0].TextDocument.Uri);
        }
    }
}
