// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorCodeDocumentExtensions
{
    private static readonly object NamespaceKey = new();

    internal static RazorHtmlDocument GetHtmlDocument(this RazorCodeDocument codeDocument)
    {
        ArgHelper.ThrowIfNull(codeDocument);

        var razorHtmlObj = codeDocument.Items[typeof(RazorHtmlDocument)];
        if (razorHtmlObj == null)
        {
            var razorHtmlDocument = RazorHtmlWriter.GetHtmlDocument(codeDocument);
            codeDocument.Items[typeof(RazorHtmlDocument)] = razorHtmlDocument;
            return razorHtmlDocument;
        }

        return (RazorHtmlDocument)razorHtmlObj;
    }

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

    public static bool TryComputeNamespace(this RazorCodeDocument codeDocument, bool fallbackToRootNamespace, out string @namespace)
        => TryComputeNamespace(codeDocument, fallbackToRootNamespace, out @namespace, out _);

    public static bool TryComputeNamespace(this RazorCodeDocument document, bool fallbackToRootNamespace, out string @namespace, out SourceSpan? namespaceSpan)
        => TryComputeNamespace(document, fallbackToRootNamespace, considerImports: true, out @namespace, out namespaceSpan);

    // In general documents will have a relative path (relative to the project root).
    // We can only really compute a nice namespace when we know a relative path.
    //
    // However all kinds of thing are possible in tools. We shouldn't barf here if the document isn't
    // set up correctly.
    public static bool TryComputeNamespace(this RazorCodeDocument codeDocument, bool fallbackToRootNamespace, bool considerImports, out string @namespace, out SourceSpan? namespaceSpan)
    {
        ArgHelper.ThrowIfNull(codeDocument);

        var cachedNsInfo = codeDocument.Items[NamespaceKey];
        // We only want to cache the namespace if we're considering all possibilities. Anyone wanting something different (ie, tooling) has to pay a slight penalty.
        if (cachedNsInfo is not null && fallbackToRootNamespace && considerImports)
        {
            (@namespace, namespaceSpan) = ((string, SourceSpan?))cachedNsInfo;
        }
        else
        {
            var result = NamespaceComputer.TryComputeNamespace(codeDocument, fallbackToRootNamespace, considerImports, out @namespace, out namespaceSpan);
            if (result)
            {
                codeDocument.Items[NamespaceKey] = (@namespace, namespaceSpan);
            }
            return result;
        }

#if DEBUG
        // In debug mode, even if we're cached, lets take the hit to run this again and make sure the cached value is correct.
        // This is to help us find issues with caching logic during development.
        var validateResult = NamespaceComputer.TryComputeNamespace(codeDocument, fallbackToRootNamespace, considerImports, out var validateNamespace, out _);
        Debug.Assert(validateResult, "We couldn't compute the namespace, but have a cached value, so something has gone wrong");
        Debug.Assert(validateNamespace == @namespace, $"We cached a namespace of {@namespace} but calculated that it should be {validateNamespace}");
#endif

        return true;
    }
}
