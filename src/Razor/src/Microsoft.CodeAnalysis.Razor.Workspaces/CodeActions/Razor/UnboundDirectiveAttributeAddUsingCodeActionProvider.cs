// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
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

        // Find a regular markup attribute (not a tag helper attribute) that starts with '@'
        // Unbound directive attributes are just regular attributes that happen to start with '@'
        var attributeBlock = owner.FirstAncestorOrSelf<MarkupAttributeBlockSyntax>();
        if (attributeBlock is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Get the attribute name - it includes the '@' prefix for directive attributes
        var attributeName = attributeBlock.Name.GetContent();

        // Check if this is a directive attribute (starts with '@')
        if (string.IsNullOrEmpty(attributeName) || !attributeName.StartsWith("@"))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Try to find the missing namespace for this directive attribute
        if (!TryGetMissingDirectiveAttributeNamespace(
            context.CodeDocument,
            attributeName,
            out var missingNamespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Create the code action
        var resolutionParams = AddUsingsCodeActionResolver.CreateAddUsingResolutionParams(
            missingNamespace,
            context.Request.TextDocument,
            additionalEdit: null,
            context.DelegatedDocumentUri);

        var addUsingCodeAction = RazorCodeActionFactory.CreateAddComponentUsing(
            missingNamespace,
            newTagName: null,
            resolutionParams);

        // Set high priority and order to show prominently
        addUsingCodeAction.Priority = VSInternalPriorityLevel.High;
        addUsingCodeAction.Order = -999;

        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([addUsingCodeAction]);
    }

    private static bool TryGetMissingDirectiveAttributeNamespace(
        RazorCodeDocument codeDocument,
        string attributeName,
        [NotNullWhen(true)] out string? missingNamespace)
    {
        missingNamespace = null;

        // Get all tag helpers, not just those in scope, since we want to suggest adding a using
        var tagHelpers = codeDocument.GetTagHelpers();
        if (tagHelpers is null)
        {
            return false;
        }

        // For attributes with parameters (e.g., @bind:after), extract just the base attribute name
        var baseAttributeName = attributeName;
        var colonIndex = attributeName.IndexOf(':');
        if (colonIndex > 0)
        {
            baseAttributeName = attributeName[..colonIndex];
        }

        // Search for matching bound attribute descriptors in all available tag helpers
        foreach (var tagHelper in tagHelpers)
        {
            foreach (var boundAttribute in tagHelper.BoundAttributes)
            {
                if (boundAttribute.Name == baseAttributeName)
                {
                    // Extract namespace from the type name
                    var typeName = boundAttribute.TypeName;

                    // Apply heuristics to determine the namespace
                    // Check for Web namespace indicators (event args types are defined there)
                    if (typeName.Contains(".Web.") || typeName.Contains(".Web>") ||
                        typeName.Contains("EventArgs") || typeName.Contains("EventCallback"))
                    {
                        missingNamespace = "Microsoft.AspNetCore.Components.Web";
                        return true;
                    }
                    else if (typeName.Contains(".Forms.") || typeName.Contains(".Forms>"))
                    {
                        missingNamespace = "Microsoft.AspNetCore.Components.Forms";
                        return true;
                    }
                    else
                    {
                        // Extract namespace from type name using the existing method
                        var extractedNamespace = AddUsingsCodeActionResolver.GetNamespaceFromFQN(typeName);
                        if (!string.IsNullOrEmpty(extractedNamespace))
                        {
                            missingNamespace = extractedNamespace;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
