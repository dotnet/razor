﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class RazorDocumentMappingServiceTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    private readonly IFilePathService _filePathService = new LSPFilePathService(TestLanguageServerFeatureOptions.Instance);

    [Fact]
    public void TryMapToHostDocumentRange_Strict_StartOnlyMaps_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 10), new LinePosition(0, 19));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Strict,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Strict_EndOnlyMaps_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 12));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Strict,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Strict_StartAndEndMap_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 6), new LinePosition(0, 18));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Strict,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_DirectlyMaps_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 6), new LinePosition(0, 18));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_StartSinglyIntersects_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 10), new LinePosition(0, 19));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_EndSinglyIntersects_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 10));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_StartDoublyIntersects_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [
                new SourceMapping(new SourceSpan(4, 8), new SourceSpan(6, 8)), // DateTime
                new SourceMapping(new SourceSpan(12, 4), new SourceSpan(14, 4)) // .Now
            ]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 14), new LinePosition(0, 19));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_EndDoublyIntersects_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [
                new SourceMapping(new SourceSpan(4, 8), new SourceSpan(6, 8)), // DateTime
                new SourceMapping(new SourceSpan(12, 4), new SourceSpan(14, 4)) // .Now
            ]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 14));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_OverlapsSingleMapping_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 19));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inclusive_OverlapsTwoMappings_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [
                new SourceMapping(new SourceSpan(4, 8), new SourceSpan(6, 8)), // DateTime
                new SourceMapping(new SourceSpan(12, 4), new SourceSpan(14, 4)) // .Now
            ]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 19));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inclusive,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_DirectlyMaps_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(6, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 6), new LinePosition(0, 18));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 4), new LinePosition(0, 16));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_BeginningOfDocAndProjection_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "@<unclosed></unclosed><p>@DateTime.Now</p>",
            projectedCSharpSource: "(__builder) => { };__o = DateTime.Now;",
            sourceMappings: [new SourceMapping(new SourceSpan(26, 12), new SourceSpan(25, 12))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 19));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out var originalRange);

        // Assert
        Assert.False(result);
        Assert.Equal(default, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_InBetweenProjections_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "@{ var abc = @<unclosed></unclosed> }",
            projectedCSharpSource: " var abc =  (__builder) => { } ",
            sourceMappings: [
                new SourceMapping(new SourceSpan(2, 11), new SourceSpan(0, 11)),
                new SourceMapping(new SourceSpan(35, 1), new SourceSpan(30, 1))
            ]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 12), new LinePosition(0, 29));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 13), new LinePosition(0, 35));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_InBetweenProjectionAndEndOfDoc_ReturnsTrue()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "@{ var abc = @<unclosed></unclosed>",
            projectedCSharpSource: " var abc =  (__builder) => { }",
            sourceMappings: [new SourceMapping(new SourceSpan(2, 11), new SourceSpan(0, 11))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 12), new LinePosition(0, 29));
        var expectedOriginalRange = new LinePositionSpan(new LinePosition(0, 13), new LinePosition(0, 35));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out var originalRange);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOriginalRange, originalRange);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_OutsideDoc_ReturnsFalse()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "@{ var abc = @<unclosed></unclosed>",
            projectedCSharpSource: " var abc =  (__builder) => { }",
            sourceMappings: [new SourceMapping(new SourceSpan(2, 11), new SourceSpan(0, 11))]);
        var projectedRange = new LinePositionSpan(new LinePosition(2, 12), new LinePosition(2, 29));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMapToHostDocumentRange_Inferred_OutOfOrderMappings_DoesNotThrow()
    {
        // Real world repo is something like:
        //
        // <Component1>
        //    @if (true)
        //    {
        //        <Component2 att="val"
        //                    onclick="() => thing()""    <-- note double quotes
        //                    att2="val" />
        //    }
        // </Component1>
        //
        // Ends up with an unterminated string in the generated code, and a "missing }" diagnostic
        // that has some very strange mappings!

        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "@{ var abc = @<unclosed></unclosed>",
            projectedCSharpSource: " var abc =  (__builder) => { }",
            sourceMappings: [new(new(30, 1), new(2, 1)), new(new(28, 2), new(30, 2))]);
        var projectedRange = new LinePositionSpan(new LinePosition(0, 25), new LinePosition(0, 25));

        // Act
        var result = service.TryMapToHostDocumentRange(
            codeDoc.GetCSharpDocument(),
            projectedRange,
            MappingBehavior.Inferred,
            out var originalRange);

        // Assert
        // We're really just happy this doesn't throw an exception. The behavior is to map to the end of the file
        Assert.True(result);
        Assert.Equal(0, originalRange.Start.Line);
        Assert.Equal(31, originalRange.Start.Character);
        Assert.Equal(0, originalRange.End.Line);
        Assert.Equal(35, originalRange.End.Character);
    }

    [Fact]
    public void TryMapToGeneratedDocumentPosition_NotMatchingAnyMapping()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "test razor source",
            projectedCSharpSource: "test C# source",
            sourceMappings: [new SourceMapping(new SourceSpan(2, 100), new SourceSpan(0, 100))]);

        // Act
        var result = service.TryMapToGeneratedDocumentPosition(
            codeDoc.GetCSharpDocument(),
            hostDocumentIndex: 1,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMapToGeneratedDocumentPosition_CSharp_OnLeadingEdge()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act
        var result = service.TryMapToGeneratedDocumentPosition(
            codeDoc.GetCSharpDocument(),
            hostDocumentIndex: 16,
            out var projectedPosition,
            out var projectedPositionIndex);

        Assert.True(result);
        Assert.Equal(2, projectedPosition.Line);
        Assert.Equal(0, projectedPosition.Character);
        Assert.Equal(11, projectedPositionIndex);
    }

    [Fact]
    public void TryMapToGeneratedDocumentPosition_CSharp_InMiddle()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act & Assert
        var result = service.TryMapToGeneratedDocumentPosition(
            codeDoc.GetCSharpDocument(),
            hostDocumentIndex: 28,
            out var projectedPosition,
            out var projectedPositionIndex);

        Assert.True(result);
        Assert.Equal(3, projectedPosition.Line);
        Assert.Equal(2, projectedPosition.Character);
        Assert.Equal(23, projectedPositionIndex);
    }

    [Fact]
    public void TryMapToGeneratedDocumentPosition_CSharp_OnTrailingEdge()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act & Assert
        var result = service.TryMapToGeneratedDocumentPosition(
            codeDoc.GetCSharpDocument(),
            hostDocumentIndex: 35,
            out var projectedPosition,
            out var projectedPositionIndex);

        Assert.True(result);
        Assert.Equal(3, projectedPosition.Line);
        Assert.Equal(9, projectedPosition.Character);
        Assert.Equal(30, projectedPositionIndex);
    }

    [Fact]
    public void TryMapToHostDocumentPosition_NotMatchingAnyMapping()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "test razor source",
            projectedCSharpSource: "projectedCSharpSource: test C# source",
            sourceMappings: [new SourceMapping(new SourceSpan(2, 100), new SourceSpan(2, 100))]);

        // Act
        var result = service.TryMapToHostDocumentPosition(
            codeDoc.GetCSharpDocument(),
            generatedDocumentIndex: 1,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMapToHostDocumentPosition_CSharp_OnLeadingEdge()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act & Assert
        var result = service.TryMapToHostDocumentPosition(
            codeDoc.GetCSharpDocument(),
            generatedDocumentIndex: 11, // @{|
            out var hostDocumentPosition,
            out var hostDocumentIndex);

        Assert.True(result);
        Assert.Equal(1, hostDocumentPosition.Line);
        Assert.Equal(9, hostDocumentPosition.Character);
        Assert.Equal(16, hostDocumentIndex);
    }

    [Fact]
    public void TryMapToHostDocumentPosition_CSharp_InMiddle()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act & Assert
        var result = service.TryMapToHostDocumentPosition(
            codeDoc.GetCSharpDocument(),
            generatedDocumentIndex: 21, // |var def
            out var hostDocumentPosition,
            out var hostDocumentIndex);

        Assert.True(result);
        Assert.Equal(2, hostDocumentPosition.Line);
        Assert.Equal(0, hostDocumentPosition.Character);
        Assert.Equal(26, hostDocumentIndex);
    }

    [Fact]
    public void TryMapToHostDocumentPosition_CSharp_OnTrailingEdge()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);

        // Act & Assert
        var result = service.TryMapToHostDocumentPosition(
            codeDoc.GetCSharpDocument(),
            generatedDocumentIndex: 30, // def; |}
            out var hostDocumentPosition,
            out var hostDocumentIndex);

        Assert.True(result);
        Assert.Equal(2, hostDocumentPosition.Line);
        Assert.Equal(9, hostDocumentPosition.Character);
        Assert.Equal(35, hostDocumentIndex);
    }

    [Fact]
    public void TryMapToGeneratedDocumentRange_CSharp()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 19), new SourceSpan(11, 19))
            ]);
        var range = new LinePositionSpan(new LinePosition(1, 10), new LinePosition(1, 13));

        // Act & Assert
        var result = service.TryMapToGeneratedDocumentRange(
            codeDoc.GetCSharpDocument(),
            range, // |var| abc
            out var projectedRange);

        Assert.True(result);
        Assert.Equal(2, projectedRange.Start.Line);
        Assert.Equal(1, projectedRange.Start.Character);
        Assert.Equal(2, projectedRange.End.Line);
        Assert.Equal(4, projectedRange.End.Character);
    }

    [Fact]
    public void TryMapToGeneratedDocumentRange_CSharp_MissingSourceMappings()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1))]);
        var range = new LinePositionSpan(new LinePosition(1, 10), new LinePosition(1, 13));

        // Act
        var result = service.TryMapToGeneratedDocumentRange(
            codeDoc.GetCSharpDocument(),
            range, // |var| abc
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMapToGeneratedDocumentRange_CSharp_End_LessThan_Start()
    {
        // Arrange
        var service = new LspDocumentMappingService(_filePathService, new TestDocumentContextFactory(), LoggerFactory);
        var codeDoc = CreateCodeDocumentWithCSharpProjection(
            razorSource: "Line 1\nLine 2 @{ var abc;\nvar def; }",
            projectedCSharpSource: "\n// Prefix\n var abc;\nvar def; \n// Suffix",
            sourceMappings: [
                new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 1)),
                new SourceMapping(new SourceSpan(16, 3), new SourceSpan(11, 3)),
                new SourceMapping(new SourceSpan(19, 10), new SourceSpan(5, 10))
            ]);
        var range = new LinePositionSpan(new LinePosition(1, 10), new LinePosition(1, 13));

        // Act
        var result = service.TryMapToGeneratedDocumentRange(
            codeDoc.GetCSharpDocument(),
            range, // |var| abc
            out _);

        // Assert
        Assert.False(result);
    }

    private static RazorCodeDocument CreateCodeDocumentWithCSharpProjection(string razorSource, string projectedCSharpSource, ImmutableArray<SourceMapping> sourceMappings)
    {
        var sourceDocument = TestRazorSourceDocument.Create(razorSource);
        var projectEngine = RazorProjectEngine.Create(builder => { });
        var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, "mvc", importSources: default, tagHelpers: []);

        var csharpDocument = new RazorCSharpDocument(
            codeDocument,
            projectedCSharpSource,
            RazorCodeGenerationOptions.Default,
            diagnostics: [],
            sourceMappings,
            linePragmas: []);
        codeDocument.SetCSharpDocument(csharpDocument);
        return codeDocument;
    }
}
