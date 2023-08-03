// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithTag;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using ImplementationResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.Location[],
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalReferenceItem[]>;
using SemanticTokensRangeParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensRangeParams;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

[Export(typeof(RazorLanguageServerCustomMessageTarget))]
internal partial class DefaultRazorLanguageServerCustomMessageTarget : RazorLanguageServerCustomMessageTarget
{
    private readonly TrackingLSPDocumentManager _documentManager;
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly LSPRequestInvoker _requestInvoker;
    private readonly ITelemetryReporter _telemetryReporter;
    private readonly FormattingOptionsProvider _formattingOptionsProvider;
    private readonly IClientSettingsManager _editorSettingsManager;
    private readonly LSPDocumentSynchronizer _documentSynchronizer;
    private readonly IOutputWindowLogger? _outputWindowLogger;

    [ImportingConstructor]
    public DefaultRazorLanguageServerCustomMessageTarget(
        LSPDocumentManager documentManager,
        JoinableTaskContext joinableTaskContext,
        LSPRequestInvoker requestInvoker,
        FormattingOptionsProvider formattingOptionsProvider,
        IClientSettingsManager editorSettingsManager,
        LSPDocumentSynchronizer documentSynchronizer,
        ITelemetryReporter telemetryReporter,
        [Import(AllowDefault = true)] IOutputWindowLogger? outputWindowLogger)
    {
        if (documentManager is null)
        {
            throw new ArgumentNullException(nameof(documentManager));
        }

        if (joinableTaskContext is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        if (requestInvoker is null)
        {
            throw new ArgumentNullException(nameof(requestInvoker));
        }

        if (formattingOptionsProvider is null)
        {
            throw new ArgumentNullException(nameof(formattingOptionsProvider));
        }

        if (editorSettingsManager is null)
        {
            throw new ArgumentNullException(nameof(editorSettingsManager));
        }

        if (documentSynchronizer is null)
        {
            throw new ArgumentNullException(nameof(documentSynchronizer));
        }

        _documentManager = (TrackingLSPDocumentManager)documentManager;

        if (_documentManager is null)
        {
            throw new ArgumentException("The LSP document manager should be of type " + typeof(TrackingLSPDocumentManager).FullName, nameof(_documentManager));
        }

        if (telemetryReporter is null)
        {
            throw new ArgumentNullException(nameof(telemetryReporter));
        }

        _joinableTaskFactory = joinableTaskContext.Factory;

        _requestInvoker = requestInvoker;
        _formattingOptionsProvider = formattingOptionsProvider;
        _editorSettingsManager = editorSettingsManager;
        _documentSynchronizer = documentSynchronizer;
        _telemetryReporter = telemetryReporter;
        _outputWindowLogger = outputWindowLogger;
    }

    // Testing constructor
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal DefaultRazorLanguageServerCustomMessageTarget(TrackingLSPDocumentManager documentManager,
        LSPDocumentSynchronizer documentSynchronizer)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        _documentManager = documentManager;
        _documentSynchronizer = documentSynchronizer;
    }

    public override async Task<SumType<DocumentSymbol[], SymbolInformation[]>?> DocumentSymbolsAsync(DelegatedDocumentSymbolParams request, CancellationToken cancellationToken)
    {
        var hostDocument = request.Identifier.TextDocumentIdentifier;
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager,
            request.Identifier.Version,
            hostDocument,
            cancellationToken).ConfigureAwait(false);

        if (!synchronized)
        {
            return null;
        }

        var documentSymbolParams = new DocumentSymbolParams()
        {
            TextDocument = hostDocument.WithUri(virtualDocument.Uri)
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentSymbolParams, SumType<DocumentSymbol[], SymbolInformation[]>?>(
            virtualDocument.Snapshot.TextBuffer,
            Methods.TextDocumentDocumentSymbolName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            documentSymbolParams,
            cancellationToken).ConfigureAwait(false);

        return response?.Response;
    }

    private async Task<DelegationRequestDetails?> GetProjectedRequestDetailsAsync(IDelegatedParams request, CancellationToken cancellationToken)
    {
        string languageServerName;

        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken,
                rejectOnNewerParallelRequest: false);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken,
                rejectOnNewerParallelRequest: false);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
            return null;
        }

        if (!synchronized)
        {
            return null;
        }

        return new DelegationRequestDetails(languageServerName, virtualDocumentSnapshot.Uri, virtualDocumentSnapshot.Snapshot.TextBuffer);
    }

    private record struct DelegationRequestDetails(string LanguageServerName, Uri ProjectedUri, ITextBuffer TextBuffer);
}
