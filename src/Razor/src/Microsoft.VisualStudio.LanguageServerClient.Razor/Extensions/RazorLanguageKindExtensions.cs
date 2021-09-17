// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class RazorLanguageKindExtensions
    {
        public static string ToContainedLanguageContentType(this RazorLanguageKind razorLanguageKind) =>
            razorLanguageKind == RazorLanguageKind.CSharp ? RazorLSPConstants.CSharpContentTypeName : RazorLSPConstants.HtmlLSPDelegationContentTypeName;

        public static string ToContainedLanguageServerName(this RazorLanguageKind razorLanguageKind)
        {
            return razorLanguageKind switch
            {
                RazorLanguageKind.CSharp => RazorLSPConstants.RazorCSharpLanguageServerName,
                RazorLanguageKind.Html => RazorLSPConstants.HtmlLanguageServerName,
                RazorLanguageKind.Razor => RazorLSPConstants.RazorLanguageServerName,
                _ => throw new NotImplementedException("A RazorLanguageKind did not have a corresponding ClientName"),
            };
        }
    }
}
