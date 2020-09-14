// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class AddUsingsCodeActionResolver : RazorCodeActionResolver
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public AddUsingsCodeActionResolver(ForegroundDispatcher foregroundDispatcher, DocumentResolver documentResolver)
        {
            _foregroundDispatcher = foregroundDispatcher ?? throw new ArgumentNullException(nameof(foregroundDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
        }

        public override string Action => LanguageServerConstants.CodeActions.AddUsing;

        public override async Task<WorkspaceEdit> ResolveAsync(JObject data, CancellationToken cancellationToken)
        {
            if (data is null)
            {
                return null;
            }

            var actionParams = data.ToObject<AddUsingsCodeActionParams>();
            if (actionParams is null)
            {
                return null;
            }

            var path = actionParams.Uri.GetAbsoluteOrUNCPath();

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(path, out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);
            if (document is null)
            {
                return null;
            }

            var text = await document.GetTextAsync().ConfigureAwait(false);
            if (text is null)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return null;
            }

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier() { Uri = actionParams.Uri };
            return AddUsingsCodeActionHelper.CreateAddUsingWorkspaceEdit(actionParams.Namespace, codeDocument, codeDocumentIdentifier);
        }
    }
}
