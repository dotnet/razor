﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hosting;

internal static class VSInternalServerCapabilitiesExtensions
{
    public static void EnableInlayHints(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.InlayHintOptions = new InlayHintOptions
        {
            ResolveProvider = true,
            WorkDoneProgress = false
        };
    }

    public static void EnableDocumentColorProvider(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.DocumentColorProvider = new DocumentColorOptions();
    }

    public static void EnableSemanticTokens(this VSInternalServerCapabilities serverCapabilities, ISemanticTokensLegendService legend)
    {
        serverCapabilities.SemanticTokensOptions = new SemanticTokensOptions().EnableSemanticTokens(legend);
    }

    public static SemanticTokensOptions EnableSemanticTokens(this SemanticTokensOptions options, ISemanticTokensLegendService legend)
    {
        options.Full = false;
        options.Legend = new SemanticTokensLegend
        {
            TokenModifiers = legend.TokenModifiers.All,
            TokenTypes = legend.TokenTypes.All
        };
        options.Range = true;

        return options;
    }

    public static void EnableHoverProvider(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.HoverProvider = new HoverOptions()
        {
            WorkDoneProgress = false,
        };
    }

    public static void EnableValidateBreakpointRange(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.BreakableRangeProvider = true;
    }

    public static void EnableMapCodeProvider(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.MapCodeProvider = true;
    }
}
