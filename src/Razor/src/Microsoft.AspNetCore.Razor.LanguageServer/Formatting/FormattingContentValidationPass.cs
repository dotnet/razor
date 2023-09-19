﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal class FormattingContentValidationPass : FormattingPassBase
{
    private readonly ILogger _logger;

    public FormattingContentValidationPass(
        IRazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase server,
        ILoggerFactory loggerFactory)
        : base(documentMappingService, server)
    {
        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _logger = loggerFactory.CreateLogger<FormattingContentValidationPass>();
    }

    // We want this to run at the very end.
    public override int Order => DefaultOrder + 1000;

    public override bool IsValidationPass => true;

    // Internal for testing.
    internal bool DebugAssertsEnabled { get; set; } = true;

    public override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
    {
        if (result.Kind != RazorLanguageKind.Razor)
        {
            // We don't care about changes to projected documents here.
            return Task.FromResult(result);
        }

        var text = context.SourceText;
        var edits = result.Edits;
        var changes = edits.Select(e => e.ToTextChange(text));
        var changedText = text.WithChanges(changes);

        if (!text.NonWhitespaceContentEquals(changedText))
        {
            // Looks like we removed some non-whitespace content as part of formatting. Oops.
            // Discard this formatting result.

            if (DebugAssertsEnabled)
            {
                Debug.Fail("A formatting result was rejected because it was going to change non-whitespace content in the document.");
            }

            return Task.FromResult(new FormattingResult(Array.Empty<TextEdit>()));
        }

        return Task.FromResult(result);
    }
}
