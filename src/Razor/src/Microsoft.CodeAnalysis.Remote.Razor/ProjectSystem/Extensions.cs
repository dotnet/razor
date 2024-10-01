// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

internal static class Extensions
{
    private const string RazorExtension = ".razor";
    private const string CSHtmlExtension = ".cshtml";

    public static bool IsRazorDocument(this TextDocument document)
    {
        if (document is not AdditionalDocument ||
            document.FilePath is not string filePath)
        {
            return false;
        }

        var comparison = FilePathComparison.Instance;

        return filePath.EndsWith(RazorExtension, comparison) ||
               filePath.EndsWith(CSHtmlExtension, comparison);
    }
}
