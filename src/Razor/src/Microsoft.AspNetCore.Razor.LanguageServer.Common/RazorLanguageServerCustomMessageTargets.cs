﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common
{
    internal static class RazorLanguageServerCustomMessageTargets
    {
        public const string RazorUpdateCSharpBufferEndpoint = "razor/updateCSharpBuffer";

        public const string RazorUpdateHtmlBufferEndpoint = "razor/updateHtmlBuffer";

        public const string RazorRangeFormattingEndpoint = "razor/rangeFormatting";

        public const string RazorProvideCodeActionsEndpoint = "razor/provideCodeActions";

        public const string RazorResolveCodeActionsEndpoint = "razor/resolveCodeActions";

        public const string RazorProvideSemanticTokensRangeEndpoint = "razor/provideSemanticTokensRange";

        public const string RazorProvideHtmlDocumentColorEndpoint = "razor/provideHtmlDocumentColor";

        public const string RazorServerReadyEndpoint = "razor/serverReady";

        public const string RazorInlineCompletionEndpoint = "razor/inlineCompletion";

        public const string RazorFoldingRangeEndpoint = "razor/foldingRange";

        public const string RazorSemanticTokensRefreshEndpoint = "razor/semanticTokensRefresh";

        public const string RazorTextPresentationEndpoint = "razor/textPresentation";

        public const string RazorUriPresentationEndpoint = "razor/uriPresentation";

        public const string RazorRenameEndpointName = "razor/rename";

        public const string RazorHoverEndpointName = "razor/hover";
    }
}
