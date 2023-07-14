﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

public class CodeActionEndToEndTest : SingleServerDelegatingEndpointTestBase
{
    public CodeActionEndToEndTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    #region CSharp CodeAction Tests

    [Fact]
    public async Task Handle_GenerateConstructor()
    {
        var input = """

            <div></div>

            @functions
            {
                public class [||]Goo
                {
                }
            }

            """;

        var expected = """
            
            <div></div>
            
            @functions
            {
                public class Goo
                {
                    public Goo()
                    {
                    }
                }
            }

            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.GenerateDefaultConstructors);
    }

    [Fact]
    public async Task Handle_IntroduceLocal()
    {
        var input = """
            @using System.Linq

            <div></div>

            @functions
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
            
            @functions
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

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable);
    }

    [Fact]
    public async Task Handle_IntroduceLocal_All()
    {
        var input = """
            @using System.Linq

            <div></div>

            @functions
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
            
            @functions
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

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable, childActionIndex: 1);
    }

    [Fact]
    public async Task Handle_ConvertConcatenationToInterpolatedString_CSharpStatement()
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

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertConcatenationToInterpolatedString_ExplicitExpression()
    {
        var input = """
            @("he[||]l" + "lo" + Environment.NewLine + "world")
            """;

        var expected = """
            @($"hello{Environment.NewLine}world")
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertConcatenationToInterpolatedString_CodeBlock()
    {
        var input = """
            @functions
            {
                private string _x = "he[||]l" + "lo" + Environment.NewLine + "world";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock()
    {
        var input = """
            @functions
            {
                private string _x = $@"h[||]ello world";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = $"hello world";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock2()
    {
        var input = """
            @functions
            {
                private string _x = $"h[||]ello\\nworld";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = $@"hello\nworld";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertBetweenRegularAndVerbatimString_CodeBlock()
    {
        var input = """
            @functions
            {
                private string _x = @"h[||]ello world";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = "hello world";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [Fact]
    public async Task Handle_ConvertBetweenRegularAndVerbatimString_CodeBlock2()
    {
        var input = """
            @functions
            {
                private string _x = "h[||]ello\\nworld";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = @"hello\nworld";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [Fact]
    public async Task Handle_ConvertPlaceholderToInterpolatedString_CodeBlock()
    {
        var input = """
            @functions
            {
                private string _x = [|string.Format("hello{0}world", Environment.NewLine)|];
            }
            """;

        var expected = """
            @functions
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertPlaceholderToInterpolatedString);
    }

    [Fact]
    public async Task Handle_ConvertToInterpolatedString_CodeBlock()
    {
        var input = """
            @functions
            {
                private string _x = [||]"hello {";
            }
            """;

        var expected = """
            @functions
            {
                private string _x = $"hello {{";
            }
            """;

        await ValidateCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertToInterpolatedString);
    }
    #endregion

    #region RazorCodeAction Tests

    [Theory]
    [InlineData("[||]DoesNotExist")]
    [InlineData("Does[||]NotExist")]
    [InlineData("DoesNotExist[||]")]
    public async Task Handle_GenerateMethod_NoCodeBlock(string cursorAndMethodName)
    {
        var input = $$"""
            <button @onclick="{{cursorAndMethodName}}"></button>
            """;

        var expected = """
            <button @onclick="DoesNotExist"></button>
            @code {
                private void DoesNotExist()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        var diagnostics = new[] { new Diagnostic() { Code = "CS0103", Message = "The name 'DoesNotExist' does not exist in the current context" } };
        await ValidateCodeActionAsync(input,
            expected, "Generate Event Handler 'DoesNotExist'",
            razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() },
            createRazorCodeActionResolversFn: () => new[] { new GenerateMethodCodeActionResolver(DocumentContextFactory, TestRazorLSPOptionsMonitor.Create()) },
            diagnostics: diagnostics);
    }

    [Theory]
    [InlineData("@code {}")]
    [InlineData("@code {\r\n}")]
    [InlineData("@code {\r\n\r\n}")]
    public async Task Handle_GenerateMethod_Empty_CodeBlock(string codeBlock)
    {
        var input = $$"""
            <button @onclick="[||]DoesNotExist"></button>
            {{codeBlock}}
            """;

        var expected = """
            <button @onclick="DoesNotExist"></button>
            @code {
                private void DoesNotExist()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        var diagnostics = new[] { new Diagnostic() { Code = "CS0103", Message = "The name 'DoesNotExist' does not exist in the current context" } };
        await ValidateCodeActionAsync(input,
            expected,
            "Generate Event Handler 'DoesNotExist'",
            razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() },
            createRazorCodeActionResolversFn: () => new[] { new GenerateMethodCodeActionResolver(DocumentContextFactory, TestRazorLSPOptionsMonitor.Create()) },
            diagnostics: diagnostics);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    public async Task Handle_GenerateMethod_Nonempty_CodeBlock(string spacing)
    {
        var input = $$"""
            <button @onclick="[||]DoesNotExist"></button>
            @code {
                public void Exists()
                {
                }{{spacing}}
            }
            """;

        var expected = """
            <button @onclick="DoesNotExist"></button>
            @code {
                public void Exists()
                {
                }

                private void DoesNotExist()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        var diagnostics = new[] { new Diagnostic() { Code = "CS0103", Message = "The name 'DoesNotExist' does not exist in the current context" } };
        await ValidateCodeActionAsync(input,
            expected,
            "Generate Event Handler 'DoesNotExist'",
            razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() },
            createRazorCodeActionResolversFn: () => new[] { new GenerateMethodCodeActionResolver(DocumentContextFactory, TestRazorLSPOptionsMonitor.Create()) },
            diagnostics: diagnostics);
    }

    [Theory]
    [InlineData("[||]Exists")]
    [InlineData("E[||]xists")]
    [InlineData("Exists[||]")]
    public async Task Handle_GenerateMethod_Method_ExistsInCodeBlock(string cursorAndMethodName)
    {
        var input = $$"""
            <button @onclick="{{cursorAndMethodName}}"></button>
            @code {
                public void Exists()
                {
                }
            }
            """;

        TestFileMarkupParser.GetSpan(input, out input, out var textSpan);
        var razorFilePath = "file://C:/path/test.razor";
        var codeDocument = CreateCodeDocument(input, filePath: razorFilePath);
        var razorSourceText = codeDocument.GetSourceText();
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, Logger, null!);

        var result = await GetCodeActionsAsync(uri, textSpan, razorSourceText, requestContext, razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() });
        Assert.False(result.Where(e => ((RazorVSInternalCodeAction)e.Value!).Title == "Generate 'DoesNotExist' Method").Any());
    }

    [Theory]
    [InlineData(true, 4, "", 0, "    ")]
    [InlineData(true, 4, "    ", 4, "    ")]
    [InlineData(true, 4, "\t", 4, "    ")]
    [InlineData(true, 2, "", 0, "  ")]
    [InlineData(true, 2, "  ", 2, "  ")]
    [InlineData(false, 4, "", 0, "\t")]
    [InlineData(false, 4, "    ", 4, "\t")]
    [InlineData(false, 4, "\t", 4, "\t")]
    [InlineData(false, 2, "", 0, "\t")]
    [InlineData(false, 2, "  ", 2, "\t")]
    public async Task Handle_GenerateMethod_VaryIndentSize(bool insertSpaces, int tabSize, string inputIndentString, int initialIndentSize, string indent)
    {
        var input = $$"""
            <button @onclick="[||]DoesNotExist"></button>
            {{inputIndentString}}@code {
            {{inputIndentString}}}
            """;

        var initialIndentString = FormattingUtilities.GetIndentationString(initialIndentSize, insertSpaces, tabSize);
        var expected = $$"""
            <button @onclick="DoesNotExist"></button>
            {{inputIndentString}}@code {
            {{initialIndentString}}{{indent}}private void DoesNotExist()
            {{initialIndentString}}{{indent}}{
            {{initialIndentString}}{{indent}}{{indent}}throw new System.NotImplementedException();
            {{initialIndentString}}{{indent}}}
            {{inputIndentString}}}
            """;

        var diagnostics = new[] { new Diagnostic() { Code = "CS0103", Message = "The name 'DoesNotExist' does not exist in the current context" } };
        var razorLSPOptions = new RazorLSPOptions(Trace: default, EnableFormatting: true, AutoClosingTags: true, insertSpaces, tabSize, FormatOnType: true, AutoInsertAttributeQuotes: true);
        var optionsMonitor = TestRazorLSPOptionsMonitor.Create();
        await optionsMonitor.UpdateAsync(razorLSPOptions, CancellationToken.None);
        await ValidateCodeActionAsync(input,
            expected,
            "Generate Event Handler 'DoesNotExist'",
            razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() },
            createRazorCodeActionResolversFn: () => new[] { new GenerateMethodCodeActionResolver(DocumentContextFactory, optionsMonitor) },
            diagnostics: diagnostics);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    [InlineData("\r\npublic void Exists(){}\r\n")]
    public async Task Handle_GenerateMethod_CodeBehindFile_Exists(string spacingOrMethod)
    {
        var @namespace = $"Test.{Path.GetTempPath().Replace(":", "_").Replace("\\", ".")[..^1]}";
        var input = """
            <button @onclick="[||]DoesNotExist"></button>
            """;

        var expectedRazorContent = """
            <button @onclick="DoesNotExist"></button>
            """;

        var initialCodeBehindContent = $$"""
            namespace {{@namespace}}
            {
                public partial class test
                {{{spacingOrMethod}}
                }
            }
            """;

        var expectedCodeBehindContent = $$"""
            namespace {{@namespace}}
            {
                public partial class test
                {{{spacingOrMethod}}
                    private void DoesNotExist()
                    {
                        throw new System.NotImplementedException();
                    }
                }
            }
            """;
        await ValidateCodeBehindFileAsync(input, initialCodeBehindContent, expectedRazorContent, expectedCodeBehindContent);
    }

    [Theory]
    [InlineData("namespace WrongNamespace\r\n{\r\npublic partial class test\r\n{\r\n}\r\n}")]
    [InlineData("namespace __GeneratedComponent\r\n{\r\npublic partial class WrongClassName\r\n{\r\n}\r\n}")]
    public async Task Handle_GenerateMethod_CodeBehindFile_Malformed(string initialCodeBehindContent)
    {
        var input = """
            <button @onclick="[||]DoesNotExist"></button>
            """;

        var expectedRazorContent = """
            <button @onclick="DoesNotExist"></button>
            @code {
                private void DoesNotExist()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        await ValidateCodeBehindFileAsync(input, initialCodeBehindContent, expectedRazorContent, initialCodeBehindContent);
    }

    [Fact]
    public async Task Handle_GenerateMethod_CodeBehindFile_FileScopedNamespace()
    {
        var @namespace = $"Test.{Path.GetTempPath().Replace(":", "_").Replace("\\", ".")[..^1]}";
        var input = """
            <button @onclick="[||]DoesNotExist"></button>
            """;

        var expectedRazorContent = """
            <button @onclick="DoesNotExist"></button>
            """;

        var initialCodeBehindContent = $$"""
            namespace {{@namespace}};
            public partial class test
            {
            }
            """;

        var expectedCodeBehindContent = $$"""
            namespace {{@namespace}};
            public partial class test
            {
                private void DoesNotExist()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;
        await ValidateCodeBehindFileAsync(input, initialCodeBehindContent, expectedRazorContent, expectedCodeBehindContent);
    }

    #endregion

    private async Task ValidateCodeBehindFileAsync(
        string input,
        string initialCodeBehindContent,
        string expectedRazorContent,
        string expectedCodeBehindContent)
    {
        var razorFilePath = FilePathNormalizer.Normalize($"{Path.GetTempPath()}test.razor");
        var codeBehindFilePath = FilePathNormalizer.Normalize($"{Path.GetTempPath()}test.razor.cs");
        var diagnostics = new[] { new Diagnostic() { Code = "CS0103", Message = "The name 'DoesNotExist' does not exist in the current context" } };

        TestFileMarkupParser.GetSpan(input, out input, out var textSpan);
        var codeDocument = CreateCodeDocument(input, filePath: razorFilePath);
        codeDocument.SetCodeGenerationOptions(RazorCodeGenerationOptions.Create(o =>
        {
            o.RootNamespace = "Test";
        }));
        var razorSourceText = codeDocument.GetSourceText();
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, Logger, null!);

        File.Create(codeBehindFilePath).Close();
        try
        {
            File.WriteAllText(codeBehindFilePath, initialCodeBehindContent);

            var result = await GetCodeActionsAsync(uri, textSpan, razorSourceText, requestContext, razorCodeActionProviders: new[] { new GenerateMethodCodeActionProvider() }, diagnostics);
            var changes = await GetEditsAsync(result, requestContext, "Generate Event Handler 'DoesNotExist'", createRazorCodeActionResolversFn: () => new[] { new GenerateMethodCodeActionResolver(DocumentContextFactory, TestRazorLSPOptionsMonitor.Create()) });

            var razorEdits = new List<TextChange>();
            var codeBehindEdits = new List<TextChange>();
            var codeBehindSourceText = SourceText.From(initialCodeBehindContent);
            foreach (var change in changes)
            {
                if (FilePathNormalizer.Normalize(change.TextDocument.Uri.GetAbsoluteOrUNCPath()) == codeBehindFilePath)
                {
                    codeBehindEdits.AddRange(change.Edits.Select(e => e.AsTextChange(codeBehindSourceText)));
                }
                else
                {
                    razorEdits.AddRange(change.Edits.Select(e => e.AsTextChange(razorSourceText)));
                }
            }

            var actualRazorContent = razorSourceText.WithChanges(razorEdits).ToString();
            AssertEx.EqualOrDiff(expectedRazorContent, actualRazorContent);

            var actualCodeBehindContent = codeBehindSourceText.WithChanges(codeBehindEdits).ToString();
            AssertEx.EqualOrDiff(expectedCodeBehindContent, actualCodeBehindContent);
        }
        finally
        {
            File.Delete(codeBehindFilePath);
        }
    }

    private async Task ValidateCodeActionAsync(
        string input,
        string expected,
        string codeAction,
        int childActionIndex = 0,
        IRazorCodeActionProvider[]? razorCodeActionProviders = null,
        Func<IRazorCodeActionResolver[]>? createRazorCodeActionResolversFn = null,
        Diagnostic[]? diagnostics = null)
    {
        TestFileMarkupParser.GetSpan(input, out input, out var textSpan);

        var razorFilePath = "file://C:/path/test.razor";
        var codeDocument = CreateCodeDocument(input, filePath: razorFilePath);
        var sourceText = codeDocument.GetSourceText();
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, Logger, null!);

        var result = await GetCodeActionsAsync(uri, textSpan, sourceText, requestContext, razorCodeActionProviders, diagnostics);
        Assert.NotEmpty(result);
        var changes = await GetEditsAsync(result, requestContext, codeAction, childActionIndex, createRazorCodeActionResolversFn);

        var edits = new List<TextChange>();
        foreach (var change in changes)
        {
            edits.AddRange(change.Edits.Select(e => e.AsTextChange(sourceText)));
        }

        var actual = sourceText.WithChanges(edits).ToString();
        AssertEx.EqualOrDiff(expected, actual);
    }

    private async Task<SumType<Command, CodeAction>[]> GetCodeActionsAsync(
        Uri uri,
        TextSpan textSpan,
        SourceText sourceText,
        RazorRequestContext requestContext,
        IRazorCodeActionProvider[]? razorCodeActionProviders = null,
        Diagnostic[]? diagnostics = null)
    {
        var csharpCodeActionProviders = new ICSharpCodeActionProvider[]
        {
            new DefaultCSharpCodeActionProvider(TestLanguageServerFeatureOptions.Instance)
        };
        var htmlCodeActionProviders = Array.Empty<IHtmlCodeActionProvider>();

        var endpoint = new CodeActionEndpoint(DocumentMappingService, razorCodeActionProviders ?? Array.Empty<IRazorCodeActionProvider>(), csharpCodeActionProviders, htmlCodeActionProviders, LanguageServer, LanguageServerFeatureOptions, default);

        // Call GetRegistration, so the endpoint knows we support resolve
        endpoint.ApplyCapabilities(new(), new VSInternalClientCapabilities
        {
            TextDocument = new TextDocumentClientCapabilities
            {
                CodeAction = new CodeActionSetting
                {
                    ResolveSupport = new CodeActionResolveSupportSetting()
                }
            }
        });

        var @params = new VSCodeActionParams
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = uri },
            Range = textSpan.AsRange(sourceText),
            Context = new VSInternalCodeActionContext() { Diagnostics = diagnostics ?? Array.Empty<Diagnostic>() }
        };

        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);
        Assert.NotNull(result);
        return result;
    }

    private async Task<TextDocumentEdit[]> GetEditsAsync(
        SumType<Command, CodeAction>[] result,
        RazorRequestContext requestContext,
        string codeAction,
        int childActionIndex = 0,
        Func<IRazorCodeActionResolver[]>? createRazorCodeActionResolversFn = null)
    {
        var codeActionToRun = (VSInternalCodeAction)result.Single(e => ((RazorVSInternalCodeAction)e.Value!).Name == codeAction || ((RazorVSInternalCodeAction)e.Value!).Title == codeAction);

        if (codeActionToRun.Children?.Length > 0)
        {
            codeActionToRun = codeActionToRun.Children[childActionIndex];
        }

        var formattingService = await TestRazorFormattingService.CreateWithFullSupportAsync();

        var csharpCodeActionResolvers = new CSharpCodeActionResolver[]
        {
            new DefaultCSharpCodeActionResolver(DocumentContextFactory, LanguageServer, formattingService)
        };
        var htmlCodeActionResolvers = Array.Empty<HtmlCodeActionResolver>();

        var resolveEndpoint = new CodeActionResolveEndpoint(createRazorCodeActionResolversFn is null ? Array.Empty<IRazorCodeActionResolver>() : createRazorCodeActionResolversFn(), csharpCodeActionResolvers, htmlCodeActionResolvers, LoggerFactory);

        var resolveResult = await resolveEndpoint.HandleRequestAsync(codeActionToRun, requestContext, DisposalToken);

        Assert.NotNull(resolveResult.Edit);

        var workspaceEdit = resolveResult.Edit;
        Assert.True(workspaceEdit.TryGetDocumentChanges(out var changes));

        return changes;
    }
}
