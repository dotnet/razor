// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal static class RazorLSPConventions
    {
        public const string RazorLSPContentTypeName = "RazorLSP";

        public const string CSHTMLFileExtension = ".cshtml";

        public const string RazorFileExtension = ".razor";

        public const string ContainedLanguageMarker = "ContainedLanguageMarker";

        public const string CSharpLSPContentTypeName = "C#_LSP";

        public const string HtmlLSPContentTypeName = "htmlyLSP";

        public const string VirtualCSharpFileNameSuffix = ".g.cs";

        public const string VirtualHtmlFileNameSuffix = "__virtual.html";

        public static bool IsRazorCSharpFile(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            return uri.GetAbsoluteOrUNCPath()?.EndsWith(VirtualCSharpFileNameSuffix) ?? false;
        }

        public static bool IsRazorHtmlFile(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            return uri.GetAbsoluteOrUNCPath()?.EndsWith(VirtualHtmlFileNameSuffix) ?? false;
        }

        public static Uri GetRazorDocumentUri(Uri virtualDocumentUri)
        {
            if (virtualDocumentUri is null)
            {
                throw new ArgumentNullException(nameof(virtualDocumentUri));
            }

            var path = virtualDocumentUri.GetAbsoluteOrUNCPath();
            path = path.Replace(VirtualCSharpFileNameSuffix, string.Empty);
            path = path.Replace(VirtualHtmlFileNameSuffix, string.Empty);

            var uri = new Uri(path, UriKind.Absolute);
            return uri;
        }
    }
}
