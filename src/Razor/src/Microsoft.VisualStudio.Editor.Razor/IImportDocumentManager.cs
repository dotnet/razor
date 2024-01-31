// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.Editor.Razor;

internal interface IImportDocumentManager
{
    void OnSubscribed(VisualStudioDocumentTracker documentTracker);
    void OnUnsubscribed(VisualStudioDocumentTracker documentTracker);

    event EventHandler<ImportChangedEventArgs> Changed;
}
