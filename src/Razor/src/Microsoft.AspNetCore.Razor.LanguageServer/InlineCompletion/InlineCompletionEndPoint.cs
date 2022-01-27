// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
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
    private readonly AdhocWorkspaceFactory _adhocWorkspaceFactory;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public InlineCompletionEndpoint(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        DocumentResolver documentResolver,
        RazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase languageServer,
        AdhocWorkspaceFactory adhocWorkspaceFactory,
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

        if (adhocWorkspaceFactory is null)
        {
            throw new ArgumentNullException(nameof(adhocWorkspaceFactory));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _documentResolver = documentResolver;
        _documentMappingService = documentMappingService;
        _languageServer = languageServer;
        _adhocWorkspaceFactory = adhocWorkspaceFactory;
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
        var csharpDocOptions = codeDocument.GetCSharpDocument();
        foreach (var item in list.Items)
        {
            var containsSnippet = item.TextFormat == InsertTextFormat.Snippet;
            var range = item.Range ?? new Range { Start = projectedPosition, End = projectedPosition };

            if (!_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, range, out var rangeInRazorDoc))
            {
                _logger.LogWarning($"Could not remap projected range {range} to razor document");
                continue;
            }

            using var formattingContext = FormattingContext.Create(request.TextDocument.Uri, document, codeDocument, s_defaultFormattingOptions, _adhocWorkspaceFactory, isFormatOnType: true, automaticallyAddUsings: false);
            if (!TryGetSnippetWithAdjustedIndentation(formattingContext, item.Text, hostDocumentIndex, out var newSnippetText))
            {
                continue;
            }

            var remappedItem = new InlineCompletionItem
            {
                Command = item.Command,
                Range = rangeInRazorDoc,
                Text = newSnippetText.ToString(),
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

    private static bool TryGetSnippetWithAdjustedIndentation(FormattingContext formattingContext, string snippetText, int hostDocumentIndex, [NotNullWhen(true)] out string? newSnippetText)
    {
        newSnippetText = null;
        if (!formattingContext.TryGetFormattingSpan(hostDocumentIndex, out var formattingSpan))
        {
            return false;
        }

        // Take the amount of indentation razor and html are adding, then remove the amount of C# indentation that is 'hidden'.
        // This should give us the desired base indentation that must be applied to each line.
        var razorAndHtmlContributionsToIndentation = formattingSpan.RazorIndentationLevel + formattingSpan.HtmlIndentationLevel;
        var amountToAddToCSharpIndentation = razorAndHtmlContributionsToIndentation - formattingSpan.MinCSharpIndentLevel;

        var snippetSourceText = SourceText.From(snippetText);
        List<TextChange> indentationChanges = new();
        // Adjust each line, skipping the first since it must start at the snippet keyword.
        foreach (var line in snippetSourceText.Lines.Skip(1))
        {
            var text = snippetSourceText.GetSubText(line.Span).ToString();
            if (string.IsNullOrEmpty(text))
            {
                // We just have an empty line, nothing to do.
                continue;
            }

            // Get the indentation of the line in the C# document based on what options the C# document was generated with.
            var csharpLineIndentationSize = line.GetIndentationSize(formattingContext.Options.TabSize);
            var csharpIndentationLevel = csharpLineIndentationSize / formattingContext.Options.TabSize;

            // Get the new indentation level based on the context in the razor document.
            var newIndentationLevel = csharpIndentationLevel + amountToAddToCSharpIndentation;
            var newIndentationString = formattingContext.GetIndentationLevelString(newIndentationLevel);

            // Replace the current indentation with the new indentation.
            var spanToReplace = new TextSpan(line.Start, line.GetFirstNonWhitespaceOffset() ?? line.Span.End);
            var textChange = new TextChange(spanToReplace, newIndentationString);
            indentationChanges.Add(textChange);
        }

        var newSnippetSourceText = snippetSourceText.WithChanges(indentationChanges);
        newSnippetText = newSnippetSourceText.ToString();
        return true;
    }
}

