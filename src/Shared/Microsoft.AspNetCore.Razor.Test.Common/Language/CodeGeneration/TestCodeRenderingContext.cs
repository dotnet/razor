// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public static class TestCodeRenderingContext
{
    private sealed class TestCodeTarget() : CodeTarget
    {
        public override IntermediateNodeWriter CreateNodeWriter(CodeRenderingContext context)
            => new RuntimeNodeWriter(context);

        public override TExtension GetExtension<TExtension>()
            => throw new NotImplementedException();

        public override bool HasExtension<TExtension>()
            => throw new NotImplementedException();
    }

    public static CodeRenderingContext CreateDesignTime(
        string? newLineString = null,
        string suppressUniqueIds = "test",
        RazorSourceDocument? source = null)
    {
        source ??= TestRazorSourceDocument.Create();
        var documentNode = new DocumentIntermediateNode();

        var options = ConfigureOptions(RazorCodeGenerationOptions.DesignTimeDefault, newLineString, suppressUniqueIds);
        var codeTarget = new TestCodeTarget();

        var context = new CodeRenderingContext(codeTarget, source, documentNode, options);
        context.SetVisitor(new RenderChildrenVisitor(context.CodeWriter));

        return context;
    }

    public static CodeRenderingContext CreateRuntime(
        string? newLineString = null,
        string? suppressUniqueIds = "test",
        RazorSourceDocument? source = null)
    {
        source ??= TestRazorSourceDocument.Create();
        var documentNode = new DocumentIntermediateNode();

        var options = ConfigureOptions(RazorCodeGenerationOptions.Default, newLineString, suppressUniqueIds);
        var codeTarget = new TestCodeTarget();

        var context = new CodeRenderingContext(codeTarget, source, documentNode, options);
        context.SetVisitor(new RenderChildrenVisitor(context.CodeWriter));

        return context;
    }

    public static CodeRenderingContext Create(
        RazorCodeGenerationOptions? options = null,
        RazorSourceDocument? source = null)
    {
        source ??= TestRazorSourceDocument.Create();
        var documentNode = new DocumentIntermediateNode();

        options ??= RazorCodeGenerationOptions.Default;
        var codeTarget = new TestCodeTarget();

        var context = new CodeRenderingContext(codeTarget, source, documentNode, options);
        context.SetVisitor(new RenderChildrenVisitor(context.CodeWriter));

        return context;
    }

    private static RazorCodeGenerationOptions ConfigureOptions(RazorCodeGenerationOptions options, string? newLine, string? suppressUniqueIds)
    {
        if (newLine is null && suppressUniqueIds is null)
        {
            return options;
        }

        if (newLine is not null)
        {
            options = options.WithNewLine(newLine);
        }

        if (suppressUniqueIds is not null)
        {
            options = options.WithSuppressUniqueIds(suppressUniqueIds);
        }

        return options;
    }

    private class RenderChildrenVisitor(CodeWriter writer) : IntermediateNodeVisitor
    {
        public override void VisitDefault(IntermediateNode node)
        {
            writer.WriteLine("Render Children");
        }
    }
}
