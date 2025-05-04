// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SimplifyTagToSelfClosingCodeActionProvider : IRazorCodeActionProvider
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

        // Caret must be inside a markup element
        if (context.ContainsDiagnostic(ComponentDiagnosticFactory.UnexpectedMarkupElement.Id) ||
            context.ContainsDiagnostic(ComponentDiagnosticFactory.UnexpectedClosingTag.Id))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: false)?.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();
        if (owner is not MarkupTagHelperElementSyntax markupElementSyntax)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check whether the code action is applicable to the element
        if (!IsApplicableTo(markupElementSyntax))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Provide code action to simplify
        var actionParams = new SimplifyTagToSelfClosingCodeActionParams
        {
            StartTagCloseAngleIndex = markupElementSyntax.StartTag.CloseAngle.SpanStart,
            EndTagCloseAngleIndex = markupElementSyntax.EndTag.CloseAngle.EndPosition,
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            TextDocument = context.Request.TextDocument,
            Action = LanguageServerConstants.CodeActions.SimplifyTagToSelfClosingAction,
            Language = RazorLanguageKind.Razor,
            DelegatedDocumentUri = context.DelegatedDocumentUri,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateSimplifyTagToSelfClosing(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    internal static bool IsApplicableTo(MarkupTagHelperElementSyntax markupElementSyntax)
    {
        // Check whether the element is self-closing
        if (markupElementSyntax is not (
        { EndTag.CloseAngle.IsMissing: false } and
        { StartTag.ForwardSlash: null } and
        { StartTag.CloseAngle.IsMissing: false } and
        { TagHelperInfo.BindingResult.Descriptors: { IsEmpty: false } descriptors }
        ))
        {
            return false;
        }

        // Check whether the element has any non-whitespace content
        if (markupElementSyntax is { Body: { } body } && body.Any(static n => !n.ContainsOnlyWhitespace()))
        {
            return false;
        }

        // Get symbols for the markup element
        var boundTagHelper = descriptors.FirstOrDefault(static d => d.IsComponentTagHelper);
        if (boundTagHelper == null)
        {
            return false;
        }

        // Check whether the Component must have children
        foreach (var attribute in boundTagHelper.BoundAttributes)
        {
            // Parameter is not required
            if (attribute is not { IsEditorRequired: true })
            {
                continue;
            }

            // Parameter is not a `RenderFragment` or `RenderFragment<T>`
            if (!attribute.IsChildContentProperty())
            {
                continue;
            }

            // Parameter is not set or bound as an attribute
            if (!markupElementSyntax.TagHelperInfo!.BindingResult.Attributes.Any(a =>
                RazorSyntaxFacts.TryGetComponentParameterNameFromFullAttributeName(a.Key, out var componentParameterName, out var directiveAttributeParameter) &&
                componentParameterName.SequenceEqual(attribute.Name) &&
                directiveAttributeParameter is { IsEmpty: true } or "get"
            ))
            {
                return false;
            }
        }

        return true;
    }
}
