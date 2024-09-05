// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToComponentCodeActionProvider(ILoggerFactory loggerFactory, ITelemetryReporter telemetryReporter) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ExtractToComponentCodeActionProvider>();
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        var telemetryDidSucceed = false;
        using var _ = _telemetryReporter.BeginBlock("extractToComponentProvider", Severity.Normal, new Property("didSucceed", telemetryDidSucceed));

        if (!IsValidContext(context))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (!IsValidSelection(context, syntaxTree))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = new ExtractToComponentCodeActionParams
        {
            Uri = context.Request.TextDocument.Uri,
            SelectStart = context.Request.Range.Start,
            SelectEnd = context.Request.Range.End,
            AbsoluteIndex = context.Location.AbsoluteIndex,
            Namespace = @namespace,
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);

        telemetryDidSucceed = true;
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static bool IsValidContext(RazorCodeActionContext context)
    {
        return context is not null &&
               context.SupportsFileCreation &&
               FileKinds.IsComponent(context.CodeDocument.GetFileKind()) &&
               !context.CodeDocument.IsUnsupported() &&
               context.CodeDocument.GetSyntaxTree() is not null;
    }

    private bool IsValidSelection(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            _logger.LogWarning($"Owner should never be null.");
            return false;
        }

        var startElementNode = owner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax);
        return startElementNode is not null && !startElementNode.GetDiagnostics().Any(d => d.Severity == RazorDiagnosticSeverity.Error) && IsInsideMarkupTag(context, owner);
    }

    private static bool IsInsideMarkupTag(RazorCodeActionContext context, SyntaxNode owner)
    {
        // The selection could start either in a MarkupElement or MarkupTagHelperElement.
        // Both of these have the necessary properties to do this check, but the base class MarkupSyntaxNode does not.
        // The workaround for this is to try to find the specific types as ancestors and then do the check.

        MarkupElementSyntax? tryMakeMarkupElement = null;
        MarkupTagHelperElementSyntax? tryMakeMarkupTagHelperElement = null;

        // Basically a custom form of FirstAncestorOrSelf for this specific case
        for (var node = owner; node is not null; node = node.Parent)
        {
            if (tryMakeMarkupElement is null && node is MarkupElementSyntax markupElement)
            {
                tryMakeMarkupElement = markupElement;
            }
            else if (tryMakeMarkupTagHelperElement is null && node is MarkupTagHelperElementSyntax tagHelper)
            {
                tryMakeMarkupTagHelperElement = tagHelper;
            }

            if (tryMakeMarkupElement is not null && tryMakeMarkupTagHelperElement is not null)
            {
                break;
            }
        }

        var isLocationInElementTag = tryMakeMarkupElement is not null &&
                                        (tryMakeMarkupElement.StartTag.Span.Contains(context.Location.AbsoluteIndex) ||
                                        tryMakeMarkupElement.EndTag.Span.Contains(context.Location.AbsoluteIndex));

        var isLocationInTagHelperTag = tryMakeMarkupTagHelperElement is not null &&
                                                (tryMakeMarkupTagHelperElement.StartTag.Span.Contains(context.Location.AbsoluteIndex) ||
                                                tryMakeMarkupTagHelperElement.EndTag.Span.Contains(context.Location.AbsoluteIndex));

        return isLocationInElementTag || isLocationInTagHelperTag;
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);


}
