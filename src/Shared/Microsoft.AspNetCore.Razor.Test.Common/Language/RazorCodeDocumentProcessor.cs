// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorCodeDocumentProcessor
{
    public RazorProjectEngine ProjectEngine { get; }
    public RazorCodeDocument CodeDocument { get; }

    private RazorCodeDocumentProcessor(RazorProjectEngine projectEngine, RazorCodeDocument codeDocument)
    {
        ProjectEngine = projectEngine;
        CodeDocument = codeDocument;
    }

    public static RazorCodeDocumentProcessor From(RazorProjectEngine projectEngine, RazorCodeDocument codeDocument)
        => new(projectEngine, codeDocument);

    public RazorCodeDocumentProcessor ExecutePhasesThrough<T>()
        where T : IRazorEnginePhase
    {
        var codeDocument = ProjectEngine.ExecutePhasesThrough<T>(CodeDocument);

        return From(ProjectEngine, codeDocument);
    }

    public RazorCodeDocumentProcessor ExecutePass<T>()
        where T : IntermediateNodePassBase, new()
    {
        var codeDocument = ProjectEngine.ExecutePass<T>(CodeDocument);

        return From(ProjectEngine, codeDocument);
    }

    public RazorCodeDocumentProcessor ExecutePass<T>(Func<T> passFactory)
        where T : IntermediateNodePassBase
    {
        var codeDocument = ProjectEngine.ExecutePass<T>(CodeDocument, passFactory);

        return From(ProjectEngine, codeDocument);
    }

    public DocumentIntermediateNode GetDocumentNode()
    {
        var documentNode = CodeDocument.GetDocumentNode();
        Assert.NotNull(documentNode);

        return documentNode;
    }
}
