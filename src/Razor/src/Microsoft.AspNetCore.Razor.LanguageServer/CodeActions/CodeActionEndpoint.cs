// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using LanguageServerInstance = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionEndpoint : ICodeActionHandler
    {
        private readonly IEnumerable<RazorCodeActionProvider> _providers;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILanguageServer _languageServer;

        private CodeActionCapability _capability;

        private bool? _supportsCodeActionResolve = null;

        public CodeActionEndpoint(
            IEnumerable<RazorCodeActionProvider> providers,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILanguageServer languageServer)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _foregroundDispatcher = foregroundDispatcher ?? throw new ArgumentNullException(nameof(foregroundDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        }

        private bool SupportsCodeActionResolve
        {
            get
            {
                if (_supportsCodeActionResolve is null)
                {
                    var languageServerInstance = _languageServer as LanguageServerInstance;
                    var extendableClientCapabilities = languageServerInstance?.ClientSettings?.Capabilities as ExtendableClientCapabilities;
                    _supportsCodeActionResolve = extendableClientCapabilities?.SupportsCodeActionResolve ?? false;
                }

                return _supportsCodeActionResolve.Value;
            }
        }

        public CodeActionRegistrationOptions GetRegistrationOptions()
        {
            return new CodeActionRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
                CodeActionKinds = new[] {
                    CodeActionKind.RefactorExtract,
                    CodeActionKind.QuickFix,
                    CodeActionKind.Refactor
                }
            };
        }

        public void SetCapability(CodeActionCapability capability)
        {
            _capability = capability;
        }

        public async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentSnapshot = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            var linePosition = new LinePosition((int)request.Range.Start.Line, (int)request.Range.Start.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Range.Start.Line, (int)request.Range.Start.Character);

            var context = new RazorCodeActionContext(request, documentSnapshot, codeDocument, location);
            var tasks = new List<Task<CommandOrCodeActionContainer>>();

            foreach (var provider in _providers)
            {
                var result = provider.ProvideAsync(context, cancellationToken);
                if (result != null)
                {
                    tasks.Add(result);
                }
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var container = new List<CommandOrCodeAction>();
            foreach (var result in results)
            {
                if (result != null)
                {
                    foreach (var commandOrCodeAction in result)
                    {
                        container.Add(commandOrCodeAction);
                    }
                }
            }

            return new CommandOrCodeActionContainer(container);
        }

            return razorCodeActions;
        }
    }
}
