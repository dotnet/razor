﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;

internal static class RazorLanguageServerCustomMessageTargets
{
    // VS Internal
    public const string RazorInlineCompletionEndpoint = "razor/inlineCompletion";
    public const string RazorValidateBreakpointRangeName = "razor/validateBreakpointRange";
    public const string RazorOnAutoInsertEndpointName = "razor/onAutoInsert";
    public const string RazorSemanticTokensRefreshEndpoint = "razor/semanticTokensRefresh";
    public const string RazorTextPresentationEndpoint = "razor/textPresentation";
    public const string RazorUriPresentationEndpoint = "razor/uriPresentation";

    // Cross platform
    public const string RazorUpdateCSharpBufferEndpoint = "razor/updateCSharpBuffer";
    public const string RazorUpdateHtmlBufferEndpoint = "razor/updateHtmlBuffer";
    public const string RazorProvideCodeActionsEndpoint = "razor/provideCodeActions";
    public const string RazorResolveCodeActionsEndpoint = "razor/resolveCodeActions";
    public const string RazorProvideHtmlColorPresentationEndpoint = "razor/provideHtmlColorPresentation";
    public const string RazorProvideHtmlDocumentColorEndpoint = "razor/provideHtmlDocumentColor";
    public const string RazorPullDiagnosticEndpointName = "razor/pullDiagnostics";
    public const string RazorProvideSemanticTokensRangeEndpoint = "razor/provideSemanticTokensRange";
    public const string RazorFoldingRangeEndpoint = "razor/foldingRange";
    public const string RazorHtmlFormattingEndpoint = "razor/htmlFormatting";
    public const string RazorHtmlOnTypeFormattingEndpoint = "razor/htmlOnTypeFormatting";

    // Still to migrate
    public const string RazorRenameEndpointName = "razor/rename";

    public const string RazorHoverEndpointName = "razor/hover";

    public const string RazorDefinitionEndpointName = "razor/definition";

    public const string RazorDocumentHighlightEndpointName = "razor/documentHighlight";

    public const string RazorSignatureHelpEndpointName = "razor/signatureHelp";

    public const string RazorImplementationEndpointName = "razor/implementation";

    public const string RazorReferencesEndpointName = "razor/references";
}
