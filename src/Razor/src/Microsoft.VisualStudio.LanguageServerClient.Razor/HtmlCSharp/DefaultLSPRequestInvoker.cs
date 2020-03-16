// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [Export(typeof(LSPRequestInvoker))]
    internal class DefaultLSPRequestInvoker : LSPRequestInvoker
    {
        private readonly ILanguageClientBroker _languageClientBroker;

        [ImportingConstructor]
        public DefaultLSPRequestInvoker(ILanguageClientBroker languageClientBroker)
        {
            if (languageClientBroker is null)
            {
                throw new ArgumentNullException(nameof(languageClientBroker));
            }

            _languageClientBroker = languageClientBroker;
        }

        public override Task<TOut> RequestServerAsync<TIn, TOut>(string method, LanguageServerKind serverKind, TIn parameters, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("message", nameof(method));
            }

            var contentType = RazorLSPContentTypeDefinition.Name;
            if (serverKind == LanguageServerKind.CSharp)
            {
                contentType = CSharpVirtualDocumentFactory.CSharpLSPContentTypeName;
            }
            else if (serverKind == LanguageServerKind.Html)
            {
                contentType = HtmlVirtualDocumentFactory.HtmlLSPContentTypeName;
            }

            return _languageClientBroker.RequestAsync<TIn, TOut>(
                new[] { contentType },
                cap => true,
                method,
                parameters,
                cancellationToken);
        }
    }
}
