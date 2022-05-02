// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(VSInternalMethods.TextDocumentTextPresentationName)]
    internal class TextPresentationHandler : PresentationHandlerBase<VSInternalTextPresentationParams>, IRequestHandler<VSInternalTextPresentationParams, WorkspaceEdit?>
    {
        [ImportingConstructor]
        public TextPresentationHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
            : base(requestInvoker, documentManager, projectionProvider, documentMappingProvider, loggerProvider)
        {
        }

        protected override string MethodName => VSInternalMethods.TextDocumentTextPresentationName;

        protected override TextDocumentIdentifier GetTextDocumentIdentifier(VSInternalTextPresentationParams request) => request.TextDocument;

        protected override Range GetRange(VSInternalTextPresentationParams request) => request.Range;
    }
}
