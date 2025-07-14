// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Testing;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostMapCodeEndpointTest(ITestOutputHelper testOutput) : CohostEndpointTestBase(testOutput)
{
    [Fact]
    public async Task HandleRazorSingleLineInsertionAsync()
    {
        var originalCode = """
                <h3>Component</h3>
                $$
                @code {

                }
                
                """;

        var codeToMap = """
            <PageTitle>Title</PageTitle>
            """;

        var expectedCode = """
            <h3>Component</h3>
            <PageTitle>Title</PageTitle>
            @code {

            }
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleCSharpInsertionAsync()
    {
        var originalCode = """
                @code
                {
                    public string Title { get; set; }
                    $$
                }

                """;

        var codeToMap = """
            @code
            {
                void M()
                {
                    var x = 1;
                }
            }
            """;

        var expectedCode = """
            @code
            {
                public string Title { get; set; }
                
                void M()
                {
                    var x = 1;
                }

            }
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleHtmlMultiLineInsertionAsync()
    {
        var originalCode = """
                <h3>Component</h3>
                $$
                @code {

                }
                
                """;

        var codeToMap = """
            <h1>Title</h1>
            <h2>Subtitle</h2>
            """;

        var expectedCode = """
            <h3>Component</h3>
            <h1>Title</h1><h2>Subtitle</h2>
            @code {

            }
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleIgnoreExistingCodeAsync()
    {
        var originalCode = """
                @page "/"

                <PageTitle>Index</PageTitle>

                <h1>Hello, world!</h1>

                $$

                Welcome to your new app.
                
                """;

        var codeToMap = """
            @page "/"

            <PageTitle>Index</PageTitle>

            <h1>Hello, world!</h1>

            <button>Click me</button>

            Welcome to your new app.
            
            """;

        var expectedCode = """
            @page "/"
            
            <PageTitle>Index</PageTitle>
            
            <h1>Hello, world!</h1>
            
            <button>Click me</button>
            
            Welcome to your new app.
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleMultipleFocusLocationsAsync()
    {
        var originalCode = """
                <h3>Component</h3>
                $$
                @code {

                }
                
                """;

        var codeToMap = """
            <PageTitle>Title</PageTitle>
            """;

        var expectedCode = """
            <h3>Component</h3>
            <PageTitle>Title</PageTitle>
            @code {
            
            }
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleFocusLocationInMiddleOfNodeAsync()
    {
        var originalCode = """
                <h3>Component</h$$3>

                @code {

                }
                
                """;

        var codeToMap = """
            <PageTitle>Title</PageTitle>
            """;

        // Code mapper isn't responsible for formatting
        var expectedCode = """
            <h3>Component</h3><PageTitle>Title</PageTitle>
            
            @code {
            
            }
            
            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    [Fact]
    public async Task HandleRazorDirectiveAttributeAsync()
    {
        var originalCode = """
                @page "/fetchdata"
                @using Microsoft.AspNetCore.Authorization
                $$
                """;

        var codeToMap = """
            @attribute [Authorize]

            """;

        var expectedCode = """
            @page "/fetchdata"
            @using Microsoft.AspNetCore.Authorization
            @attribute [Authorize]

            """;

        await VerifyCodeMappingAsync(originalCode, [codeToMap], expectedCode);
    }

    private protected override TestComposition ConfigureRoslynDevenvComposition(TestComposition composition)
    {
        return composition.AddParts(typeof(TestMapCodeService));
    }

    private async Task VerifyCodeMappingAsync(TestCode input, string[] codeToMap, string expected)
    {
        UpdateClientLSPInitializationOptions(c =>
        {
            c.ClientCapabilities.Workspace ??= new();
            c.ClientCapabilities.Workspace.WorkspaceEdit ??= new();
            c.ClientCapabilities.Workspace.WorkspaceEdit.DocumentChanges = true;

            return c;
        });

        var document = CreateProjectAndRazorDocument(input.Text, createSeparateRemoteAndLocalWorkspaces: true);

        var endpoint = new CohostMapCodeEndpoint(ClientCapabilitiesService, NoOpTelemetryReporter.Instance, RemoteServiceInvoker);

        var sourceText = await document.GetTextAsync(DisposalToken);

        var mappings = new VSInternalMapCodeMapping[]
        {
            new() {
                TextDocument = new TextDocumentIdentifier
                {
                    DocumentUri = document.CreateDocumentUri()
                },
                FocusLocations = [
                    [
                        new LspLocation
                        {
                            Range = sourceText.GetZeroWidthRange(input.Position),
                            DocumentUri = document.CreateDocumentUri()
                        }
                    ]
                ],
                Contents = codeToMap
            }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(document.Project.Solution, mappings, Guid.NewGuid(), DisposalToken);

        Assert.NotNull(result);

        var actualCode = ApplyWorkspaceEdit(result, document.CreateDocumentUri(), sourceText);
        AssertEx.EqualOrDiff(expected, actualCode.ToString());
    }

    private static SourceText ApplyWorkspaceEdit(WorkspaceEdit workspaceEdit, DocumentUri documentUri, SourceText sourceText)
    {
        Assert.NotNull(workspaceEdit.DocumentChanges);
        var edits = workspaceEdit.DocumentChanges.Value.First;

        foreach (var edit in edits)
        {
            Assert.Equal(documentUri, edit.TextDocument.DocumentUri);

            foreach (var currentEdit in edit.Edits)
            {
                sourceText = sourceText.WithChanges(sourceText.GetTextChange((TextEdit)currentEdit));
            }
        }

        return sourceText;
    }
}
