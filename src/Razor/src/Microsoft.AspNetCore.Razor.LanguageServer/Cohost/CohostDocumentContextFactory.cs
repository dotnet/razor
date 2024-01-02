// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Cohost;

// NOTE: This is not a "normal" MEF export (ie, exporting an interface) purely because of a strange desire to keep API in
//       RazorCohostRequestContextExtensions looking like the previous code in the non-cohost world.
[ExportRazorStatelessLspService(typeof(CohostDocumentContextFactory))]
[method: ImportingConstructor]
internal class CohostDocumentContextFactory(DocumentSnapshotFactory documentSnapshotFactory) : AbstractRazorLspService
{
    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;

    public VersionedDocumentContext Create(Uri documentUri, TextDocument textDocument)
    {
        var documentSnapshot = _documentSnapshotFactory.GetOrCreate(textDocument);

        // TODO: There is no need for this to be "versioned" in cohosting, but easier to fake it for now so that existing
        //       code can be reused.
        return new VersionedDocumentContext(documentUri, documentSnapshot, null, -1337);
    }
}
