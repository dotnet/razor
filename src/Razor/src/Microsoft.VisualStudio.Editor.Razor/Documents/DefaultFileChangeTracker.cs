// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

// A noop implementation for non-ide cases
internal class DefaultFileChangeTracker : FileChangeTracker
{
    public override event EventHandler<FileChangeEventArgs>? Changed
    {
        // Do nothing (the handlers would never be used anyway)
        add { }
        remove { }
    }

    public DefaultFileChangeTracker(string filePath)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        FilePath = filePath;
    }

    public override string FilePath { get; }

    public override ValueTask StartListeningAsync(CancellationToken cancellationToken)
    {
        // Do nothing
        return default;
    }

    public override ValueTask StopListeningAsync(CancellationToken cancellationToken)
    {
        // Do nothing
        return default;
    }
}
