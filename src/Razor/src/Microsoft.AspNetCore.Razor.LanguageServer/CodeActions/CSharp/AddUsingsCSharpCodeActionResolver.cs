// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    /// <summary>
    /// Resolves the C# Add Using Code Action by requesting edits from Roslyn
    /// and converting them to be Razor compatible.
    /// </summary>
    internal class AddUsingsCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;

        public AddUsingsCSharpCodeActionResolver(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache)
            : base(languageServer)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _documentVersionCache = documentVersionCache ?? throw new ArgumentNullException(nameof(documentVersionCache));
        }

        public override string Action => LanguageServerConstants.CodeActions.AddUsing;

        public async override Task<CodeAction?> ResolveAsync(
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

            var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(csharpParams.RazorFileUri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);
            if (documentSnapshot is null)
            {
                return codeAction;
            }

            var text = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (text is null)
            {
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var documentVersion = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version);
                return version;
            }, cancellationToken).ConfigureAwait(false);

            var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier()
            {
                Uri = csharpParams.RazorFileUri,
                Version = documentVersion.Value
            };

            var edit = AddUsingsCodeActionResolver.CreateAddUsingWorkspaceEdit(@namespace, codeDocument, codeDocumentIdentifier);
            codeAction = codeAction with { Edit = edit };

            return codeAction;
        }
    }
}
