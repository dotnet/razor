// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public RazorCodeDocumentProcessor RunPhasesTo<T>()
        where T : IRazorEnginePhase
    {
        ProjectEngine.RunPhasesTo<T>(CodeDocument);

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePass<T>()
        where T : IntermediateNodePassBase, new()
    {
        ProjectEngine.ExecutePass<T>(CodeDocument);

        return this;
    }

    public DocumentIntermediateNode GetDocumentNode()
    {
        var documentNode = CodeDocument.GetDocumentIntermediateNode();
        Assert.NotNull(documentNode);

        return documentNode; ;
    }
}
