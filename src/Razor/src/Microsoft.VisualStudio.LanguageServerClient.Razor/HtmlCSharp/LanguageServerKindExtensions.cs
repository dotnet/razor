// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal static class LanguageServerKindExtensions
    {
        public static string ToLanguageServerName(this LanguageServerKind languageServerKind)
        {
            return languageServerKind switch
            {
                LanguageServerKind.CSharp => RazorLSPConstants.RazorCSharpLanguageServerName,
                LanguageServerKind.Html => RazorLSPConstants.HtmlLanguageServerName,
                LanguageServerKind.Razor => RazorLSPConstants.RazorLanguageServerName,
                _ => throw new System.NotImplementedException(),
            };
        }

        public static string ToContentType(this LanguageServerKind languageServerKind)
        {
            return languageServerKind switch
            {
                LanguageServerKind.CSharp => RazorLSPConstants.CSharpContentTypeName,
                LanguageServerKind.Html => RazorLSPConstants.HtmlLSPDelegationContentTypeName,
                _ => RazorLSPConstants.RazorLSPContentTypeName,
            };
        }
    }
}
