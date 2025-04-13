// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Shared]
[Export(typeof(IRemoteServiceInvoker))]
internal class VSCodeRemoteServiceInvoker : IRemoteServiceInvoker
{
    public ValueTask<TResult?> TryInvokeAsync<TService, TResult>(Solution solution, Func<TService, RazorPinnedSolutionInfoWrapper, CancellationToken, ValueTask<TResult>> invocation, CancellationToken cancellationToken, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null) where TService : class
    {
        // TODO:
        throw new NotImplementedException();
    }
}
