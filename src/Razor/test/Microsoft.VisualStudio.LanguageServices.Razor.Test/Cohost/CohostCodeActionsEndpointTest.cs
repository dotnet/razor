// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostCodeActionsEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task GenerateConstructor()
    {
        var input = """

            <div></div>

            @code
            {
                public class [||]Goo
                {
                }
            }

            """;

        var expected = """
            
            <div></div>
            
            @code
            {
                public class Goo
                {
                    public Goo()
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.GenerateDefaultConstructors);
    }

    [Fact]
    public async Task IntroduceLocal()
    {
        var input = """
            @using System.Linq

            <div></div>

            @code
            {
                void M(string[] args)
                {
                    if ([|args.First()|].Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        var expected = """
            @using System.Linq

            <div></div>
            
            @code
            {
                void M(string[] args)
                {
                    string v = args.First();
                    if (v.Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable);
    }

    [Fact]
    public async Task IntroduceLocal_All()
    {
        var input = """
            @using System.Linq

            <div></div>

            @code
            {
                void M(string[] args)
                {
                    if ([|args.First()|].Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        var expected = """
            @using System.Linq

            <div></div>
            
            @code
            {
                void M(string[] args)
                {
                    string v = args.First();
                    if (v.Length > 0)
                    {
                    }
                    if (v.Length > 0)
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable, childActionIndex: 1);
    }

    [Fact]
    public async Task ConvertConcatenationToInterpolatedString_CSharpStatement()
    {
        var input = """
            @{
                var x = "he[||]l" + "lo" + Environment.NewLine + "world";
            }
            """;

        var expected = """
            @{
                var x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task ConvertConcatenationToInterpolatedString_ExplicitExpression()
    {
        var input = """
            @("he[||]l" + "lo" + Environment.NewLine + "world")
            """;

        var expected = """
            @($"hello{Environment.NewLine}world")
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task ConvertConcatenationToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = "he[||]l" + "lo" + Environment.NewLine + "world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = $@"h[||]ello world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [Fact]
    public async Task ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock2()
    {
        var input = """
            @code
            {
                private string _x = $"h[||]ello\\nworld";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $@"hello\nworld";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [Fact]
    public async Task ConvertBetweenRegularAndVerbatimString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = @"h[||]ello world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = "hello world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [Fact]
    public async Task ConvertBetweenRegularAndVerbatimString_CodeBlock2()
    {
        var input = """
            @code
            {
                private string _x = "h[||]ello\\nworld";
            }
            """;

        var expected = """
            @code
            {
                private string _x = @"hello\nworld";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [Fact]
    public async Task ConvertPlaceholderToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = [|string.Format("hello{0}world", Environment.NewLine)|];
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertPlaceholderToInterpolatedString);
    }

    [Fact]
    public async Task ConvertToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = [||]"hello {";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello {{";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertToInterpolatedString);
    }

    [Fact]
    public async Task AddDebuggerDisplay()
    {
        var input = """
            @code {
                class Goo[||]
                {
                    
                }
            }
            """;

        var expected = """
            @using System.Diagnostics
            @code {
                [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
                class Goo
                {
                    private string GetDebuggerDisplay()
                    {
                        return ToString();
                    }
                }
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.AddDebuggerDisplay);
    }

    [Fact]
    public async Task AddUsing()
    {
        var input = """
            @code
            {
                private [||]StringBuilder _x = new StringBuilder();
            }
            """;

        var expected = """
            @using System.Text
            @code
            {
                private StringBuilder _x = new StringBuilder();
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeFixProviderNames.AddImport);
    }

    [Fact]
    public async Task AddUsing_WithExisting()
    {
        var input = """
            @using System
            @using System.Collections.Generic

            @code
            {
                private [||]StringBuilder _x = new StringBuilder();
            }
            """;

        var expected = """
            @using System
            @using System.Collections.Generic
            @using System.Text

            @code
            {
                private StringBuilder _x = new StringBuilder();
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeFixProviderNames.AddImport);
    }

    [Theory]
    [InlineData("[||]DoesNotExist")]
    [InlineData("Does[||]NotExist")]
    [InlineData("DoesNotExist[||]")]
    public async Task Handle_GenerateMethod_NoCodeBlock_NonEmptyTrailingLine(string cursorAndMethodName)
    {
        var input = $$"""
            <button @onclick="{|CS0103:{{cursorAndMethodName}}|}"></button>
            """;

        var expected = """
            <button @onclick="DoesNotExist"></button>
            @code {
                private void DoesNotExist(global::Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
                {
                    throw new global::System.NotImplementedException();
                }
            }
            """;

        await VerifyCodeActionAsync(input, expected, "Generate Event Handler 'DoesNotExist'");
    }

    private async Task VerifyCodeActionAsync(TestCode input, string? expected, string codeActionName, int childActionIndex = 0, string? fileKind = null)
    {
        UpdateClientLSPInitializationOptions(options =>
        {
            options.ClientCapabilities.TextDocument = new()
            {
                CodeAction = new()
                {
                    ResolveSupport = new()
                }
            };

            return options;
        });

        var document = await CreateProjectAndRazorDocumentAsync(input.Text, fileKind, createSeparateRemoteAndLocalWorkspaces: true);
        var inputText = await document.GetTextAsync(DisposalToken);

        var requestInvoker = new TestLSPRequestInvoker();

        var endpoint = new CohostCodeActionsEndpoint(RemoteServiceInvoker, ClientCapabilitiesService, TestHtmlDocumentSynchronizer.Instance, requestInvoker, NoOpTelemetryReporter.Instance);

        using var diagnostics = new PooledArrayBuilder<Diagnostic>();
        foreach (var (code, spans) in input.NamedSpans)
        {
            if (code.Length == 0)
            {
                continue;
            }

            foreach (var textSpan in spans)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = code,
                    Range = inputText.GetRange(textSpan)
                });
            }
        }

        var request = new VSCodeActionParams
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = document.CreateUri() },
            Range = inputText.GetRange(input.NamedSpans[""].Single()),
            Context = new VSInternalCodeActionContext() { Diagnostics = diagnostics.ToArray() }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(document, request, DisposalToken);

        if (expected is null)
        {
            Assert.Null(result);
            return;
        }

        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var codeActionToRun = (VSInternalCodeAction?)result.SingleOrDefault(e => ((RazorVSInternalCodeAction)e.Value!).Name == codeActionName || ((RazorVSInternalCodeAction)e.Value!).Title == codeActionName).Value;
        Assert.NotNull(codeActionToRun);

        if (codeActionToRun.Children?.Length > 0)
        {
            codeActionToRun = codeActionToRun.Children[childActionIndex];
        }

        Assert.NotNull(codeActionToRun);
    }
}
