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
using Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal class AddUsingsCodeActionProvider : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context.HasSelection)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Make sure we're in a Razor or component file
        if (!FileKinds.IsComponent(context.CodeDocument.FileKind) && !FileKinds.IsLegacy(context.CodeDocument.FileKind))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!context.CodeDocument.TryGetSyntaxRoot(out var syntaxRoot))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Find the node at the cursor position
        var owner = syntaxRoot.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: false);
        if (owner is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check if we're in a fully qualified component tag
        if (owner.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>() is { } markupTagHelperElement)
        {
            var startTag = markupTagHelperElement.StartTag;
            if (startTag is not null &&
                startTag.Name.Content.Contains('.') &&
                startTag.Name.Span.Contains(context.StartAbsoluteIndex))
            {
                var fullyQualifiedName = startTag.Name.Content;

                // Check if this matches a tag helper
                var descriptors = markupTagHelperElement.TagHelperInfo.BindingResult.Descriptors;
                var boundTagHelper = descriptors.FirstOrDefault(static d => d.Kind == TagHelperKind.Component);

                if (boundTagHelper is not null && boundTagHelper.IsFullyQualifiedNameMatch)
                {
                    // Create the add using code action
                    if (AddUsingsCodeActionResolver.TryCreateAddUsingResolutionParams(
                        fullyQualifiedName,
                        context.Request.TextDocument,
                        additionalEdit: null,
                        context.DelegatedDocumentUri,
                        out var extractedNamespace,
                        out var resolutionParams))
                    {
                        // Extract component name for the title
                        var lastDotIndex = fullyQualifiedName.LastIndexOf('.');
                        var componentName = lastDotIndex > 0 ? fullyQualifiedName[(lastDotIndex + 1)..] : null;

                        var addUsingCodeAction = RazorCodeActionFactory.CreateAddComponentUsing(extractedNamespace, componentName, resolutionParams);
                        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([addUsingCodeAction]);
                    }
                }
            }
        }

        return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
    }
}
