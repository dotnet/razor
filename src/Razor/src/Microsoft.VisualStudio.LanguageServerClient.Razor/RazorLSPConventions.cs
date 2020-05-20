// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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

            return uri.AbsolutePath?.EndsWith(VirtualCSharpFileNameSuffix) ?? false;
        }

        public static bool IsRazorHtmlFile(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            return uri.AbsolutePath?.EndsWith(VirtualHtmlFileNameSuffix) ?? false;
        }
    }
}
