// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal abstract class DocumentContextFactory
{
    public abstract Task<DocumentContext?> TryCreateAsync(Uri documentUri, VSProjectContext? projectContext, CancellationToken cancellationToken);

    public abstract Task<VersionedDocumentContext?> TryCreateForOpenDocumentAsync(Uri documentUri, VSProjectContext? projectContext, CancellationToken cancellationToken);
}
