// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Editor.Razor.Logging;

internal interface IRazorLogHubTraceProvider
{
    Task InitializeTraceAsync(string logIdentifier, int logHubSessionId, CancellationToken cancellationToken);
    TraceSource? TryGetTraceSource();
}
