// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Threading;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem.Sources;

internal sealed class GeneratedOutputSource
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1);

    // Hold the output in a WeakReference to avoid memory leaks in the case of a long-lived
    // document snapshots. In particular, the DynamicFileInfo system results in the Roslyn
    // workspace holding onto document snapshots.
    private WeakReference<RazorCodeDocument>? _weakOutput;

    public GeneratedOutputSource ForkIfOutputAvailable()
        => TryGetValue(out var result)
            ? new() { _weakOutput = new(result) }
            : new();

    public bool TryGetValue([NotNullWhen(true)] out RazorCodeDocument? result)
    {
        var weakOutput = _weakOutput;
        if (weakOutput is null)
        {
            result = null;
            return false;
        }

        return weakOutput.TryGetTarget(out result);
    }

    public ValueTask<RazorCodeDocument> GetValueAsync(DocumentSnapshot document, CancellationToken cancellationToken)
    {
        if (TryGetValue(out var result))
        {
            return new(result);
        }

        return new(GetValueCoreAsync(document, cancellationToken));

        async Task<RazorCodeDocument> GetValueCoreAsync(DocumentSnapshot document, CancellationToken cancellationToken)
        {
            using (await _gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                if (TryGetValue(out result))
                {
                    return result;
                }

                var project = document.Project;
                var projectEngine = project.ProjectEngine;
                var compilerOptions = project.CompilerOptions;

                result = await CompilationHelpers
                    .GenerateCodeDocumentAsync(document, projectEngine, compilerOptions, cancellationToken)
                    .ConfigureAwait(false);

                if (_weakOutput is null)
                {
                    _weakOutput = new(result);
                }
                else
                {
                    _weakOutput.SetTarget(result);
                }

                return result;
            }
        }
    }
}
