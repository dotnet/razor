// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class InlineCompletionEndpoint : IInlineCompletionHandler
{
    // Usually when we need to format code, we utilize the formatting options provided
    // by the platform. Similar to DefaultCSharpCodeActionResolver we do not have any, so use defaults.
    private static readonly FormattingOptions s_defaultFormattingOptions = new FormattingOptions()
    {
        TabSize = 4,
        InsertSpaces = true,
        TrimTrailingWhitespace = true,
        InsertFinalNewline = true,
        TrimFinalNewlines = true
    };

    private static readonly ImmutableHashSet<string> s_cSharpKeywords = ImmutableHashSet.Create(
        "~", "Attribute", "checked", "class", "ctor", "cw", "do", "else", "enum", "equals", "Exception", "for", "foreach", "forr",
        "if", "indexer", "interface", "invoke", "iterator", "iterindex", "lock", "mbox", "namespace", "#if", "#region", "prop",
        "propfull", "propg", "sim", "struct", "svm", "switch", "try", "tryf", "unchecked", "unsafe", "using", "while");

    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly DocumentResolver _documentResolver;
    private readonly RazorDocumentMappingService _documentMappingService;
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly RazorFormattingService _formattingService;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public InlineCompletionEndpoint(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        DocumentResolver documentResolver,
        RazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase languageServer,
        RazorFormattingService razorFormattingService,
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

        if (documentMappingService is null)
        {
            throw new ArgumentNullException(nameof(documentMappingService));
        }

        if (languageServer is null)
        {
            throw new ArgumentNullException(nameof(languageServer));
        }

        if (razorFormattingService is null)
        {
            throw new ArgumentNullException(nameof(razorFormattingService));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _documentResolver = documentResolver;
        _documentMappingService = documentMappingService;
        _languageServer = languageServer;
        _formattingService = razorFormattingService;
        _logger = loggerFactory.CreateLogger<InlineCompletionEndpoint>();
    }

    public RegistrationExtensionResult GetRegistration()
    {
        const string AssociatedServerCapability = "_vs_inlineCompletionOptions";

        var registrationOptions = new InlineCompletionOptions()
        {
            DocumentSelector = RazorDefaults.Selector,
            Pattern = string.Join("|", s_cSharpKeywords)
        };

        return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
    }

    public async Task<InlineCompletionList?> Handle(InlineCompletionRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogInformation($"Starting request for {request.TextDocument.Uri} at {request.Position}.");

        var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
        {
            _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

            return documentSnapshot;
        }, cancellationToken).ConfigureAwait(false);

        if (document is null)
        {
            return null;
        }

        var codeDocument = await document.GetGeneratedOutputAsync();
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        var sourceText = await document.GetTextAsync();
        var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
        var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);

        var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex);

        // Map to the location in the C# document.
        if (languageKind != RazorLanguageKind.CSharp ||
            !_documentMappingService.TryMapToProjectedDocumentPosition(codeDocument, hostDocumentIndex, out var projectedPosition, out _))
        {
            _logger.LogInformation($"Unsupported location for {request.TextDocument.Uri}.");
            return null;
        }

        request.Position = projectedPosition;
        var response = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorInlineCompletionEndpoint, request).ConfigureAwait(false);
        var list = await response.Returning<InlineCompletionList>(cancellationToken);
        if (list == null)
        {
            _logger.LogInformation($"Did not get any inline completions from delegation.");
            return null;
        }

        var items = new List<InlineCompletionItem>();
        foreach (var item in list.Items)
        {
            var containsSnippet = item.TextFormat == InsertTextFormat.Snippet;
            var range = item.Range ?? new Range { Start = projectedPosition, End = projectedPosition };

            var textEdit = new TextEdit { NewText = item.Text, Range = range };

            // Remaps the text edits from the generated C# to the razor file,
            // as well as applying appropriate formatting.
            var formattedEdits = await _formattingService.FormatSnippetAsync(
                request.TextDocument.Uri,
                document,
                RazorLanguageKind.CSharp,
                new[] { textEdit },
                s_defaultFormattingOptions,
                cancellationToken);

            if (!formattedEdits.Any())
            {
                _logger.LogInformation("Discarding inline completion item after remapping");
                continue;
            }

            var remappedItem = new InlineCompletionItem
            {
                Command = item.Command,
                Range = formattedEdits.Single().Range,
                Text = formattedEdits.Single().NewText,
                TextFormat = item.TextFormat,
            };
            items.Add(remappedItem);
        }

        _logger.LogInformation($"Returning items.");
        return new InlineCompletionList
        {
            Items = items.ToArray()
        };
    }
}

