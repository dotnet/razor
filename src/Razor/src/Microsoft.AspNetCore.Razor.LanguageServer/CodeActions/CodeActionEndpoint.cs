﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

[LanguageServerEndpoint(LspEndpointName)]
internal sealed class CodeActionEndpoint : IRazorRequestHandler<VSCodeActionParams, SumType<Command, CodeAction>[]?>, ICapabilitiesProvider
{
    public const string LspEndpointName = Methods.TextDocumentCodeActionName;
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly IEnumerable<IRazorCodeActionProvider> _razorCodeActionProviders;
    private readonly IEnumerable<ICSharpCodeActionProvider> _csharpCodeActionProviders;
    private readonly IEnumerable<IHtmlCodeActionProvider> _htmlCodeActionProviders;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly ITelemetryReporter? _telemetryReporter;

    internal bool _supportsCodeActionResolve = false;

    private readonly ImmutableHashSet<string> _allAvailableCodeActionNames;

    public bool MutatesSolutionState { get; } = false;

    public CodeActionEndpoint(
        IRazorDocumentMappingService documentMappingService,
        IEnumerable<IRazorCodeActionProvider> razorCodeActionProviders,
        IEnumerable<ICSharpCodeActionProvider> csharpCodeActionProviders,
        IEnumerable<IHtmlCodeActionProvider> htmlCodeActionProviders,
        ClientNotifierServiceBase languageServer,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        ITelemetryReporter? telemetryReporter)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _razorCodeActionProviders = razorCodeActionProviders ?? throw new ArgumentNullException(nameof(razorCodeActionProviders));
        _csharpCodeActionProviders = csharpCodeActionProviders ?? throw new ArgumentNullException(nameof(csharpCodeActionProviders));
        _htmlCodeActionProviders = htmlCodeActionProviders ?? throw new ArgumentNullException(nameof(htmlCodeActionProviders));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        _telemetryReporter = telemetryReporter;

        _allAvailableCodeActionNames = GetAllAvailableCodeActionNames();
    }

    public async Task<SumType<Command, CodeAction>[]?> HandleRequestAsync(VSCodeActionParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

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

        var delegatedCodeActions = await GetDelegatedCodeActionsAsync(documentContext, razorCodeActionContext, correlationId, requestContext.Logger, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        using var _ = ArrayBuilderPool<SumType<Command, CodeAction>>.GetPooledObject(out var commandsOrCodeActions);

        ConvertCodeActionsToSumType(razorCodeActions);
        ConvertCodeActionsToSumType(delegatedCodeActions);

        return commandsOrCodeActions.ToArray();

        void ConvertCodeActionsToSumType(ImmutableArray<RazorVSInternalCodeAction> codeActions)
        {
            // We must cast the RazorCodeAction into a platform compliant code action
            // For VS (SupportsCodeActionResolve = true) this means just encapsulating the RazorCodeAction in the `CommandOrCodeAction` struct
            // For VS Code (SupportsCodeActionResolve = false) we must convert it into a CodeAction or Command before encapsulating in the `CommandOrCodeAction` struct.
            if (_supportsCodeActionResolve)
            {
                foreach (var action in codeActions)
                {
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
            CodeActionKinds = new[]
            {
                CodeActionKind.RefactorExtract,
                CodeActionKind.QuickFix,
                CodeActionKind.Refactor
            },
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
        var vsCodeActionContext = (VSInternalCodeActionContext)request.Context;
        if (vsCodeActionContext.SelectionRange != null)
        {
            request.Range = vsCodeActionContext.SelectionRange;
        }

        var linePosition = new LinePosition(
            request.Range.Start.Line,
            request.Range.Start.Character);
        var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
        var location = new SourceLocation(
            hostDocumentIndex,
            request.Range.Start.Line,
            request.Range.Start.Character);

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

    private async Task<ImmutableArray<RazorVSInternalCodeAction>> GetDelegatedCodeActionsAsync(VersionedDocumentContext documentContext, RazorCodeActionContext context, Guid correlationId, IRazorLogger logger, CancellationToken cancellationToken)
    {
        var languageKind = _documentMappingService.GetLanguageKind(context.CodeDocument, context.Location.AbsoluteIndex, rightAssociative: false);

        // No point delegating if we're in a Razor context
        if (languageKind == RazorLanguageKind.Razor)
        {
            return ImmutableArray<RazorVSInternalCodeAction>.Empty;
        }

        var codeActions = await GetCodeActionsFromLanguageServerAsync(languageKind, documentContext, context, correlationId, logger, cancellationToken).ConfigureAwait(false);
        if (codeActions is not [_, ..])
        {
            return ImmutableArray<RazorVSInternalCodeAction>.Empty;
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

        return await FilterCodeActionsAsync(context, codeActions, providers, cancellationToken).ConfigureAwait(false);
    }

    private RazorVSInternalCodeAction[] ExtractCSharpCodeActionNamesFromData(RazorVSInternalCodeAction[] codeActions)
    {
        using var _ = ArrayBuilderPool<RazorVSInternalCodeAction>.GetPooledObject(out var actions);

        foreach (var codeAction in codeActions)
        {
            // Note: we may see a perf benefit from using a JsonConverter
            var tags = ((JToken?)codeAction.Data)?["CustomTags"]?.ToObject<string[]>();
            if (tags is null || tags.Length == 0)
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
        RazorVSInternalCodeAction[] codeActions,
        IEnumerable<ICodeActionProvider> providers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var _ = ArrayBuilderPool<Task<IReadOnlyList<RazorVSInternalCodeAction>?>>.GetPooledObject(out var tasks);

        foreach (var provider in providers)
        {
            tasks.Add(provider.ProvideAsync(context, codeActions, cancellationToken));
        }

        return await ConsolidateCodeActionsFromProvidersAsync(tasks.ToImmutableArray(), cancellationToken).ConfigureAwait(false);
    }

    // Internal for testing
    internal async Task<RazorVSInternalCodeAction[]> GetCodeActionsFromLanguageServerAsync(RazorLanguageKind languageKind, VersionedDocumentContext documentContext, RazorCodeActionContext context, Guid correlationId, IRazorLogger logger, CancellationToken cancellationToken)
    {
        if (languageKind == RazorLanguageKind.CSharp)
        {
            // For C# we have to map the ranges to the generated document
            if (!_documentMappingService.TryMapToGeneratedDocumentRange(context.CodeDocument.GetCSharpDocument(), context.Request.Range, out var projectedRange))
            {
                return Array.Empty<RazorVSInternalCodeAction>();
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
            HostDocumentVersion = documentContext.Version,
            CodeActionParams = context.Request,
            LanguageKind = languageKind,
            CorrelationId = correlationId
        };

        try
        {
            return await _languageServer.SendRequestAsync<DelegatedCodeActionParams, RazorVSInternalCodeAction[]>(CustomMessageNames.RazorProvideCodeActionsEndpoint, delegatedParams, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException e)
        {
            logger.LogException(e, "Error getting code actions from delegate language server for {languageKind}", languageKind);
            return Array.Empty<RazorVSInternalCodeAction>();
        }
    }

    private async Task<ImmutableArray<RazorVSInternalCodeAction>> GetRazorCodeActionsAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var _ = ArrayBuilderPool<Task<IReadOnlyList<RazorVSInternalCodeAction>?>>.GetPooledObject(out var tasks);

        foreach (var provider in _razorCodeActionProviders)
        {
            tasks.Add(provider.ProvideAsync(context, cancellationToken));
        }

        return await ConsolidateCodeActionsFromProvidersAsync(tasks.ToImmutableArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ImmutableArray<RazorVSInternalCodeAction>> ConsolidateCodeActionsFromProvidersAsync(
        ImmutableArray<Task<IReadOnlyList<RazorVSInternalCodeAction>?>> tasks,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        using var _ = ArrayBuilderPool<RazorVSInternalCodeAction>.GetPooledObject(out var codeActions);

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var result in results)
        {
            if (result is not null)
            {
                codeActions.AddRange(result);
            }
        }

        return codeActions.ToImmutableArray();
    }

    private static ImmutableHashSet<string> GetAllAvailableCodeActionNames()
    {
        using var _ = ArrayBuilderPool<string>.GetPooledObject(out var availableCodeActionNames);

        var refactoringProviderNames = typeof(RazorPredefinedCodeRefactoringProviderNames)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .WithoutNull();
        var codeFixProviderNames = typeof(RazorPredefinedCodeFixProviderNames)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .WithoutNull();

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
