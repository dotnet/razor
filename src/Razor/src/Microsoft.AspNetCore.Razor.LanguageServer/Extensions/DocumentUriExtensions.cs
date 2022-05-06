// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class DocumentUriExtensions
    {
        public static string GetAbsoluteOrUNCPath(this DocumentUri documentUri)
        {
            if (documentUri is null)
            {
                throw new ArgumentNullException(nameof(documentUri));
            }

            return documentUri.ToUri().GetAbsoluteOrUNCPath();
        }
    }
}
