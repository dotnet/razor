// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions
{
    internal static class RazorCodeDocumentExtensions
    {
        private static readonly object s_unsupportedKey = new object();
        private static readonly object s_sourceTextKey = new object();
        private static readonly object s_csharpSourceTextKey = new object();
        private static readonly object s_htmlSourceTextKey = new object();

        public static bool IsUnsupported(this RazorCodeDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var unsupportedObj = document.Items[s_unsupportedKey];
            if (unsupportedObj == null)
            {
                return false;
            }

            return (bool)unsupportedObj;
        }

        public static void SetUnsupported(this RazorCodeDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.Items[s_unsupportedKey] = true;
        }
    }
}
