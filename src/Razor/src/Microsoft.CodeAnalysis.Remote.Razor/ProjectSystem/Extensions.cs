// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

internal static class Extensions
{
    private const string RazorExtension = ".razor";
    private const string CSHtmlExtension = ".cshtml";

    public static bool IsRazorFilePath(this string filePath)
    {
        var comparison = PathUtilities.OSSpecificPathComparison;

        return filePath.EndsWith(RazorExtension, comparison) ||
               filePath.EndsWith(CSHtmlExtension, comparison);
    }

    public static bool IsRazorDocument(this TextDocument document)
        => document is AdditionalDocument &&
           document.FilePath is string filePath &&
           filePath.IsRazorFilePath();

    public static bool ContainsRazorDocuments(this Project project)
        => project.AdditionalDocuments.Any(static d => d.IsRazorDocument());

    public static Uri GetRazorDocumentUri(this Solution solution, RazorCodeDocument codeDocument)
    {
        var filePath = codeDocument.Source.FilePath;
        var documentId = solution.GetDocumentIdsWithFilePath(filePath).First();
        var document = solution.GetAdditionalDocument(documentId).AssumeNotNull();
        return document.CreateUri();
    }
}
