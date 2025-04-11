﻿// Licensed to the .NET Foundation under one or more agreements.
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
        ProjectEngine.ExecutePhasesThrough<T>(CodeDocument);

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePass<T>()
        where T : IntermediateNodePassBase, new()
    {
        ProjectEngine.ExecutePass<T>(CodeDocument);

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePass<T>(Func<T> passFactory)
        where T : IntermediateNodePassBase
    {
        ProjectEngine.ExecutePass<T>(CodeDocument, passFactory);

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePhase<T>(RazorCodeDocument codeDocument)
        where T : IRazorEnginePhase, new()
    {
        ProjectEngine.ExecutePhase<T>(codeDocument);

        return this;
    }

    public RazorCodeDocumentProcessor ExecutePhase<T>(RazorCodeDocument codeDocument, Func<T> phaseFactory)
        where T : IRazorEnginePhase
    {
        ProjectEngine.ExecutePhase<T>(codeDocument, phaseFactory);

        return this;
    }

    public DocumentIntermediateNode GetDocumentNode()
    {
        var documentNode = CodeDocument.GetDocumentIntermediateNode();
        Assert.NotNull(documentNode);

        return documentNode; ;
    }
}
