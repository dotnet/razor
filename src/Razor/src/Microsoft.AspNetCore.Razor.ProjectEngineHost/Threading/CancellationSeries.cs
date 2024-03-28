﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.AspNetCore.Razor.Threading;

// NOTE: This code is copied from dotnet/roslyn:
// https://github.com/dotnet/roslyn/blob/98cd097bf122677378692ebe952b71ab6e5bb013/src/Workspaces/Core/Portable/Utilities/CancellationSeries.cs
//
// However, it was originally derived from an implementation in dotnet/project-system:
// https://github.com/dotnet/project-system/blob/bdf69d5420ec8d894f5bf4c3d4692900b7f2479c/src/Microsoft.VisualStudio.ProjectSystem.Managed/Threading/Tasks/CancellationSeries.cs

/// <summary>
/// Produces a series of <see cref="CancellationToken"/> objects such that requesting a new token
/// causes the previously issued token to be cancelled.
/// </summary>
/// <remarks>
/// <para>Consuming code is responsible for managing overlapping asynchronous operations.</para>
/// <para>This class has a lock-free implementation to minimise latency and contention.</para>
/// </remarks>
internal sealed class CancellationSeries : IDisposable
{
    private CancellationTokenSource? _cts;

    private readonly CancellationToken _superToken;

    /// <summary>
    /// Initializes a new instance of <see cref="CancellationSeries"/>.
    /// </summary>
    /// <param name="token">An optional cancellation token that, when cancelled, cancels the last
    /// issued token and causes any subsequent tokens to be issued in a cancelled state.</param>
    public CancellationSeries(CancellationToken token = default)
    {
        // Initialize with a pre-cancelled source to ensure HasActiveToken has the correct state
        _cts = new CancellationTokenSource();
        _cts.Cancel();

        _superToken = token;
    }

    /// <summary>
    /// Determines if the cancellation series has an active token which has not been cancelled.
    /// </summary>
    public bool HasActiveToken
        => _cts is { IsCancellationRequested: false };

    /// <summary>
    /// Creates the next <see cref="CancellationToken"/> in the series, ensuring the last issued
    /// token (if any) is cancelled first.
    /// </summary>
    /// <param name="token">An optional cancellation token that, when cancelled, cancels the
    /// returned token.</param>
    /// <returns>
    /// A cancellation token that will be cancelled when either:
    /// <list type="bullet">
    /// <item><see cref="CreateNext"/> is called again</item>
    /// <item>The token passed to this method (if any) is cancelled</item>
    /// <item>The token passed to the constructor (if any) is cancelled</item>
    /// <item><see cref="Dispose"/> is called</item>
    /// </list>
    /// </returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public CancellationToken CreateNext(CancellationToken token = default)
    {
        var nextSource = CancellationTokenSource.CreateLinkedTokenSource(token, _superToken);

        // Obtain the token before exchange, as otherwise the CTS may be cancelled before
        // we request the Token, which will result in an ObjectDisposedException.
        // This way we would return a cancelled token, which is reasonable.
        var nextToken = nextSource.Token;

        // The following block is identical to Interlocked.Exchange, except no replacement is made if the current
        // field value is null (latch on null). This ensures state is not corrupted if CreateNext is called after
        // the object is disposed.
        var priorSource = Volatile.Read(ref _cts);
        while (priorSource is not null)
        {
            var candidate = Interlocked.CompareExchange(ref _cts, nextSource, priorSource);

            if (candidate == priorSource)
            {
                break;
            }
            else
            {
                priorSource = candidate;
            }
        }

        if (priorSource is null)
        {
            nextSource.Dispose();

            throw new ObjectDisposedException(nameof(CancellationSeries));
        }

        try
        {
            priorSource.Cancel();
        }
        finally
        {
            // A registered action on the token may throw, which would surface here.
            // Ensure we always dispose the prior CTS.
            priorSource.Dispose();
        }

        return nextToken;
    }

    public void Dispose()
    {
        var source = Interlocked.Exchange(ref _cts, null);

        if (source is null)
        {
            // Already disposed
            return;
        }

        try
        {
            source.Cancel();
        }
        finally
        {
            source.Dispose();
        }
    }
}
