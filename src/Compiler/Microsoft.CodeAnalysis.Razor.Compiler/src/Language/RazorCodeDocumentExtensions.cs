// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorCodeDocumentExtensions
{
    public static bool TryComputeClassName(this RazorCodeDocument codeDocument, [NotNullWhen(true)] out string? className)
    {
        var filePath = codeDocument.Source.RelativePath ?? codeDocument.Source.FilePath;
        if (filePath.IsNullOrEmpty())
        {
            className = null;
            return false;
        }

        className = CSharpIdentifier.GetClassNameFromPath(filePath);
        return className is not null;
    }

    public static bool TryGetNamespace(
        this RazorCodeDocument codeDocument,
        bool fallbackToRootNamespace,
        [NotNullWhen(true)] out string? @namespace)
        => codeDocument.TryGetNamespace(fallbackToRootNamespace, out @namespace, out _);

    public static bool TryGetNamespace(
        this RazorCodeDocument codeDocument,
        bool fallbackToRootNamespace,
        [NotNullWhen(true)] out string? @namespace,
        out SourceSpan? namespaceSpan)
        => codeDocument.TryGetNamespace(fallbackToRootNamespace, considerImports: true, out @namespace, out namespaceSpan);

    internal static ImmutableArray<BaseRazorDirectiveSyntax> GetUnusedDirectives(this RazorCodeDocument codeDocument)
    {
        // Never report unused directives in imports files, as we don't track at that level
        if (codeDocument.FileKind.IsComponentImport() ||
            string.Equals(Path.GetFileName(codeDocument.Source.FilePath), MvcImportProjectFeature.ImportsFileName, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var contributions = codeDocument.GetDirectiveTagHelperContributions();
        if (contributions.IsDefaultOrEmpty)
        {
            return [];
        }

        var referencedTagHelpers = codeDocument.GetReferencedTagHelpers();

        using var unusedDirectives = new PooledArrayBuilder<BaseRazorDirectiveSyntax>();
        foreach (var contribution in contributions)
        {
            if (referencedTagHelpers is null ||
                contribution.ContributedTagHelpers.IsEmpty ||
                !AnyContributedTagHelperIsReferenced(contribution.ContributedTagHelpers, referencedTagHelpers))
            {
                unusedDirectives.Add(contribution.Directive);
            }
        }

        return unusedDirectives.ToImmutableAndClear();

        static bool AnyContributedTagHelperIsReferenced(
            TagHelperCollection contributedTagHelpers,
            TagHelperCollection referencedTagHelpers)
        {
            foreach (var contributed in contributedTagHelpers)
            {
                if (referencedTagHelpers.Contains(contributed))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
