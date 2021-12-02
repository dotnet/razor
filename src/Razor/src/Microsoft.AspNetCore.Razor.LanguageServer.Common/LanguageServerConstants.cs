// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common
{
    public static class LanguageServerConstants
    {
        public const int VSCompletionItemKindOffset = 118115;

        public const string ProjectConfigurationFile = "project.razor.json";

        public const string RazorSemanticTokensLegendEndpoint = "_vs_/textDocument/semanticTokensLegend";

        public const string RazorSemanticTokensEditEndpoint = "textDocument/semanticTokens/full/delta";

        public const string RazorSemanticTokensEndpoint = "textDocument/semanticTokens/full";

        public const string SemanticTokensProviderName = "semanticTokensProvider";

        public const string RazorLanguageQueryEndpoint = "razor/languageQuery";

        public const string RazorMonitorProjectConfigurationFilePathEndpoint = "razor/monitorProjectConfigurationFilePath";

        public const string RazorMapToDocumentRangesEndpoint = "razor/mapToDocumentRanges";

        public const string RazorTranslateDiagnosticsEndpoint = "razor/translateDiagnostics";

        public const string RazorMapToDocumentEditsEndpoint = "razor/mapToDocumentEdits";

        public const string RazorCodeActionRunnerCommand = "razor/runCodeAction";

        public const string RazorDocumentFormattingEndpoint = "textDocument/formatting";

        // RZLS Custom Message Targets
        public const string RazorUpdateCSharpBufferEndpoint = "razor/updateCSharpBuffer";

        public const string RazorUpdateHtmlBufferEndpoint = "razor/updateHtmlBuffer";

        public const string RazorRangeFormattingEndpoint = "razor/rangeFormatting";

        public const string RazorProvideCodeActionsEndpoint = "razor/provideCodeActions";

        public const string RazorResolveCodeActionsEndpoint = "razor/resolveCodeActions";

        public const string RazorProvideSemanticTokensRangeEndpoint = "razor/provideSemanticTokensRange";

        public const string RazorServerReadyEndpoint = "razor/serverReady";

        public const string RazorWrapWithTagEndpoint = "razor/wrapWithTag";

        public static class CodeActions
        {
            public const string EditBasedCodeActionCommand = "EditBasedCodeActionCommand";

            public const string ExtractToCodeBehindAction = "ExtractToCodeBehind";

            public const string CreateComponentFromTag = "CreateComponentFromTag";

            public const string AddUsing = "AddUsing";

            public const string CodeActionFromVSCode = "CodeActionFromVSCode";

            /// <summary>
            /// Remaps without formatting the resolved code action edit
            /// </summary>
            public const string UnformattedRemap = "UnformattedRemap";

            /// <summary>
            /// Remaps and formats the resolved code action edit
            /// </summary>
            public const string Default = "Default";

            public static class Languages
            {
                public const string CSharp = "CSharp";

                public const string Razor = "Razor";
            }
        }
    }
}
