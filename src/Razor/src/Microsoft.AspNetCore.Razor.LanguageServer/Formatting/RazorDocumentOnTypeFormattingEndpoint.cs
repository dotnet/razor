// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal class RazorDocumentOnTypeFormattingEndpoint : IVSDocumentOnTypeFormattingEndpoint
{
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly DocumentResolver _documentResolver;
    private readonly RazorFormattingService _razorFormattingService;
    private readonly RazorDocumentMappingService _razorDocumentMappingService;
    private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
    private readonly ILogger _logger;

    private static readonly IReadOnlyList<string> s_csharpTriggerCharacters = new[] { "}", ";" };
    private static readonly IReadOnlyList<string> s_htmlTriggerCharacters = new[] { "\n", "{", "}", ";" };
    private static readonly IReadOnlyList<string> s_allTriggerCharacters = s_csharpTriggerCharacters.Concat(s_htmlTriggerCharacters).ToArray();

    public RazorDocumentOnTypeFormattingEndpoint(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        DocumentResolver documentResolver,
        RazorFormattingService razorFormattingService,
        RazorDocumentMappingService razorDocumentMappingService,
        IOptionsMonitor<RazorLSPOptions> optionsMonitor,
        ILoggerFactory loggerFactory)
    {
        if (projectSnapshotManagerDispatcher is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
        }

        if (documentResolver is null)
        {
            throw new ArgumentNullException(nameof(documentResolver));
        }

        if (razorFormattingService is null)
        {
            throw new ArgumentNullException(nameof(razorFormattingService));
        }

        if (razorDocumentMappingService is null)
        {
            throw new ArgumentNullException(nameof(razorDocumentMappingService));
        }

        if (optionsMonitor is null)
        {
            throw new ArgumentNullException(nameof(optionsMonitor));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _documentResolver = documentResolver;
        _razorFormattingService = razorFormattingService;
        _razorDocumentMappingService = razorDocumentMappingService;
        _optionsMonitor = optionsMonitor;
        _logger = loggerFactory.CreateLogger<RazorDocumentOnTypeFormattingEndpoint>();
    }

    public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        const string ServerCapability = "documentOnTypeFormattingProvider";

        return new RegistrationExtensionResult(ServerCapability,
            new DocumentOnTypeFormattingOptions
            {
                FirstTriggerCharacter = s_allTriggerCharacters[0],
                MoreTriggerCharacter = s_allTriggerCharacters.Skip(1).ToArray(),
            });
    }

    public async Task<TextEdit[]?> Handle(DocumentOnTypeFormattingParamsBridge request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OnTypeFormatting request for {requestTextDocumentUri}.", request.TextDocument.Uri);

        if (!_optionsMonitor.CurrentValue.EnableFormatting)
        {
            _logger.LogInformation("Formatting option disabled.");
            return null;
        }

        if (!s_allTriggerCharacters.Contains(request.Character, StringComparer.Ordinal))
        {
            _logger.LogWarning("Unexpected trigger character '{requestCharacter}'.", request.Character);
            return null;
        }

        var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
        {
            _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

            return documentSnapshot;
        }, cancellationToken).ConfigureAwait(false);

        if (documentSnapshot is null)
        {
            _logger.LogWarning("Failed to find document {requestTextDocumentUri}.", request.TextDocument.Uri);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
        if (codeDocument.IsUnsupported())
        {
            _logger.LogWarning("Failed to retrieve generated output for document {requestTextDocumentUri}.", request.TextDocument.Uri);
            return null;
        }

        var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
        if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
        {
            return null;
        }

        var triggerCharacterKind = _razorDocumentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex, rightAssociative: false);
        if (triggerCharacterKind is not (RazorLanguageKind.CSharp or RazorLanguageKind.Html))
        {
            _logger.LogInformation("Unsupported trigger character language {triggerCharacterKind:G}.", triggerCharacterKind);
            return null;
        }

        if (!IsApplicableTriggerCharacter(request.Character, triggerCharacterKind))
        {
            // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
            _logger.LogInformation("Unsupported trigger character location.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        Debug.Assert(request.Character.Length > 0);

        var formattedEdits = await _razorFormattingService.FormatOnTypeAsync(request.TextDocument.Uri, documentSnapshot, triggerCharacterKind, Array.Empty<TextEdit>(), request.Options, hostDocumentIndex, request.Character[0], cancellationToken).ConfigureAwait(false);
        if (formattedEdits.Length == 0)
        {
            _logger.LogInformation("No formatting changes were necessary");
            return null;
        }

        _logger.LogInformation("Returning {formattingEditsLength} final formatted results.", formattedEdits.Length);
        return formattedEdits;
    }

    private static bool IsApplicableTriggerCharacter(string triggerCharacter, RazorLanguageKind languageKind)
    {
        if (languageKind == RazorLanguageKind.CSharp)
        {
            return s_csharpTriggerCharacters.Contains(triggerCharacter);
        }
        else if (languageKind == RazorLanguageKind.Html)
        {
            return s_htmlTriggerCharacters.Contains(triggerCharacter);
        }

        // Unknown trigger character.
        return false;
    }
}
