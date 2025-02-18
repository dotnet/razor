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
        foreach (var phase in ProjectEngine.Engine.Phases)
        {
            phase.Execute(CodeDocument);

            if (phase is T)
            {
                break;
            }
        }

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePass<T>(DocumentIntermediateNode? documentNode = null)
        where T : IntermediateNodePassBase, new()
    {
        var pass = new T()
        {
            Engine = ProjectEngine.Engine
        };

        documentNode ??= CodeDocument.GetDocumentIntermediateNode();
        Assert.NotNull(documentNode);

        pass.Execute(CodeDocument, documentNode);

        return this;
    }

    public DocumentIntermediateNode GetDocumentNode()
    {
        var documentNode = CodeDocument.GetDocumentIntermediateNode();
        Assert.NotNull(documentNode);

        return documentNode; ;
    }
}
