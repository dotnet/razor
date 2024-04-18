﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal partial class RazorDiagnosticsPublisher
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(RazorDiagnosticsPublisher instance)
    {
        public bool IsWaitingToClearClosedDocuments
        {
            get
            {
                lock (instance._documentClosedGate)
                {
                    return instance._waitingToClearClosedDocuments;
                }
            }
        }

        public async Task WaitForClearClosedDocumentsAsync()
        {
            Task task;

            lock (instance._documentClosedGate)
            {
                task = instance._clearClosedDocumentsTask;
            }

            await task.ConfigureAwait(false);
        }

        public Task WaitForDiagnosticsToPublishAsync()
        {
            return instance._workQueue.WaitUntilCurrentBatchCompletesAsync();
        }

        public void SetPublishedCSharpDiagnostics(string filePath, ImmutableArray<Diagnostic> diagnostics)
        {
            lock (instance._publishedDiagnosticsGate)
            {
                instance._publishedCSharpDiagnostics[filePath] = diagnostics;
            }
        }

        public void SetPublishedRazorDiagnostics(string filePath, ImmutableArray<RazorDiagnostic> diagnostics)
        {
            lock (instance._publishedDiagnosticsGate)
            {
                instance._publishedRazorDiagnostics[filePath] = diagnostics;
            }
        }

        public void ClearClosedDocuments()
        {
            instance.ClearClosedDocuments();
        }

        public Task PublishDiagnosticsAsync(IDocumentSnapshot document, CancellationToken cancellationToken)
        {
            return instance.PublishDiagnosticsAsync(document, cancellationToken);
        }
    }
}