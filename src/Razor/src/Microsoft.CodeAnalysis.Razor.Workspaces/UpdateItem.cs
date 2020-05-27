// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor
{
    // Internal for testing
    internal class UpdateItem
    {
        public UpdateItem(Task task, CancellationTokenSource cts)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (cts == null)
            {
                throw new ArgumentNullException(nameof(cts));
            }

            Task = task;
            Cts = cts;
        }

        public Task Task { get; }

        public CancellationTokenSource Cts { get; }
    }
}
