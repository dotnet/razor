// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostFoldingRangeEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public Task IfStatements()
        => VerifyFoldingRangesAsync("""
            <div>
              @if (true) {[|
                <div>
                  Hello World
                </div>
              }|]
            </div>

            @if (true) {[|
              <div>
                Hello World
              </div>
            }|]

            @if (true) {[|
            }|]
            """);

    [Fact]
    public Task LockStatement()
        => VerifyFoldingRangesAsync("""
            @lock (new object()) {[|
            }|]
            """);

    [Fact]
    public Task UsingStatement()
      => VerifyFoldingRangesAsync("""
            @using (new object()) {[|
            }|]
            """);

    [Fact]
    public Task IfElseStatements()
        => VerifyFoldingRangesAsync("""
            <div>
              @if (true) {[|
                <div>
                  Hello World
                </div>
                else {[|
                <div>
                    Goodbye World
                </div>
                }|]
              }|]
            </div>
            """);

    [Fact]
    public Task Usings()
        => VerifyFoldingRangesAsync("""
            @using System[|
            @using System.Text|]

            <p>hello!</p>

            @using System.Buffers[|
            @using System.Drawing
            @using System.CodeDom|]

            <p>hello!</p>
            """);

    [Fact]
    public Task CSharpStatement()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @{[|
                var helloWorld = "";
            }|]

            @(DateTime
                .Now)

            <p>hello!</p>
            """);

    [Fact]
    public Task CSharpStatement_Nested()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            <div>

                @{[|
                    var helloWorld = "";
                }|]

            </div>

            @(DateTime
                .Now)

            <p>hello!</p>
            """);

    [Fact]
    public Task CSharpStatement_NotSingleLine()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @{ var helloWorld = ""; }

            <p>hello!</p>
            """);

    [Fact]
    public Task CodeBlock()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @code {[|
                var helloWorld = "";
            }|]

            <p>hello!</p>
            """);

    [Fact]
    public Task CodeBlock_Mvc()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @functions {[|
                var helloWorld = "";
            }|]

            <p>hello!</p>
            """,
            fileKind: FileKinds.Legacy);

    [Fact]
    public Task Section()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @section Hello {[|
                <p>Hello</p>
            }|]

            <p>hello!</p>
            """,
            fileKind: FileKinds.Legacy);

    [Fact]
    public Task Section_Invalid()
        => VerifyFoldingRangesAsync("""
            <p>hello!</p>

            @section {
                <p>Hello</p>
            }

            <p>hello!</p>
            """,
            fileKind: FileKinds.Legacy);

    [Fact]
    public Task CSharpCodeInCodeBlocks()
       => VerifyFoldingRangesAsync("""
            <div>
              Hello @_name
            </div>

            @code {[|
                private string _name = "Dave";

                public void M() {[|
                }|]
            }|]
            """);

    private async Task VerifyFoldingRangesAsync(string input, string? fileKind = null)
    {
        TestFileMarkupParser.GetSpans(input, out var source, out ImmutableArray<TextSpan> expected);
        var document = CreateProjectAndRazorDocument(source, fileKind);

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentFoldingRangeName, Array.Empty<FoldingRange>())]);

        var endpoint = new CohostFoldingRangeEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker, LoggerFactory);

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(document, DisposalToken);

        if (expected.Length == 0)
        {
            Assert.Null(result);
            return;
        }

        // Rather than comparing numbers and spans, its nicer to reconstruct the test input data and use string comparisons so we can
        // more easily understand what went wrong.

        var inputText = SourceText.From(source);
        var markerPositions = result
            .SelectMany(r =>
                new[] {
                    (index: inputText.GetRequiredAbsoluteIndex(r.StartLine, r.StartCharacter.AssumeNotNull()), isStart: true),
                    (index: inputText.GetRequiredAbsoluteIndex(r.EndLine, r.EndCharacter.AssumeNotNull()), isStart: false)
                });

        var actual = new StringBuilder(source);
        foreach (var marker in markerPositions.OrderByDescending(p => p.index))
        {
            actual.Insert(marker.index, marker.isStart ? "[|" : "|]");
        }

        AssertEx.EqualOrDiff(input, actual.ToString());
    }
}
