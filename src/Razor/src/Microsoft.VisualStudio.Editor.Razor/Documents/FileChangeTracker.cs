// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

internal abstract class FileChangeTracker
{
    public abstract event EventHandler<FileChangeEventArgs>? Changed;

    public abstract string FilePath { get; }

    public abstract ValueTask StartListeningAsync(CancellationToken cancellationToken);

    public abstract ValueTask StopListeningAsync(CancellationToken cancellationToken);
}
