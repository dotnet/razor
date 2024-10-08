﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

[RazorLanguageServerEndpoint(LspEndpointName)]
internal sealed class CodeActionEndpoint(
    IDocumentMappingService documentMappingService,
    IEnumerable<IRazorCodeActionProvider> razorCodeActionProviders,
    IEnumerable<ICSharpCodeActionProvider> csharpCodeActionProviders,
    IEnumerable<IHtmlCodeActionProvider> htmlCodeActionProviders,
    IClientConnection clientConnection,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    ILoggerFactory loggerFactory,
    ITelemetryReporter? telemetryReporter)
    : IRazorRequestHandler<VSCodeActionParams, SumType<Command, CodeAction>[]?>, ICapabilitiesProvider
{
    private const string LspEndpointName = Methods.TextDocumentCodeActionName;

    private readonly IDocumentMappingService _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
    private readonly IEnumerable<IRazorCodeActionProvider> _razorCodeActionProviders = razorCodeActionProviders ?? throw new ArgumentNullException(nameof(razorCodeActionProviders));
    private readonly IEnumerable<ICSharpCodeActionProvider> _csharpCodeActionProviders = csharpCodeActionProviders ?? throw new ArgumentNullException(nameof(csharpCodeActionProviders));
    private readonly IEnumerable<IHtmlCodeActionProvider> _htmlCodeActionProviders = htmlCodeActionProviders ?? throw new ArgumentNullException(nameof(htmlCodeActionProviders));
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
    private readonly IClientConnection _clientConnection = clientConnection ?? throw new ArgumentNullException(nameof(clientConnection));
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CodeActionEndpoint>();
    private readonly ITelemetryReporter? _telemetryReporter = telemetryReporter;

    internal bool _supportsCodeActionResolve = false;

    private readonly ImmutableHashSet<string> _allAvailableCodeActionNames = GetAllAvailableCodeActionNames();

    public bool MutatesSolutionState { get; } = false;

    public async Task<SumType<Command, CodeAction>[]?> HandleRequestAsync(VSCodeActionParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        var razorCodeActionContext = await GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot).ConfigureAwait(false);
        if (razorCodeActionContext is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var correlationId = Guid.NewGuid();
        using var __ = _telemetryReporter?.TrackLspRequest(LspEndpointName, LanguageServerConstants.RazorLanguageServerName, correlationId);
        var razorCodeActions = await GetRazorCodeActionsAsync(razorCodeActionContext, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var delegatedCodeActions = await GetDelegatedCodeActionsAsync(documentContext, razorCodeActionContext, correlationId, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        using var commandsOrCodeActions = new PooledArrayBuilder<SumType<Command, CodeAction>>();

        // Grouping the code actions causes VS to sort them into groups, rather than just alphabetically sorting them
        // by title. The latter is bad for us because it can put "Remove <div>" at the top in some locales, and our fully
        // qualify component code action at the bottom, depending on the users namespace.
        ConvertCodeActionsToSumType(razorCodeActions, "A-Razor");
        ConvertCodeActionsToSumType(delegatedCodeActions, "B-Delegated");

        return commandsOrCodeActions.ToArray();

        void ConvertCodeActionsToSumType(ImmutableArray<RazorVSInternalCodeAction> codeActions, string groupName)
        {
            // We must cast the RazorCodeAction into a platform compliant code action
            // For VS (SupportsCodeActionResolve = true) this means just encapsulating the RazorCodeAction in the `CommandOrCodeAction` struct
            // For VS Code (SupportsCodeActionResolve = false) we must convert it into a CodeAction or Command before encapsulating in the `CommandOrCodeAction` struct.
            if (_supportsCodeActionResolve)
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
                    commandsOrCodeActions.Add(action.AsVSCodeCommandOrCodeAction());
                }
            }
        }
    }

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        _supportsCodeActionResolve = clientCapabilities.TextDocument?.CodeAction?.ResolveSupport is not null;

        serverCapabilities.CodeActionProvider = new CodeActionOptions
        {
            CodeActionKinds =
            [
                CodeActionKind.RefactorExtract,
                CodeActionKind.QuickFix,
                CodeActionKind.Refactor
            ],
            ResolveProvider = true,
        };
    }

    // internal for testing
    internal async Task<RazorCodeActionContext?> GenerateRazorCodeActionContextAsync(VSCodeActionParams request, IDocumentSnapshot documentSnapshot)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);

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

        if (!sourceText.TryGetSourceLocation(request.Range.Start, out var location))
        {
            return null;
        }

        var context = new RazorCodeActionContext(
            request,
            documentSnapshot,
            codeDocument,
            location,
            sourceText,
            _languageServerFeatureOptions.SupportsFileManipulation,
            _supportsCodeActionResolve);

        return context;
    }

    private async Task<ImmutableArray<RazorVSInternalCodeAction>> GetDelegatedCodeActionsAsync(DocumentContext documentContext, RazorCodeActionContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        var languageKind = context.CodeDocument.GetLanguageKind(context.Location.AbsoluteIndex, rightAssociative: false);

        // No point delegating if we're in a Razor context
        if (languageKind == RazorLanguageKind.Razor)
        {
            return [];
        }

        var codeActions = await GetCodeActionsFromLanguageServerAsync(languageKind, documentContext, context, correlationId, cancellationToken).ConfigureAwait(false);
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
                if (_allAvailableCodeActionNames.Contains(tag))
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

    // Internal for testing
    internal async Task<RazorVSInternalCodeAction[]> GetCodeActionsFromLanguageServerAsync(RazorLanguageKind languageKind, DocumentContext documentContext, RazorCodeActionContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        if (languageKind == RazorLanguageKind.CSharp)
        {
            // For C# we have to map the ranges to the generated document
            if (!_documentMappingService.TryMapToGeneratedDocumentRange(context.CodeDocument.GetCSharpDocument(), context.Request.Range, out var projectedRange))
            {
                return [];
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

        var delegatedParams = new DelegatedCodeActionParams()
        {
            HostDocumentVersion = documentContext.Snapshot.Version,
            CodeActionParams = context.Request,
            LanguageKind = languageKind,
            CorrelationId = correlationId
        };

        try
        {
            return await _clientConnection.SendRequestAsync<DelegatedCodeActionParams, RazorVSInternalCodeAction[]>(CustomMessageNames.RazorProvideCodeActionsEndpoint, delegatedParams, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException e)
        {
            _telemetryReporter?.ReportFault(e, "Error getting code actions from delegate language server for {languageKind}", languageKind);
            _logger.LogError(e, $"Error getting code actions from delegate language server for {languageKind}");
            return [];
        }
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

    public TextDocumentIdentifier GetTextDocumentIdentifier(VSCodeActionParams request)
    {
        return request.TextDocument;
    }
}
