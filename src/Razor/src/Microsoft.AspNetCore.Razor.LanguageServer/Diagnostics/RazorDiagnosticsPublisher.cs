// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal partial class RazorDiagnosticsPublisher : IDocumentProcessedListener, IDisposable
{
    private static readonly TimeSpan s_publishDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_clearClosedDocumentsDelay = TimeSpan.FromSeconds(5);

    private readonly IProjectSnapshotManager _projectManager;
    private readonly IClientConnection _clientConnection;
    private readonly ILogger _logger;
    private readonly LanguageServerFeatureOptions _options;
    private readonly Lazy<RazorTranslateDiagnosticsService> _translateDiagnosticsService;
    private readonly Lazy<IDocumentContextFactory> _documentContextFactory;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<IDocumentSnapshot> _workQueue;

    private readonly object _publishedDiagnosticsGate = new();
    private readonly Dictionary<string, IReadOnlyList<RazorDiagnostic>> _publishedRazorDiagnostics;
    private readonly Dictionary<string, IReadOnlyList<Diagnostic>> _publishedCSharpDiagnostics;

    private readonly object _documentClosedGate = new();
    private Task _clearClosedDocumentsTask = Task.CompletedTask;
    private bool _waitingToClearClosedDocuments;

    public RazorDiagnosticsPublisher(
        IProjectSnapshotManager projectManager,
        IClientConnection clientConnection,
        LanguageServerFeatureOptions options,
        Lazy<RazorTranslateDiagnosticsService> translateDiagnosticsService,
        Lazy<IDocumentContextFactory> documentContextFactory,
        ILoggerFactory loggerFactory)
        : this(projectManager, clientConnection, options,
              translateDiagnosticsService, documentContextFactory,
               loggerFactory, s_publishDelay)
    {
    }

    // Present for test to specify publish delay
    protected RazorDiagnosticsPublisher(
        IProjectSnapshotManager projectManager,
        IClientConnection clientConnection,
        LanguageServerFeatureOptions options,
        Lazy<RazorTranslateDiagnosticsService> translateDiagnosticsService,
        Lazy<IDocumentContextFactory> documentContextFactory,
        ILoggerFactory loggerFactory,
        TimeSpan publishDelay)
    {
        _projectManager = projectManager;
        _clientConnection = clientConnection;
        _options = options;
        _translateDiagnosticsService = translateDiagnosticsService;
        _documentContextFactory = documentContextFactory;

        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<IDocumentSnapshot>(publishDelay, ProcessBatchAsync, _disposeTokenSource.Token);

        _publishedRazorDiagnostics = new Dictionary<string, IReadOnlyList<RazorDiagnostic>>(FilePathComparer.Instance);
        _publishedCSharpDiagnostics = new Dictionary<string, IReadOnlyList<Diagnostic>>(FilePathComparer.Instance);
        _logger = loggerFactory.GetOrCreateLogger<RazorDiagnosticsPublisher>();
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    public void DocumentProcessed(RazorCodeDocument codeDocument, IDocumentSnapshot document)
    {
        _workQueue.AddWork(document);

        StartDelayToClearDocuments();
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<IDocumentSnapshot> items, CancellationToken token)
    {
        foreach (var document in items.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await PublishDiagnosticsAsync(document, token).ConfigureAwait(false);
        }
    }

    private async Task PublishDiagnosticsAsync(IDocumentSnapshot document, CancellationToken token)
    {
        var result = await document.GetGeneratedOutputAsync().ConfigureAwait(false);

        Diagnostic[]? csharpDiagnostics = null;
        if (_options.DelegateToCSharpOnDiagnosticPublish)
        {
            var uriBuilder = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = document.FilePath,
                Host = string.Empty,
            };

            var delegatedParams = new DocumentDiagnosticParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uriBuilder.Uri },
            };

            var delegatedResponse = await _clientConnection.SendRequestAsync<DocumentDiagnosticParams, SumType<FullDocumentDiagnosticReport, UnchangedDocumentDiagnosticReport>?>(
                CustomMessageNames.RazorCSharpPullDiagnosticsEndpointName,
                delegatedParams,
                token).ConfigureAwait(false);

            if (delegatedResponse.HasValue &&
                delegatedResponse.Value.TryGetFirst(out var fullDiagnostics) &&
                fullDiagnostics.Items is not null)
            {
                var documentContext = await _documentContextFactory.Value
                    .TryCreateAsync(delegatedParams.TextDocument.Uri, projectContext: null, token)
                    .ConfigureAwait(false);

                if (documentContext is not null)
                {
                    csharpDiagnostics = await _translateDiagnosticsService.Value
                        .TranslateAsync(RazorLanguageKind.CSharp, fullDiagnostics.Items, documentContext, token)
                        .ConfigureAwait(false);
                }
            }
        }

        var razorDiagnostics = result.GetCSharpDocument().Diagnostics;

        lock (_publishedDiagnosticsGate)
        {
            var filePath = document.FilePath.AssumeNotNull();

            if (_publishedRazorDiagnostics.TryGetValue(filePath, out var previousRazorDiagnostics) && razorDiagnostics.SequenceEqual(previousRazorDiagnostics)
                && (csharpDiagnostics == null || (_publishedCSharpDiagnostics.TryGetValue(filePath, out var previousCsharpDiagnostics) && csharpDiagnostics.SequenceEqual(previousCsharpDiagnostics))))
            {
                // Diagnostics are the same as last publish
                return;
            }

            _publishedRazorDiagnostics[filePath] = razorDiagnostics;
            if (csharpDiagnostics != null)
            {
                _publishedCSharpDiagnostics[filePath] = csharpDiagnostics;
            }
        }

        if (!document.TryGetText(out var sourceText))
        {
            Debug.Fail("Document source text should already be available.");
            return;
        }

        var convertedDiagnostics = razorDiagnostics.Select(razorDiagnostic => RazorDiagnosticConverter.Convert(razorDiagnostic, sourceText, document));
        var combinedDiagnostics = csharpDiagnostics == null ? convertedDiagnostics : convertedDiagnostics.Concat(csharpDiagnostics);
        PublishDiagnosticsForFilePath(document.FilePath, combinedDiagnostics);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            var diagnosticString = string.Join(", ", razorDiagnostics.Select(diagnostic => diagnostic.Id));
            _logger.LogTrace($"Publishing diagnostics for document '{document.FilePath}': {diagnosticString}");
        }
    }

    private void StartDelayToClearDocuments()
    {
        lock (_documentClosedGate)
        {
            if (_waitingToClearClosedDocuments)
            {
                return;
            }

            _clearClosedDocumentsTask = ClearClosedDocumentsAfterDelayAsync();
            _waitingToClearClosedDocuments = true;
        }

        async Task ClearClosedDocumentsAfterDelayAsync()
        {
            await Task.Delay(s_clearClosedDocumentsDelay, _disposeTokenSource.Token).ConfigureAwait(false);

            ClearClosedDocuments();

            lock (_documentClosedGate)
            {
                _waitingToClearClosedDocuments = false;
            }
        }
    }

    private void ClearClosedDocuments()
    {
        lock (_publishedDiagnosticsGate)
        {
            ClearClosedDocumentsPublishedDiagnostics(_publishedRazorDiagnostics);
            ClearClosedDocumentsPublishedDiagnostics(_publishedCSharpDiagnostics);

            if (_publishedRazorDiagnostics.Count > 0 || _publishedCSharpDiagnostics.Count > 0)
            {
                // There's no way for us to know when a document is closed at this layer. Therefore, we need to poll every X seconds
                // and check if the currently tracked documents are closed. In practice this work is super minimal.
                StartDelayToClearDocuments();
            }

            void ClearClosedDocumentsPublishedDiagnostics<T>(Dictionary<string, IReadOnlyList<T>> publishedDiagnostics) where T : class
            {
                using var documentsToRemove = new PooledArrayBuilder<(string key, bool publishEmptyDiagnostics)>(capacity: publishedDiagnostics.Count);

                foreach (var (key, value) in publishedDiagnostics)
                {
                    if (!_projectManager.IsDocumentOpen(key))
                    {
                        // If there were previously published diagnostics for this document, take note so
                        // we can publish an empty set of diagnostics.
                        documentsToRemove.Add((key, publishEmptyDiagnostics: value.Count > 0));
                    }
                }

                if (documentsToRemove.Count == 0)
                {
                    return;
                }

                foreach (var (key, publishEmptyDiagnostics) in documentsToRemove)
                {
                    publishedDiagnostics.Remove(key);

                    if (publishEmptyDiagnostics)
                    {
                        PublishDiagnosticsForFilePath(key, []);
                    }
                }
            }
        }
    }

    private void PublishDiagnosticsForFilePath(string filePath, IEnumerable<Diagnostic> diagnostics)
    {
        var uriBuilder = new UriBuilder()
        {
            Scheme = Uri.UriSchemeFile,
            Path = filePath,
            Host = string.Empty,
        };

        _clientConnection
            .SendNotificationAsync(
                Methods.TextDocumentPublishDiagnosticsName,
                new PublishDiagnosticParams()
                {
                    Uri = uriBuilder.Uri,
                    Diagnostics = diagnostics.ToArray(),
                },
                _disposeTokenSource.Token)
            .Forget();
    }
}
