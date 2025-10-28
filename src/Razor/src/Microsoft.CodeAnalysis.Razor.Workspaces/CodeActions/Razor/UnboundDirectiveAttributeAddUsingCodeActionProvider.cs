// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal class UnboundDirectiveAttributeAddUsingCodeActionProvider : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context.HasSelection)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Only work in component files
        if (!FileKinds.IsComponent(context.CodeDocument.FileKind))
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

        // Find the directive attribute ancestor
        var directiveAttribute = owner.FirstAncestorOrSelf<MarkupTagHelperDirectiveAttributeSyntax>();
        if (directiveAttribute?.TagHelperAttributeInfo is not { } attributeInfo)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check if it's an unbound directive attribute
        if (attributeInfo.Bound || !attributeInfo.IsDirectiveAttribute)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Try to find the missing namespace
        if (!TryGetMissingDirectiveAttributeNamespace(
            context.CodeDocument,
            attributeInfo,
            out var missingNamespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Check if the namespace is already imported
        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree is not null)
        {
            var existingUsings = syntaxTree.EnumerateUsingDirectives()
                .SelectMany(d => d.DescendantNodes())
                .Select(n => n.GetChunkGenerator())
                .OfType<AddImportChunkGenerator>()
                .Where(g => !g.IsStatic)
                .Select(g => g.ParsedNamespace)
                .ToImmutableArray();

            if (existingUsings.Contains(missingNamespace))
            {
                return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
            }
        }

        // Create the code action
        if (AddUsingsCodeActionResolver.TryCreateAddUsingResolutionParams(
            missingNamespace + ".Dummy", // Dummy type name to extract namespace
            context.Request.TextDocument,
            additionalEdit: null,
            context.DelegatedDocumentUri,
            out var extractedNamespace,
            out var resolutionParams))
        {
            var addUsingCodeAction = RazorCodeActionFactory.CreateAddComponentUsing(
                extractedNamespace,
                newTagName: null,
                resolutionParams);

            // Set high priority and order to show prominently
            addUsingCodeAction.Priority = VSInternalPriorityLevel.High;
            addUsingCodeAction.Order = -999;

            return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([addUsingCodeAction]);
        }

        return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
    }

    private static bool TryGetMissingDirectiveAttributeNamespace(
        RazorCodeDocument codeDocument,
        TagHelperAttributeInfo attributeInfo,
        [NotNullWhen(true)] out string? missingNamespace)
    {
        missingNamespace = null;

        var tagHelperContext = codeDocument.GetRequiredTagHelperContext();
        var attributeName = attributeInfo.Name;

        // For attributes with parameters, extract just the attribute name
        if (attributeInfo.ParameterName is not null)
        {
            var colonIndex = attributeName.IndexOf(':');
            if (colonIndex >= 0)
            {
                attributeName = attributeName[..colonIndex];
            }
        }

        // Search for matching bound attribute descriptors
        foreach (var tagHelper in tagHelperContext.TagHelpers)
        {
            foreach (var boundAttribute in tagHelper.BoundAttributes)
            {
                if (boundAttribute.Name == attributeName)
                {
                    // Extract namespace from the type name
                    var typeName = boundAttribute.TypeName;

                    // Apply heuristics to determine the namespace
                    if (typeName.Contains(".Web.") || typeName.EndsWith(".Web.EventHandlers"))
                    {
                        missingNamespace = "Microsoft.AspNetCore.Components.Web";
                        return true;
                    }
                    else if (typeName.Contains(".Forms."))
                    {
                        missingNamespace = "Microsoft.AspNetCore.Components.Forms";
                        return true;
                    }
                    else
                    {
                        // Extract namespace from type name (everything before the last dot)
                        var lastDotIndex = typeName.LastIndexOf('.');
                        if (lastDotIndex > 0)
                        {
                            missingNamespace = typeName[..lastDotIndex];
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
