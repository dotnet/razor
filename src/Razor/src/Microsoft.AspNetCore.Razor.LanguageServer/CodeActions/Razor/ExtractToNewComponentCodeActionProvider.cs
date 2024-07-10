// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToNewComponentCodeActionProvider : IRazorCodeActionProvider
{
    private readonly ILogger _logger;

    public ExtractToNewComponentCodeActionProvider(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.GetOrCreateLogger<ExtractToNewComponentCodeActionProvider>();
    }

    public Task<IReadOnlyList<RazorVSInternalCodeAction>?> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!context.SupportsFileCreation)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!FileKinds.IsComponent(context.CodeDocument.GetFileKind()))
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var owner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, true);
        if (owner is null)
        {
            _logger.LogWarning($"Owner should never be null.");
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var componentNode = owner.FirstAncestorOrSelf<MarkupElementSyntax>();

        // Make sure we've found tag
        if (componentNode is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        // Do not provide code action if the cursor is inside proper html content (i.e. page text)
        if (context.Location.AbsoluteIndex > componentNode.StartTag.Span.End &&
            context.Location.AbsoluteIndex < componentNode.EndTag.SpanStart)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var actionParams = new ExtractToNewComponentCodeActionParams()
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = componentNode.Span.Start,
            ExtractEnd = componentNode.Span.End,
            Namespace = @namespace
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToNewComponent(resolutionParams);
        var codeActions = new List<RazorVSInternalCodeAction> { codeAction };

        return Task.FromResult<IReadOnlyList<RazorVSInternalCodeAction>?>(codeActions);
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);

    //private static bool HasUnsupportedChildren(Language.Syntax.SyntaxNode node)
    //{
    //    return node.DescendantNodes().Any(static n => n is MarkupBlockSyntax or CSharpTransitionSyntax or RazorCommentBlockSyntax);
    //}
}
