// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    internal class OnAutoInsertEndpoint : IVSOnAutoInsertEndpoint
    {
        private readonly AdhocWorkspaceFactory _workspaceFactory;
        private readonly IReadOnlyList<RazorOnAutoInsertProvider> _onAutoInsertProviders;
        private readonly ImmutableHashSet<string> _onAutoInsertTriggerCharacters;
        private readonly DocumentContextFactory _documentContextFactory;

        public OnAutoInsertEndpoint(
            DocumentContextFactory documentContextFactory,
            IEnumerable<RazorOnAutoInsertProvider> onAutoInsertProvider,
            AdhocWorkspaceFactory workspaceFactory)
        {
            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (onAutoInsertProvider is null)
            {
                throw new ArgumentNullException(nameof(onAutoInsertProvider));
            }

            if (workspaceFactory is null)
            {
                throw new ArgumentNullException(nameof(workspaceFactory));
            }

            _documentContextFactory = documentContextFactory;
            _workspaceFactory = workspaceFactory;
            _onAutoInsertProviders = onAutoInsertProvider.ToList();
            _onAutoInsertTriggerCharacters = _onAutoInsertProviders.Select(provider => provider.TriggerCharacter).ToImmutableHashSet();
        }

        public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "_vs_onAutoInsertProvider";

            var registrationOptions = new VSInternalDocumentOnAutoInsertOptions()
            {
                TriggerCharacters = _onAutoInsertTriggerCharacters.ToArray(),
            };

            return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
        }

        public async Task<VSInternalDocumentOnAutoInsertResponseItem?> Handle(OnAutoInsertParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var character = request.Character;

            var applicableProviders = new List<RazorOnAutoInsertProvider>();
            for (var i = 0; i < _onAutoInsertProviders.Count; i++)
            {
                var formatOnTypeProvider = _onAutoInsertProviders[i];
                if (formatOnTypeProvider.TriggerCharacter == character)
                {
                    applicableProviders.Add(formatOnTypeProvider);
                }
            }

            if (applicableProviders.Count == 0)
            {
                // There's currently a bug in the LSP platform where other language clients OnAutoInsert trigger characters influence every language clients trigger characters.
                // To combat this we need to pre-emptively return so we don't try having our providers handle characters that they can't.
                return null;
            }

            var uri = request.TextDocument.Uri;
            var position = request.Position;

            using (var formattingContext = FormattingContext.Create(uri, documentContext.Snapshot, codeDocument, request.Options, _workspaceFactory))
            {
                for (var i = 0; i < applicableProviders.Count; i++)
                {
                    if (applicableProviders[i].TryResolveInsertion(position, formattingContext, out var textEdit, out var format))
                    {
                        return new VSInternalDocumentOnAutoInsertResponseItem()
                        {
                            TextEdit = textEdit,
                            TextEditFormat = format,
                        };
                    }
                }
            }

            // No provider could handle the text edit.
            return null;
        }
    }
}
