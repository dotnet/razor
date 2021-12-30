﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public static class RazorDefaults
    {
        public static DocumentSelector Selector { get; } = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.{cshtml,razor}"
            });

        public static RazorConfiguration Configuration { get; } = FallbackRazorConfiguration.Latest;

        public static string RootNamespace { get; } = null;
    }
}
