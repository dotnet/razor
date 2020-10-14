// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public interface IUIContextManager
    {
        Task SetUIContextAsync(Guid guid, bool isActive, CancellationToken cancellationToken);
    }
}
