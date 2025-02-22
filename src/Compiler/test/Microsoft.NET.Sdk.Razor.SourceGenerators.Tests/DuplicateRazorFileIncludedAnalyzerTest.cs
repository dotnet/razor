// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators.Tests;

public class DuplicateRazorFileIncludedAnalyzerTest
{
    [Theory]
    [InlineData("Duplicate.cshtml")]
    [InlineData("Duplicate.razor")]
    public async Task Analyzer_ReportsDiagnostic_WhenDuplicateRazorFileIsIncluded(string fileName)
    {
        // Arrange
        var test = new CSharpAnalyzerTest<DuplicateRazorFileIncludedAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // Need a non-empty source file to make the test helper happy
                    ("Test.cs", "public class Test {}"),
                },
                AdditionalFiles =
                {
                    (fileName, "<h1>Duplicate</h1>"),
                    (fileName, "<h1>Duplicate</h1>"),
                },
            },
            ExpectedDiagnostics =
            {
                new DiagnosticResult(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.Id, DiagnosticSeverity.Error)
                    .WithLocation(fileName, 1, 1)
                    .WithMessageFormat(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.MessageFormat.ToString())
                    .WithArguments(fileName),
            },
        };

        // Act & Assert
        await test.RunAsync();
    }

    [Theory]
    [InlineData("Duplicate.cshtml", "duplicate.cshtml")]
    [InlineData("Duplicate.razor", "duplicate.razor")]
    public async Task Analyzer_NoDiagnostic_WhenDuplicateRazorFileIsIncluded_DifferentCase(string fileName1, string fileName2)
    {
        // Arrange
        var test = new CSharpAnalyzerTest<DuplicateRazorFileIncludedAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // Need a non-empty source file to make the test helper happy
                    ("Test.cs", "public class Test {}"),
                },
                AdditionalFiles =
                {
                    (fileName1, "<h1>Duplicate</h1>"),
                    (fileName2, "<h1>Duplicate</h1>"),
                },
            },
            ExpectedDiagnostics =
            {
            },
        };

        // Act & Assert
        await test.RunAsync();
    }

    [Theory]
    [InlineData("Duplicate.cshtml")]
    [InlineData("Duplicate.razor")]
    public async Task Analyzer_ReportsDiagnostic_WhenThreeDuplicateRazorFilesAreIncluded(string fileName)
    {
        // Arrange
        var test = new CSharpAnalyzerTest<DuplicateRazorFileIncludedAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // Need a non-empty source file to make the test helper happy
                    ("Test.cs", "public class Test {}"),
                },
                AdditionalFiles =
                {
                    (fileName, "<h1>Duplicate</h1>"),
                    (fileName, "<h1>Duplicate</h1>"),
                    (fileName, "<h1>Duplicate</h1>"),
                },
            },
            ExpectedDiagnostics =
            {
                new DiagnosticResult(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.Id, DiagnosticSeverity.Error)
                    .WithLocation(fileName, 1, 1)
                    .WithMessageFormat(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.MessageFormat.ToString())
                    .WithArguments(fileName),
                new DiagnosticResult(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.Id, DiagnosticSeverity.Error)
                    .WithLocation(fileName, 1, 1)
                    .WithMessageFormat(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor.MessageFormat.ToString())
                    .WithArguments(fileName),
            },
        };

        // Act & Assert
        await test.RunAsync();
    }
}

