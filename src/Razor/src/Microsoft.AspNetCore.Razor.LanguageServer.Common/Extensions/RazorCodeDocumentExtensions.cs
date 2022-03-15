﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions
{
    internal static class RazorCodeDocumentExtensions
    {
        private static readonly object s_unsupportedKey = new();

        public static bool IsUnsupported(this RazorCodeDocument document!!)
        {
            var unsupportedObj = document.Items[s_unsupportedKey];
            if (unsupportedObj is null)
            {
                return false;
            }

            return (bool)unsupportedObj;
        }

        public static void SetUnsupported(this RazorCodeDocument document!!)
        {
            document.Items[s_unsupportedKey] = true;
        }
    }
}
