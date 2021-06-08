// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal sealed class RazorDocumentOptionsService : IRazorDocumentOptionsService
    {
        private readonly RazorLSPClientOptionsMonitor _optionsMonitor;

        public RazorDocumentOptionsService(RazorLSPClientOptionsMonitor optionsMonitor)
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

        public Task<IRazorDocumentOptions> GetOptionsForDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            // TO-DO: We should switch to a per-document implementation once Razor starts supporting .editorconfig.
            var editorSettings = _optionsMonitor.EditorSettings;
            return Task.FromResult<IRazorDocumentOptions>(new RazorDocumentOptions(document, editorSettings));
        }

        private sealed class RazorDocumentOptions : IRazorDocumentOptions
        {
            private readonly Document document;
            private readonly EditorSettings editorSettings;

            public RazorDocumentOptions(Document document, EditorSettings editorSettings)
            {
                this.document = document;
                this.editorSettings = editorSettings;
            }

            public bool TryGetDocumentOption(OptionKey option, out object value)
            {
                if (option == new OptionKey(FormattingOptions.UseTabs, document.Project.Language))
                {
                    value = editorSettings.IndentWithTabs;
                    return true;
                }
                else if (option == new OptionKey(FormattingOptions.TabSize, document.Project.Language) ||
                         option == new OptionKey(FormattingOptions.IndentationSize, document.Project.Language))
                {
                    value = editorSettings.IndentSize;
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }
        }
    }
}
