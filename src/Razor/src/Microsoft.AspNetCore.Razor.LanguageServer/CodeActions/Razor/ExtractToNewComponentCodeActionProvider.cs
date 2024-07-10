// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToNewComponentCodeActionProvider(ILoggerFactory loggerFactory) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ExtractToNewComponentCodeActionProvider>();

    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!context.SupportsFileCreation)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!FileKinds.IsComponent(context.CodeDocument.GetFileKind()))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var owner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            _logger.LogWarning($"Owner should never be null.");
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var componentNode = owner.FirstAncestorOrSelf<MarkupElementSyntax>();

        var selectionStart = context.Request.Range.Start;
        var selectionEnd = context.Request.Range.End;

        // If user selects range from end to beginning (i.e., bottom-to-top or right-to-left), get the effective start and end.
        if (selectionEnd.Line < selectionStart.Line ||
           (selectionEnd.Line == selectionStart.Line && selectionEnd.Character < selectionStart.Character))
        {
            (selectionEnd, selectionStart) = (selectionStart, selectionEnd);
        }

        var selectionEndIndex = new SourceLocation(0, 0, 0);
        var endOwner = owner;
        var endComponentNode = componentNode;

        var isSelection = selectionStart != selectionEnd;

        if (isSelection)
        {
            if (!selectionEnd.TryGetSourceLocation(context.CodeDocument.GetSourceText(), _logger, out var location))
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }
            // Print selectionEndIndex to see if it is correct
            if (location is null)
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }

            selectionEndIndex = location.Value;
            endOwner = syntaxTree.Root.FindInnermostNode(selectionEndIndex.AbsoluteIndex, true);

            if (endOwner is null)
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }

            endComponentNode = endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();
        }

        // Make sure we've found tag
        if (componentNode is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Do not provide code action if the cursor is inside proper html content (i.e. page text)
        if (context.Location.AbsoluteIndex > componentNode.StartTag.Span.End &&
            context.Location.AbsoluteIndex < componentNode.EndTag.SpanStart)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = new ExtractToNewComponentCodeActionParams()
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = componentNode.Span.Start,
            ExtractEnd = componentNode.Span.End,
            Namespace = @namespace
        };

        if (isSelection && endComponentNode is not null)
        {
            actionParams.ExtractEnd = endComponentNode.Span.End;
        }

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToNewComponent(resolutionParams);

        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);
}
