// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;

[Export(typeof(IRazorDocumentMappingService)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteDocumentMappingService(
    IFilePathService filePathService,
    ILoggerFactory loggerFactory)
    : AbstractRazorDocumentMappingService(filePathService, loggerFactory.GetOrCreateLogger<RemoteDocumentMappingService>())
{
    protected override ValueTask<RazorCodeDocument?> TryGetCodeDocumentAsync(Uri razorDocumentUri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
