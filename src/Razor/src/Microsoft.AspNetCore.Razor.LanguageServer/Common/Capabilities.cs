// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;

internal static class Capabilities
{
    public static void ApplyDocumentColorProvider(VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.DocumentColorProvider = new DocumentColorOptions();
    }

    public static void ApplySemanticTokensOptions(VSInternalServerCapabilities serverCapabilities, SemanticTokensLegend legend)
    {
        serverCapabilities.SemanticTokensOptions = new SemanticTokensOptions
        {
            Full = false,
            Legend = legend,
            Range = true,
        };
    }
}
