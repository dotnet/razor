// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostRenameEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public Task CSharp_Method()
        => VerifyRenamesAsync(
            input: """
                This is a Razor document.

                <h1>@MyMethod()</h1>

                @code
                {
                    public string MyMe$$thod()
                    {
                        return $"Hi from {nameof(MyMethod)}";
                    }
                }

                The end.
                """,
            newName: "CallThisFunction",
            expected: """
                This is a Razor document.
                
                <h1>@CallThisFunction()</h1>
                
                @code
                {
                    public string CallThisFunction()
                    {
                        return $"Hi from {nameof(CallThisFunction)}";
                    }
                }
                
                The end.
                """);

    [Theory]
    [InlineData("$$Component")]
    [InlineData("Com$$ponent")]
    [InlineData("Component$$")]
    public Task Component_StartTag(string startTag)
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <Component />

                <div>
                    <{startTag} />
                    <Component>
                    </Component>
                    <div>
                        <Component />
                        <Component>
                        </Component>
                    </div>
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            newName: "DifferentName",
            expected: """
                This is a Razor document.

                <DifferentName />
                
                <div>
                    <DifferentName />
                    <DifferentName>
                    </DifferentName>
                    <div>
                        <DifferentName />
                        <DifferentName>
                        </DifferentName>
                    </div>
                </div>

                The end.
                """,
            renames: [("Component.razor", "DifferentName.razor")]);

    [Theory]
    [InlineData("$$Component")]
    [InlineData("Com$$ponent")]
    [InlineData("Component$$")]
    public Task Component_EndTag(string endTag)
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <Component />

                <div>
                    <Component />
                    <Component>
                    </Component>
                    <div>
                        <Component />
                        <Component>
                        </{endTag}>
                    </div>
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            newName: "DifferentName",
            expected: """
                This is a Razor document.

                <DifferentName />

                <div>
                    <DifferentName />
                    <DifferentName>
                    </DifferentName>
                    <div>
                        <DifferentName />
                        <DifferentName>
                        </DifferentName>
                    </div>
                </div>

                The end.
                """,
            renames: [("Component.razor", "DifferentName.razor")]);

    [Fact]
    public Task Component_Attribute()
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <Component Tit$$le="Hello1" />

                <div>
                    <Component Title="Hello2" />
                    <Component Title="Hello3">
                    </Component>
                    <div>
                        <Component Title="Hello4"/>
                        <Component Title="Hello5">
                        </Component>
                    </div>
                </div>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), """
                    <div></div>

                    @code {
                        [Parameter]
                        public string Title { get; set; }
                    }

                    """)
            ],
            newName: "Name",
            expected: """
                This is a Razor document.
                
                <Component Name="Hello1" />
                
                <div>
                    <Component Name="Hello2" />
                    <Component Name="Hello3">
                    </Component>
                    <div>
                        <Component Name="Hello4"/>
                        <Component Name="Hello5">
                        </Component>
                    </div>
                </div>
                
                The end.
                """,
             additionalExpectedFiles: [
                (FileUri("Component.razor"), """
                    <div></div>

                    @code {
                        [Parameter]
                        public string Name { get; set; }
                    }

                    """)
            ]);

    [Fact]
    public Task Mvc()
        => VerifyRenamesAsync(
            input: """
                This is a Razor document.

                <Com$$ponent />

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), "")
            ],
            newName: "DifferentName",
            expected: "",
            fileKind: RazorFileKind.Legacy);

    [Fact]
    public Task Component_WithContent()
      => VerifyRenamesAsync(
          input: $"""
                This is a Razor document.

                <Component>Hello</Compon$$ent>
                <Component>
                    Hello
                </Component>

                The end.
                """,
          additionalFiles: [
              (FilePath("Component.razor"), "")
          ],
          newName: "DifferentName",
          expected: """
                This is a Razor document.

                <DifferentName>Hello</DifferentName>
                <DifferentName>
                    Hello
                </DifferentName>

                The end.
                """,
          renames: [
              ("Component.razor", "DifferentName.razor")
          ]);

    [Fact]
    public Task Component_WithContent_FullyQualified()
      => VerifyRenamesAsync(
          input: $"""
                This is a Razor document.

                <My.Namespace.Component>Hello</My.Namespace.Compon$$ent>
                <My.Namespace.Component>
                    Hello
                </My.Namespace.Component>

                The end.
                """,
          additionalFiles: [
              (FilePath("Component.razor"), """
                    @namespace My.Namespace
                    """)
          ],
          newName: "DifferentName",
          expected: """
                This is a Razor document.

                <My.Namespace.DifferentName>Hello</My.Namespace.DifferentName>
                <My.Namespace.DifferentName>
                    Hello
                </My.Namespace.DifferentName>

                The end.
                """,
          renames: [
              ("Component.razor", "DifferentName.razor")
          ]);

    [Fact]
    public Task Component_MultipleUsage()
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <Comp$$onent />
                <Component></Component>
                <Component>
                </Component>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), ""),
                (FilePath("OtherComponent.razor"), """
                    <Component />
                    <Component></Component>
                    <Component>
                    </Component>
                    """)
            ],
            newName: "DifferentName",
            expected: """
                This is a Razor document.

                <DifferentName />
                <DifferentName></DifferentName>
                <DifferentName>
                </DifferentName>

                The end.
                """,
            renames: [
                ("Component.razor", "DifferentName.razor")
            ],
            additionalExpectedFiles: [
                (FileUri("OtherComponent.razor"), """
                    <DifferentName />
                    <DifferentName></DifferentName>
                    <DifferentName>
                    </DifferentName>
                    """)
            ]);

    [Fact]
    public Task Component_FullyQualified()
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <My.Namespace.Comp$$onent />
                <My.Namespace.Component></My.Namespace.Component>
                <My.Namespace.Component>
                </My.Namespace.Component>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), """
                    @namespace My.Namespace
                    """)
            ],
            newName: "DifferentName",
            expected: """
                This is a Razor document.

                <My.Namespace.DifferentName />
                <My.Namespace.DifferentName></My.Namespace.DifferentName>
                <My.Namespace.DifferentName>
                </My.Namespace.DifferentName>

                The end.
                """,
            renames: [
                ("Component.razor", "DifferentName.razor")
            ]);

    [Fact]
    public Task Component_MultipleUsage_FullyQualified()
        => VerifyRenamesAsync(
            input: $"""
                This is a Razor document.

                <My.Namespace.Comp$$onent />
                <My.Namespace.Component></My.Namespace.Component>
                <My.Namespace.Component>
                </My.Namespace.Component>

                The end.
                """,
            additionalFiles: [
                (FilePath("Component.razor"), """
                    @namespace My.Namespace
                    """),
                (FilePath("OtherComponent.razor"), """
                    <My.Namespace.Component />
                    <My.Namespace.Component></My.Namespace.Component>
                    <My.Namespace.Component>
                    </My.Namespace.Component>
                    """)
            ],
            newName: "DifferentName",
            expected: """
                This is a Razor document.

                <My.Namespace.DifferentName />
                <My.Namespace.DifferentName></My.Namespace.DifferentName>
                <My.Namespace.DifferentName>
                </My.Namespace.DifferentName>

                The end.
                """,
            renames: [
                ("Component.razor", "DifferentName.razor")
            ],
            additionalExpectedFiles: [
                (FileUri("OtherComponent.razor"), """
                    <My.Namespace.DifferentName />
                    <My.Namespace.DifferentName></My.Namespace.DifferentName>
                    <My.Namespace.DifferentName>
                    </My.Namespace.DifferentName>
                    """)
            ]);

    private async Task VerifyRenamesAsync(
        string input,
        string newName,
        string expected,
        RazorFileKind? fileKind = null,
        (string fileName, string contents)[]? additionalFiles = null,
        (string oldName, string newName)[]? renames = null,
        (Uri fileUri, string contents)[]? additionalExpectedFiles = null)
    {
        TestFileMarkupParser.GetPosition(input, out var source, out var cursorPosition);
        var document = CreateProjectAndRazorDocument(source, fileKind, additionalFiles: additionalFiles);
        var inputText = await document.GetTextAsync(DisposalToken);
        var position = inputText.GetPosition(cursorPosition);

        var requestInvoker = new TestHtmlRequestInvoker([(Methods.TextDocumentRenameName, (object?)null)]);

        var endpoint = new CohostRenameEndpoint(IncompatibleProjectService, RemoteServiceInvoker, requestInvoker);

        var renameParams = new RenameParams
        {
            Position = position,
            TextDocument = new TextDocumentIdentifier { DocumentUri = document.CreateDocumentUri() },
            NewName = newName,
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(renameParams, document, DisposalToken);

        if (expected.Length == 0)
        {
            Assert.True(renames is null or []);
            Assert.Null(result);
            return;
        }

        Assert.NotNull(result);

        if (result.DocumentChanges.AssumeNotNull().TryGetSecond(out var changes))
        {
            Assert.NotNull(renames);

            var expectedRenames = renames.ToList();
            foreach (var change in changes)
            {
                if (change.TryGetThird(out var renameEdit))
                {
                    var found = Assert.Single(renames,
                        r => renameEdit.OldDocumentUri.GetRequiredParsedUri().GetDocumentFilePath().EndsWith(r.oldName) &&
                             renameEdit.NewDocumentUri.GetRequiredParsedUri().GetDocumentFilePath().EndsWith(r.newName));
                    expectedRenames.Remove(found);
                }
            }

            Assert.Empty(expectedRenames);
        }

        var expectedChanges = (additionalExpectedFiles ?? []).Concat([(document.CreateUri(), expected)]);
        await VerifyWorkspaceEditAsync(result, document.Project.Solution, expectedChanges, DisposalToken);
    }

    private static async Task VerifyWorkspaceEditAsync(WorkspaceEdit workspaceEdit, Solution solution, IEnumerable<(Uri fileUri, string contents)> expectedChanges, CancellationToken cancellationToken)
    {
        Assert.True(workspaceEdit.TryGetTextDocumentEdits(out var textDocumentEdits));
        foreach (var textDocumentEdit in textDocumentEdits)
        {
            var (uri, _) = expectedChanges.Single(e => e.fileUri == textDocumentEdit.TextDocument.DocumentUri.GetRequiredParsedUri());

            var document = solution.GetTextDocuments(uri).First();
            var text = await document.GetTextAsync(cancellationToken);

            text = text.WithChanges(textDocumentEdit.Edits.Select(e => text.GetTextChange((TextEdit)e)));

            solution = solution.WithAdditionalDocumentText(document.Id, text);
        }

        foreach (var (uri, contents) in expectedChanges)
        {
            var document = solution.GetTextDocuments(uri).First();
            var text = await document.GetTextAsync(cancellationToken);
            AssertEx.EqualOrDiff(contents, text.ToString());
        }
    }
}
