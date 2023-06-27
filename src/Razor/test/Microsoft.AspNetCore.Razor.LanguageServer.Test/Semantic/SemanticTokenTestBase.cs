// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

public abstract class SemanticTokenTestBase : TagHelperServiceTestBase
{
    private static readonly AsyncLocal<string?> s_fileName = new();
    private static readonly string s_projectPath = TestProject.GetProjectDirectory(typeof(TagHelperServiceTestBase));

    protected static readonly ServerCapabilities SemanticTokensServerCapabilities = new()
    {
        SemanticTokensOptions = new()
        {
            Full = false,
            Range = true
        }
    };
    // Used by the test framework to set the 'base' name for test files.
    public static string? FileName
    {
        get { return s_fileName.Value; }
        set { s_fileName.Value = value; }
    }

#if GENERATE_BASELINES
    protected bool GenerateBaselines { get; set; } = true;
#else
    protected bool GenerateBaselines { get; set; } = false;
#endif

    protected int BaselineTestCount { get; set; }
    protected int BaselineEditTestCount { get; set; }

    protected SemanticTokenTestBase(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    protected void AssertSemanticTokensMatchesBaseline(IEnumerable<int>? actualSemanticTokens)
    {
        if (FileName is null)
        {
            var message = $"{nameof(AssertSemanticTokensMatchesBaseline)} should only be called from a Semantic test ({nameof(FileName)} is null).";
            throw new InvalidOperationException(message);
        }

        var fileName = BaselineTestCount > 0 ? FileName + $"_{BaselineTestCount}" : FileName;
        var baselineFileName = Path.ChangeExtension(fileName, ".semantic.txt");
        var actual = actualSemanticTokens?.ToArray();

        var actualFileContents = GetFileRepresentationOfTokens(actual);

        BaselineTestCount++;
        if (GenerateBaselines)
        {
            GenerateSemanticBaseline(actualFileContents, baselineFileName);
        }

        var expectedFileContents = GetBaselineFileContents(baselineFileName);

        new XUnitVerifier().EqualOrDiff(expectedFileContents, actualFileContents);
    }

    protected string GetBaselineFileContents(string baselineFileName)
    {
        var semanticFile = TestFile.Create(baselineFileName, GetType().GetTypeInfo().Assembly);
        if (!semanticFile.Exists())
        {
            return string.Empty;
        }

        return semanticFile.ReadAllText();
    }

    private protected async Task<ProvideSemanticTokensResponse> GetCSharpSemanticTokensResponseAsync(
        string documentText, Range razorRange, bool isRazorFile, int hostDocumentSyncVersion = 0)
    {
        var codeDocument = CreateCodeDocument(documentText, isRazorFile, DefaultTagHelpers);
        var csharpRange = GetMappedCSharpRange(codeDocument, razorRange);
        var csharpTokens = Array.Empty<int>();

        if (csharpRange is not null)
        {
            var csharpDocumentUri = new Uri("C:\\TestSolution\\TestProject\\TestDocument.cs");
            var csharpSourceText = codeDocument.GetCSharpSourceText();

            await using var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
                csharpSourceText, csharpDocumentUri, SemanticTokensServerCapabilities, SpanMappingService, DisposalToken);
            var result = await csharpServer.ExecuteRequestAsync<SemanticTokensRangeParams, SemanticTokens>(
                Methods.TextDocumentSemanticTokensRangeName,
                CreateVSSemanticTokensRangeParams(csharpRange, csharpDocumentUri),
                DisposalToken);

            csharpTokens = result?.Data;
        }

        var csharpResponse = new ProvideSemanticTokensResponse(tokens: csharpTokens, hostDocumentSyncVersion);
        return csharpResponse;
    }

    protected Range? GetMappedCSharpRange(RazorCodeDocument codeDocument, Range razorRange)
    {
        var documentMappingService = new RazorDocumentMappingService(
            TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);
        if (!documentMappingService.TryMapToGeneratedDocumentRange(codeDocument.GetCSharpDocument(), razorRange, out var csharpRange) &&
            !RazorSemanticTokensInfoService.TryGetMinimalCSharpRange(codeDocument, razorRange, out csharpRange))
        {
            // No C# in the range.
            return null;
        }

        return csharpRange;
    }

    internal static SemanticTokensRangeParams CreateVSSemanticTokensRangeParams(Range range, Uri uri)
        => new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = range
        };

    private static void GenerateSemanticBaseline(string actualFileContents, string baselineFileName)
    {
        var semanticBaselinePath = Path.Combine(s_projectPath, baselineFileName);
        File.WriteAllText(semanticBaselinePath, actualFileContents);
    }

    private static string GetFileRepresentationOfTokens(int[]? data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        using var _ = StringBuilderPool.GetPooledObject(out var builder);
        builder.AppendLine("//line,characterPos,length,tokenType,modifier");
        var legendArray = TestRazorSemanticTokensLegend.Instance.Legend.TokenTypes;
        var prevLength = 0;
        for (var i = 0; i < data.Length; i += 5)
        {
            Assert.False(i != 0 && data[i] == 0 && data[i + 1] == 0, "line delta and character delta are both 0, which is invalid as we shouldn't be producing overlapping tokens");
            Assert.False(i != 0 && data[i] == 0 && data[i + 1] < prevLength, "Previous length is longer than char offset from previous start, meaning tokens will overlap");

            var typeString = legendArray[data[i + 3]];
            builder.Append(data[i]).Append(' ');
            builder.Append(data[i + 1]).Append(' ');
            builder.Append(data[i + 2]).Append(' ');
            builder.Append(typeString).Append(' ');
            builder.Append(data[i + 4]);
            builder.AppendLine();

            prevLength = data[i + 2];
        }

        return builder.ToString();
    }

    private static int[]? ParseSemanticBaseline(string semanticIntStr)
    {
        if (string.IsNullOrEmpty(semanticIntStr))
        {
            return null;
        }

        var tokenTypesList = TestRazorSemanticTokensLegend.Instance.Legend.TokenTypes.ToList();
        var strArr = semanticIntStr.Split(new string[] { " ", Environment.NewLine }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var results = new List<int>();
        for (var i = 0; i < strArr.Length; i++)
        {
            if (int.TryParse(strArr[i], System.Globalization.NumberStyles.Integer, Thread.CurrentThread.CurrentCulture, out var intResult))
            {
                results.Add(intResult);
                continue;
            }

            // Needed to handle token types with spaces in their names, e.g. C#'s 'local name' type
            var tokenTypeStr = strArr[i];
            while (i + 1 < strArr.Length && !int.TryParse(strArr[i + 1], System.Globalization.NumberStyles.Integer, Thread.CurrentThread.CurrentCulture, out _))
            {
                tokenTypeStr += $" {strArr[i + 1]}";
                i++;
            }

            var tokenIndex = tokenTypesList.IndexOf(tokenTypeStr);
            if (tokenIndex != -1)
            {
                results.Add(tokenIndex);
                continue;
            }
        }

        return results.ToArray();
    }
}
