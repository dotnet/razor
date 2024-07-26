// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using System.Linq;

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

    public static void EnableSignatureHelp(this VSInternalServerCapabilities serverCapabilities)
    {
        serverCapabilities.SignatureHelpProvider = new SignatureHelpOptions().EnableSignatureHelp();
    }

    public static SignatureHelpOptions EnableSignatureHelp(this SignatureHelpOptions options)
    {
        options.TriggerCharacters = ["(", ",", "<"];
        options.RetriggerCharacters = [">", ")"];

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

    public static HashSet<string> HtmlAllowedAutoInsertTriggerCharacters { get; } = new(StringComparer.Ordinal) { "=", };
    public static HashSet<string> CSharpAllowedAutoInsertTriggerCharacters { get; } = new(StringComparer.Ordinal) { "'", "/", "\n" };

    public static void EnableOnAutoInsert(
        this VSInternalServerCapabilities serverCapabilities,
        bool singleServerSupport,
        IEnumerable<string> triggerCharacters)
    {
        serverCapabilities.OnAutoInsertProvider = new VSInternalDocumentOnAutoInsertOptions()
            .EnableOnAutoInsert(singleServerSupport, triggerCharacters);
    }

    public static VSInternalDocumentOnAutoInsertOptions EnableOnAutoInsert(
        this VSInternalDocumentOnAutoInsertOptions options,
        bool singleServerSupport,
        IEnumerable<string> triggerCharacters)
    {
        if (singleServerSupport)
        {
            triggerCharacters = triggerCharacters
                .Concat(HtmlAllowedAutoInsertTriggerCharacters)
                .Concat(CSharpAllowedAutoInsertTriggerCharacters);
        }

        options.TriggerCharacters = triggerCharacters.Distinct().ToArray();

        return options;
    }
}
