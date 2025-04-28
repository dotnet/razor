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
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SimplifyTagToSelfClosingCodeActionProvider(ILoggerFactory loggerFactory) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<SimplifyTagToSelfClosingCodeActionProvider>();

    private const string RenderFragmentTypeName = "Microsoft.AspNetCore.Components.RenderFragment";
    private const string GenericRenderFragmentTypeName = "Microsoft.AspNetCore.Components.RenderFragment<";

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

        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: !context.HasSelection)?.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();
        if (owner is not MarkupTagHelperElementSyntax markupElementSyntax)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check whether the code action is applicable to the element
        if (!IsApplicableTo(context, markupElementSyntax))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Provide code action to simplify
        var actionParams = new SimplifyTagToSelfClosingCodeActionParams
        {
            Start = markupElementSyntax.StartTag.CloseAngle.SpanStart,
            End = markupElementSyntax.EndTag.CloseAngle.EndPosition,
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

    internal bool IsApplicableTo(RazorCodeActionContext context, MarkupTagHelperElementSyntax markupElementSyntax)
    {
        // Check whether the element is self-closing
        if (markupElementSyntax is not (
        { EndTag.CloseAngle.IsMissing: false } and
        { StartTag.ForwardSlash: null } and
        { StartTag.CloseAngle.IsMissing: false }
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
        if (!RazorComponentDefinitionHelpers.TryGetBoundTagHelpers(context.CodeDocument, markupElementSyntax.StartTag.Name.SpanStart, true, _logger, out var boundTagHelper, out _))
        {
            return false;
        }

        if (!boundTagHelper.IsComponentTagHelper)
        {
            return false;
        }

        // Check whether the Component must have children
        if (boundTagHelper.BoundAttributes.Any(attribute =>
            // Attribute has `EditorRequired` flag
            attribute is { TypeName: string typeName, IsEditorRequired: true } &&

            // It has type of a `RenderFragment`
            (typeName == RenderFragmentTypeName || typeName.StartsWith(GenericRenderFragmentTypeName, StringComparison.Ordinal)) &&

            // It is not set or bound as an attribute
            !markupElementSyntax.TagHelperInfo!.BindingResult.Attributes.Any(a =>
                a.Key == attribute.Name ||
                (a.Key.StartsWith("@bind-", StringComparison.Ordinal) && a.Key.AsSpan("@bind-".Length).Equals(attribute.Name, StringComparison.Ordinal))
            )
        ))
        {
            return false;
        }

        return true;
    }
}
