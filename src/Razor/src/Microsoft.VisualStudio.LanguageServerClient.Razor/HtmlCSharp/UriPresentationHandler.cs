// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(VSInternalMethods.TextDocumentUriPresentationName)]
    internal class UriPresentationHandler : PresentationHandlerBase<VSInternalUriPresentationParams>, IRequestHandler<VSInternalUriPresentationParams, WorkspaceEdit?>
    {
        [ImportingConstructor]
        public UriPresentationHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
            : base(requestInvoker, documentManager, projectionProvider, documentMappingProvider, loggerProvider)
        {
        }

        protected override string MethodName => VSInternalMethods.TextDocumentUriPresentationName;

        protected override TextDocumentIdentifier GetTextDocumentIdentifier(VSInternalUriPresentationParams request) => request.TextDocument;

        protected override Range GetRange(VSInternalUriPresentationParams request) => request.Range;
    }
}
