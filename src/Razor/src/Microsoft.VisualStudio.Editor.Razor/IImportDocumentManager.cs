// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.VisualStudio.Editor.Razor;

internal interface IImportDocumentManager : ILanguageService
{
    event EventHandler<ImportChangedEventArgs>? Changed;

    ValueTask OnSubscribedAsync(VisualStudioDocumentTracker tracker, CancellationToken cancellationToken);
    ValueTask OnUnsubscribedAsync(VisualStudioDocumentTracker tracker, CancellationToken cancellationToken);
}
