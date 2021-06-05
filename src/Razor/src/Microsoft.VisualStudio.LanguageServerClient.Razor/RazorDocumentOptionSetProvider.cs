// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal sealed class RazorDocumentOptionSetProvider : IRazorDocumentOptionSetProvider
    {
        private readonly RazorLSPClientOptionsMonitor _optionsMonitor;

        public RazorDocumentOptionSetProvider(RazorLSPClientOptionsMonitor optionsMonitor)
        {
            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            _optionsMonitor = optionsMonitor;
        }

        public async Task<OptionSet> GetDocumentOptionsAsync(Document document, CancellationToken cancellationToken)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            // TO-DO: We should switch to a per-document implementation once Razor starts supporting .editorconfig.
            var editorSettings = _optionsMonitor.EditorSettings;

            var documentOptions = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            documentOptions = documentOptions.WithChangedOption(FormattingOptions.UseTabs, editorSettings.IndentWithTabs);
            documentOptions = documentOptions.WithChangedOption(FormattingOptions.TabSize, editorSettings.IndentSize);
            documentOptions = documentOptions.WithChangedOption(FormattingOptions.IndentationSize, editorSettings.IndentSize);

            return documentOptions;
        }
    }
}
