// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    /// <summary>
    /// Resolves the C# Add Using Code Action by requesting edits from Roslyn
    /// and converting them to be Razor compatible.
    /// </summary>
    internal class AddUsingsCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        private readonly DocumentContextFactory _documentContextFactory;

        public AddUsingsCSharpCodeActionResolver(
            DocumentContextFactory documentContextFactory,
            ClientNotifierServiceBase languageServer)
            : base(languageServer)
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        }

        public override string Action => LanguageServerConstants.CodeActions.AddUsing;

        public async override Task<CodeAction> ResolveAsync(
            CSharpCodeActionParams csharpParams,
            CodeAction codeAction,
            CancellationToken cancellationToken)
        {
            if (csharpParams is null)
            {
                throw new ArgumentNullException(nameof(csharpParams));
            }

            if (codeAction is null)
            {
                throw new ArgumentNullException(nameof(codeAction));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!AddUsingsCodeActionProviderHelper.TryExtractNamespace(codeAction.Title, out var @namespace))
            {
                // Invalid text edit, missing namespace
                return codeAction;
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(csharpParams.RazorFileUri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null || cancellationToken.IsCancellationRequested)
            {
                return codeAction;
            }

            var codeDocument = await documentContext.Snapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return codeAction;
            }

            var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier()
            {
                Uri = csharpParams.RazorFileUri,
                Version = documentContext.Version
            };

            var edit = AddUsingsCodeActionResolver.CreateAddUsingWorkspaceEdit(@namespace, codeDocument, codeDocumentIdentifier);
            codeAction.Edit = edit;

            return codeAction;
        }
    }
}
