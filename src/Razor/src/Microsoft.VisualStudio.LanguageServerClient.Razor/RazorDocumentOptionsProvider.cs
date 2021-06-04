// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal sealed class RazorDocumentOptionsProvider : IRazorDocumentOptionsProvider
    {
        private readonly RazorLSPClientOptionsMonitor _optionsMonitor;

        public RazorDocumentOptionsProvider(RazorLSPClientOptionsMonitor optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        public Task<RazorDocumentOptions> GetDocumentOptionsAsync(Document document, CancellationToken cancellationToken)
        {
            // TO-DO: We should switch to a per-document implementation once Razor starts supporting .editorconfig.
            var editorSettings = _optionsMonitor.EditorSettings;
            var documentOptions = new RazorDocumentOptions(editorSettings.IndentWithTabs, editorSettings.IndentSize);
            return Task.FromResult(documentOptions);
        }
    }
}
