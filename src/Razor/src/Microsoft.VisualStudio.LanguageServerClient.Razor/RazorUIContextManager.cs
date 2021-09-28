// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal abstract class RazorUIContextManager
    {
        public abstract Task SetUIContextAsync(Guid guid, bool isActive, CancellationToken cancellationToken);
    }
}
