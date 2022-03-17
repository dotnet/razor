// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal static class RazorLSPConventions
    {
        public static bool IsVirtualCSharpFile(Uri uri) => CheckIfFileUriAndExtensionMatch(uri, RazorLSPConstants.VirtualCSharpFileNameSuffix);

        public static bool IsVirtualHtmlFile(Uri uri) => CheckIfFileUriAndExtensionMatch(uri, RazorLSPConstants.VirtualHtmlFileNameSuffix);

        public static bool IsRazorFile(Uri uri) => CheckIfFileUriAndExtensionMatch(uri, RazorLSPConstants.RazorFileExtension);

        public static bool IsCSHTMLFile(Uri uri) => CheckIfFileUriAndExtensionMatch(uri, RazorLSPConstants.CSHTMLFileExtension);

        public static Uri GetRazorDocumentUri(Uri virtualDocumentUri!!)
        {
            var path = virtualDocumentUri.AbsoluteUri;
            path = path.Replace(RazorLSPConstants.VirtualCSharpFileNameSuffix, string.Empty);
            path = path.Replace(RazorLSPConstants.VirtualHtmlFileNameSuffix, string.Empty);

            var uri = new Uri(path, UriKind.Absolute);
            return uri;
        }

        private static bool CheckIfFileUriAndExtensionMatch(Uri uri!!, string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentNullException(nameof(extension));
            }

            return uri.GetAbsoluteOrUNCPath()?.EndsWith(extension, StringComparison.Ordinal) ?? false;
        }
    }
}
