// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
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

        // Make sure we're in the right kind of file
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

        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: false)?.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();
        if (owner is not MarkupTagHelperElementSyntax markupElementSyntax)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check whether the code action is applicable to the element
        if (!IsApplicableTo(markupElementSyntax, context.CodeDocument, out var fullyQualifiedName, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Provide code action to simplify
        var actionParams = new SimplifyFullyQualifiedComponentCodeActionParams
        {
            FullyQualifiedName = fullyQualifiedName,
            Namespace = @namespace
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

    internal static bool IsApplicableTo(MarkupTagHelperElementSyntax markupElementSyntax, RazorCodeDocument codeDocument, out string fullyQualifiedName, out string @namespace)
    {
        fullyQualifiedName = string.Empty;
        @namespace = string.Empty;

        // Check if the tag name contains a dot (fully qualified)
        var tagName = markupElementSyntax.StartTag?.Name.Content;
        if (string.IsNullOrEmpty(tagName) || !tagName.Contains('.'))
        {
            return false;
        }

        // Get the component descriptors
        if (markupElementSyntax is not { TagHelperInfo.BindingResult.Descriptors: [.. var descriptors] })
        {
            return false;
        }

        // Find the component descriptor
        var boundTagHelper = descriptors.FirstOrDefault(static d => d.Kind == TagHelperKind.Component);
        if (boundTagHelper == null)
        {
            return false;
        }

        // Extract namespace from the fully qualified name
        fullyQualifiedName = tagName;
        var lastDotIndex = tagName.LastIndexOf('.');
        if (lastDotIndex <= 0)
        {
            return false;
        }

        @namespace = tagName[..lastDotIndex];

        // Check if the using directive already exists
        var hasUsing = HasUsingDirective(codeDocument, @namespace);

        // Only offer if we can simplify (either using already exists, or we can add it)
        return true;
    }

    private static bool HasUsingDirective(RazorCodeDocument codeDocument, string @namespace)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return false;
        }

        foreach (var node in syntaxTree.Root.DescendantNodes())
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                foreach (var child in directiveNode.DescendantNodes())
                {
                    if (child.GetChunkGenerator() is AddImportChunkGenerator { IsStatic: false } usingStatement)
                    {
                        if (usingStatement.ParsedNamespace == @namespace)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
