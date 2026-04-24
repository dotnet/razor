// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal record RazorHtmlDependentCompletionContext(
    RazorCodeDocument CodeDocument,
    int AbsoluteIndex,
    RazorSyntaxNode? Owner,
    RazorSyntaxTree SyntaxTree,
    TagHelperDocumentContext TagHelperDocumentContext,
    HashSet<string> HtmlLabels,
    CompletionReason Reason = CompletionReason.Invoked,
    RazorCompletionOptions Options = default)
    : RazorCompletionContext(CodeDocument, AbsoluteIndex, Owner, SyntaxTree, TagHelperDocumentContext, Reason, Options)
{
}
