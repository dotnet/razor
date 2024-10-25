// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal sealed class CodeActionsService(
    IDocumentMappingService documentMappingService,
    IEnumerable<IRazorCodeActionProvider> razorCodeActionProviders,
    IEnumerable<ICSharpCodeActionProvider> csharpCodeActionProviders,
    IEnumerable<IHtmlCodeActionProvider> htmlCodeActionProviders,
    IDelegatedCodeActionsProvider delegatedCodeActionsProvider,
    LanguageServerFeatureOptions languageServerFeatureOptions) : ICodeActionsService
{
    private static readonly ImmutableHashSet<string> s_allAvailableCodeActionNames = GetAllAvailableCodeActionNames();

    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IEnumerable<IRazorCodeActionProvider> _razorCodeActionProviders = razorCodeActionProviders;
    private readonly IEnumerable<ICSharpCodeActionProvider> _csharpCodeActionProviders = csharpCodeActionProviders;
    private readonly IEnumerable<IHtmlCodeActionProvider> _htmlCodeActionProviders = htmlCodeActionProviders;
    private readonly IDelegatedCodeActionsProvider _delegatedCodeActionsProvider = delegatedCodeActionsProvider;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

    public async Task<SumType<Command, CodeAction>[]?> GetCodeActionsAsync(VSCodeActionParams request, DocumentContext documentContext, bool supportsCodeActionResolve, Guid correlationId, CancellationToken cancellationToken)
    {
        var razorCodeActionContext = await GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot, supportsCodeActionResolve, cancellationToken).ConfigureAwait(false);
        if (razorCodeActionContext is null)
        {
            return null;
        }

        var razorCodeActions = await GetRazorCodeActionsAsync(razorCodeActionContext, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var delegatedCodeActions = await GetDelegatedCodeActionsAsync(razorCodeActionContext, correlationId, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        using var commandsOrCodeActions = new PooledArrayBuilder<SumType<Command, CodeAction>>();

        // Grouping the code actions causes VS to sort them into groups, rather than just alphabetically sorting them
        // by title. The latter is bad for us because it can put "Remove <div>" at the top in some locales, and our fully
        // qualify component code action at the bottom, depending on the users namespace.
        ConvertCodeActionsToSumType(request.TextDocument, razorCodeActions, "A-Razor");
        ConvertCodeActionsToSumType(request.TextDocument, delegatedCodeActions, "B-Delegated");

        return commandsOrCodeActions.ToArray();

        void ConvertCodeActionsToSumType(VSTextDocumentIdentifier textDocument, ImmutableArray<RazorVSInternalCodeAction> codeActions, string groupName)
        {
            // We must cast the RazorCodeAction into a platform compliant code action
            // For VS (SupportsCodeActionResolve = true) this means just encapsulating the RazorCodeAction in the `CommandOrCodeAction` struct
            // For VS Code (SupportsCodeActionResolve = false) we must convert it into a CodeAction or Command before encapsulating in the `CommandOrCodeAction` struct.
            if (supportsCodeActionResolve)
            {
                foreach (var action in codeActions)
                {
                    // Make sure we honour the grouping that a delegated server may have created
                    action.Group = groupName + (action.Group ?? string.Empty);
                    commandsOrCodeActions.Add(action);
                }
            }
            else
            {
                foreach (var action in codeActions)
                {
                    commandsOrCodeActions.Add(action.AsVSCodeCommandOrCodeAction(textDocument));
                }
            }
        }
    }

    private async Task<RazorCodeActionContext?> GenerateRazorCodeActionContextAsync(
        VSCodeActionParams request,
        IDocumentSnapshot documentSnapshot,
        bool supportsCodeActionResolve,
        CancellationToken cancellationToken)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        var sourceText = codeDocument.Source.Text;

        // VS Provides `CodeActionParams.Context.SelectionRange` in addition to
        // `CodeActionParams.Range`. The `SelectionRange` is relative to where the
        // code action was invoked (ex. line 14, char 3) whereas the `Range` is
        // always at the start of the line (ex. line 14, char 0). We want to utilize
        // the relative positioning to ensure we provide code actions for the appropriate
        // context.
        //
        // Note: VS Code doesn't provide a `SelectionRange`.
        var vsCodeActionContext = request.Context;
        if (vsCodeActionContext.SelectionRange != null)
        {
            request.Range = vsCodeActionContext.SelectionRange;
        }

        if (!sourceText.TryGetAbsoluteIndex(request.Range.Start, out var startLocation))
        {
            return null;
        }

        if (!sourceText.TryGetAbsoluteIndex(request.Range.End, out var endLocation))
        {
            endLocation = startLocation;
        }

        var context = new RazorCodeActionContext(
            request,
            documentSnapshot,
            codeDocument,
            startLocation,
            endLocation,
            sourceText,
            _languageServerFeatureOptions.SupportsFileManipulation,
            supportsCodeActionResolve);

        return context;
    }

    private async Task<ImmutableArray<RazorVSInternalCodeAction>> GetDelegatedCodeActionsAsync(RazorCodeActionContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        var languageKind = context.CodeDocument.GetLanguageKind(context.StartAbsoluteIndex, rightAssociative: false);

        // No point delegating if we're in a Razor context
        if (languageKind == RazorLanguageKind.Razor)
        {
            return [];
        }

        var codeActions = await GetCodeActionsFromLanguageServerAsync(languageKind, context, correlationId, cancellationToken).ConfigureAwait(false);
        if (codeActions is not [_, ..])
        {
            return [];
        }

        IEnumerable<ICodeActionProvider> providers;
        if (languageKind == RazorLanguageKind.CSharp)
        {
            codeActions = ExtractCSharpCodeActionNamesFromData(codeActions);
            providers = _csharpCodeActionProviders;
        }
        else
        {
            providers = _htmlCodeActionProviders;
        }

        return await FilterCodeActionsAsync(context, codeActions.ToImmutableArray(), providers, cancellationToken).ConfigureAwait(false);
    }

    private RazorVSInternalCodeAction[] ExtractCSharpCodeActionNamesFromData(RazorVSInternalCodeAction[] codeActions)
    {
        using var actions = new PooledArrayBuilder<RazorVSInternalCodeAction>();

        foreach (var codeAction in codeActions)
        {
            if (codeAction.Data is not JsonElement jsonData ||
                !jsonData.TryGetProperty("CustomTags", out var value) ||
                value.Deserialize<string[]>() is not [..] tags)
            {
                continue;
            }

            foreach (var tag in tags)
            {
                if (s_allAvailableCodeActionNames.Contains(tag))
                {
                    codeAction.Name = tag;
                    break;
                }
            }

            if (string.IsNullOrEmpty(codeAction.Name))
            {
                continue;
            }

            actions.Add(codeAction);
        }

        return actions.ToArray();
    }

    private static async Task<ImmutableArray<RazorVSInternalCodeAction>> FilterCodeActionsAsync(
        RazorCodeActionContext context,
        ImmutableArray<RazorVSInternalCodeAction> codeActions,
        IEnumerable<ICodeActionProvider> providers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var tasks = new PooledArrayBuilder<Task<ImmutableArray<RazorVSInternalCodeAction>>>();
        foreach (var provider in providers)
        {
            tasks.Add(provider.ProvideAsync(context, codeActions, cancellationToken));
        }

        return await ConsolidateCodeActionsFromProvidersAsync(tasks.ToImmutable(), cancellationToken).ConfigureAwait(false);
    }

    private Task<RazorVSInternalCodeAction[]> GetCodeActionsFromLanguageServerAsync(RazorLanguageKind languageKind, RazorCodeActionContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        if (languageKind == RazorLanguageKind.CSharp)
        {
            // For C# we have to map the ranges to the generated document
            if (!_documentMappingService.TryMapToGeneratedDocumentRange(context.CodeDocument.GetCSharpDocument(), context.Request.Range, out var projectedRange))
            {
                return SpecializedTasks.EmptyArray<RazorVSInternalCodeAction>();
            }

            var newContext = context.Request.Context;
            if (context.Request.Context is VSInternalCodeActionContext { SelectionRange: not null } vsContext &&
                _documentMappingService.TryMapToGeneratedDocumentRange(context.CodeDocument.GetCSharpDocument(), vsContext.SelectionRange, out var selectionRange))
            {
                vsContext.SelectionRange = selectionRange;
                newContext = vsContext;
            }

            context.Request.Range = projectedRange;
            context.Request.Context = newContext;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _delegatedCodeActionsProvider.GetDelegatedCodeActionsAsync(languageKind, context.Request, context.DocumentSnapshot.Version, correlationId, cancellationToken);
    }

    private async Task<ImmutableArray<RazorVSInternalCodeAction>> GetRazorCodeActionsAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var tasks = new PooledArrayBuilder<Task<ImmutableArray<RazorVSInternalCodeAction>>>();

        foreach (var provider in _razorCodeActionProviders)
        {
            tasks.Add(provider.ProvideAsync(context, cancellationToken));
        }

        return await ConsolidateCodeActionsFromProvidersAsync(tasks.ToImmutable(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ImmutableArray<RazorVSInternalCodeAction>> ConsolidateCodeActionsFromProvidersAsync(
        ImmutableArray<Task<ImmutableArray<RazorVSInternalCodeAction>>> tasks,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        using var codeActions = new PooledArrayBuilder<RazorVSInternalCodeAction>();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var result in results)
        {
            codeActions.AddRange(result);
        }

        return codeActions.ToImmutable();
    }

    private static ImmutableHashSet<string> GetAllAvailableCodeActionNames()
    {
        using var _ = ArrayBuilderPool<string>.GetPooledObject(out var availableCodeActionNames);

        var refactoringProviderNames = typeof(RazorPredefinedCodeRefactoringProviderNames)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .WhereNotNull();
        var codeFixProviderNames = typeof(RazorPredefinedCodeFixProviderNames)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .WhereNotNull();

        availableCodeActionNames.AddRange(refactoringProviderNames);
        availableCodeActionNames.AddRange(codeFixProviderNames);
        availableCodeActionNames.Add(LanguageServerConstants.CodeActions.CodeActionFromVSCode);

        return availableCodeActionNames.ToImmutableHashSet();
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CodeActionsService instance)
    {
        public Task<RazorCodeActionContext?> GenerateRazorCodeActionContextAsync(VSCodeActionParams request, IDocumentSnapshot documentSnapshot, bool supportsCodeActionResolve, CancellationToken cancellationToken)
            => instance.GenerateRazorCodeActionContextAsync(request, documentSnapshot, supportsCodeActionResolve, cancellationToken);

        public Task<RazorVSInternalCodeAction[]> GetCodeActionsFromLanguageServerAsync(RazorLanguageKind languageKind, RazorCodeActionContext context, Guid correlationId, CancellationToken cancellationToken)
            => instance.GetCodeActionsFromLanguageServerAsync(languageKind, context, correlationId, cancellationToken);
    }
}
