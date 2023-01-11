﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class RazorDiagnosticTest
{
    [Fact]
    public void Create_WithDescriptor_CreatesDefaultRazorDiagnostic()
    {
        // Arrange
        var descriptor = new RazorDiagnosticDescriptor("RZ0001", () => "a", RazorDiagnosticSeverity.Error);
        var span = new SourceSpan("test.cs", 15, 1, 8, 5);

        // Act
        var diagnostic = RazorDiagnostic.Create(descriptor, span);

        // Assert
        var defaultDiagnostic = Assert.IsType<DefaultRazorDiagnostic>(diagnostic);
        Assert.Equal("RZ0001", defaultDiagnostic.Id);
        Assert.Equal(RazorDiagnosticSeverity.Error, defaultDiagnostic.Severity);
        Assert.Equal(span, diagnostic.Span);
    }

    [Fact]
    public void Create_WithDescriptor_AndArgs_CreatesDefaultRazorDiagnostic()
    {
        // Arrange
        var descriptor = new RazorDiagnosticDescriptor("RZ0001", () => "a", RazorDiagnosticSeverity.Error);
        var span = new SourceSpan("test.cs", 15, 1, 8, 5);

        // Act
        var diagnostic = RazorDiagnostic.Create(descriptor, span, "Hello", "World");

        // Assert
        var defaultDiagnostic = Assert.IsType<DefaultRazorDiagnostic>(diagnostic);
        Assert.Equal("RZ0001", defaultDiagnostic.Id);
        Assert.Equal(RazorDiagnosticSeverity.Error, defaultDiagnostic.Severity);
        Assert.Equal(span, diagnostic.Span);
    }

    [Fact]
    public void GetMessage_WithNullDescriptorFormat_ReturnsDefaultErrorString()
    {
        // Arrange
        var descriptor = new RazorDiagnosticDescriptor("RZ0001", () => null, RazorDiagnosticSeverity.Error);
        var span = new SourceSpan("test.cs", 15, 1, 8, 5);

        // Act
        var diagnostic = RazorDiagnostic.Create(descriptor, span, "Hello", "World");
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Encountered diagnostic 'RZ0001'.", message);
    }
}
