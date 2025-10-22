// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SimplifyFullyQualifiedComponentCodeActionProvider : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context.HasSelection)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Make sure we're in the right kind and part of file
        if (!FileKinds.IsComponent(context.CodeDocument.FileKind))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (context.LanguageKind != RazorLanguageKind.Html)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Find the element at the cursor position
        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: false)?.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();
        if (owner is not MarkupTagHelperElementSyntax markupElementSyntax)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // If there are any diagnostics on the start tag, we shouldn't offer
        if (HasDiagnosticsOnStartTag(markupElementSyntax, context))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check whether the element represents a fully qualified component
        if (!IsFullyQualifiedComponent(markupElementSyntax, out var @namespace, out var componentName))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Create the action params
        var actionParams = new SimplifyFullyQualifiedComponentCodeActionParams
        {
            Namespace = @namespace,
            ComponentName = componentName,
            StartTagSpanStart = markupElementSyntax.StartTag.Name.SpanStart,
            StartTagSpanEnd = markupElementSyntax.StartTag.Name.Span.End,
            EndTagSpanStart = markupElementSyntax.EndTag?.Name.SpanStart ?? -1,
            EndTagSpanEnd = markupElementSyntax.EndTag?.Name.Span.End ?? -1,
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            TextDocument = context.Request.TextDocument,
            Action = LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            Language = RazorLanguageKind.Razor,
            DelegatedDocumentUri = context.DelegatedDocumentUri,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateSimplifyFullyQualifiedComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static bool HasDiagnosticsOnStartTag(MarkupTagHelperElementSyntax element, RazorCodeActionContext context)
    {
        if (context.Request.Context.Diagnostics is null)
        {
            return false;
        }

        var startTagSpan = element.StartTag.Span;
        foreach (var diagnostic in context.Request.Context.Diagnostics)
        {
            if (diagnostic.Range is null)
            {
                continue;
            }

            var diagnosticStart = context.SourceText.GetRequiredAbsoluteIndex(diagnostic.Range.Start);
            var diagnosticEnd = context.SourceText.GetRequiredAbsoluteIndex(diagnostic.Range.End);

            // Check if diagnostic overlaps with the start tag
            if (diagnosticStart < startTagSpan.End && diagnosticEnd > startTagSpan.Start)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFullyQualifiedComponent(MarkupTagHelperElementSyntax element, out string @namespace, out string componentName)
    {
        @namespace = string.Empty;
        componentName = string.Empty;

        if (element.TagHelperInfo?.BindingResult?.Descriptors is not [.. var descriptors])
        {
            return false;
        }

        var boundTagHelper = descriptors.FirstOrDefault(static d => d.Kind == TagHelperKind.Component);
        if (boundTagHelper is null)
        {
            return false;
        }

        // Check if this is a fully qualified name match
        if (!boundTagHelper.IsFullyQualifiedNameMatch)
        {
            return false;
        }

        var fullyQualifiedName = boundTagHelper.Name;

        // Extract the namespace and component name
        var lastDotIndex = fullyQualifiedName.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            return false;
        }

        @namespace = fullyQualifiedName[..lastDotIndex];
        componentName = fullyQualifiedName[(lastDotIndex + 1)..];
        return true;
    }
}
