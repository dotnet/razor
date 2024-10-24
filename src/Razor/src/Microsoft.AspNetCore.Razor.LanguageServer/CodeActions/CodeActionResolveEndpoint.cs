// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

[RazorLanguageServerEndpoint(Methods.CodeActionResolveName)]
internal sealed class CodeActionResolveEndpoint(
    IEnumerable<IRazorCodeActionResolver> razorCodeActionResolvers,
    IEnumerable<ICSharpCodeActionResolver> csharpCodeActionResolvers,
    IEnumerable<IHtmlCodeActionResolver> htmlCodeActionResolvers,
    ILoggerFactory loggerFactory) : IRazorRequestHandler<CodeAction, CodeAction>
{
    private readonly FrozenDictionary<string, IRazorCodeActionResolver> _razorCodeActionResolvers = CreateResolverMap(razorCodeActionResolvers);
    private readonly FrozenDictionary<string, ICSharpCodeActionResolver> _csharpCodeActionResolvers = CreateResolverMap(csharpCodeActionResolvers);
    private readonly FrozenDictionary<string, IHtmlCodeActionResolver> _htmlCodeActionResolvers = CreateResolverMap(htmlCodeActionResolvers);
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CodeActionResolveEndpoint>();

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(CodeAction request)
        => GetRazorCodeActionResolutionParams(request).TextDocument;

    public async Task<CodeAction> HandleRequestAsync(CodeAction request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var resolutionParams = GetRazorCodeActionResolutionParams(request);
        var documentContext = requestContext.DocumentContext.AssumeNotNull();

        var codeActionId = GetCodeActionId(resolutionParams);
        _logger.LogDebug($"Resolving workspace edit for action {codeActionId}.");

        // If it's a special "edit based code action" then the edit has been pre-computed and we
        // can extract the edit details and return to the client. This is only required for VSCode
        // as it does not support Command.Edit based code actions anymore.
        if (resolutionParams.Action == LanguageServerConstants.CodeActions.EditBasedCodeActionCommand)
        {
            request.Edit = (resolutionParams.Data as JsonElement?)?.Deserialize<WorkspaceEdit>();
            return request;
        }

        request.Data = resolutionParams.Data;

        switch (resolutionParams.Language)
        {
            case RazorLanguageKind.Razor:
                return await ResolveRazorCodeActionAsync(
                    documentContext,
                    request,
                    resolutionParams,
                    cancellationToken).ConfigureAwait(false);
            case RazorLanguageKind.CSharp:
                return await ResolveCSharpCodeActionAsync(
                    documentContext,
                    request,
                    resolutionParams,
                    cancellationToken).ConfigureAwait(false);
            case RazorLanguageKind.Html:
                return await ResolveHtmlCodeActionAsync(
                    documentContext,
                    request,
                    resolutionParams,
                    cancellationToken).ConfigureAwait(false);
            default:
                _logger.LogError($"Invalid CodeAction.Data.Language. Received {codeActionId}.");
                return request;
        }
    }

    private static RazorCodeActionResolutionParams GetRazorCodeActionResolutionParams(CodeAction request)
    {
        if (request.Data is not JsonElement paramsObj)
        {
            throw new InvalidOperationException($"Invalid CodeAction Received '{request.Title}'.");
        }

        var resolutionParams = paramsObj.Deserialize<RazorCodeActionResolutionParams>();
        if (resolutionParams is null)
        {
            throw new InvalidOperationException($"request.Data should be convertible to {nameof(RazorCodeActionResolutionParams)}");
        }

        return resolutionParams;
    }

    private async Task<CodeAction> ResolveRazorCodeActionAsync(
        DocumentContext documentContext,
        CodeAction codeAction,
        RazorCodeActionResolutionParams resolutionParams,
        CancellationToken cancellationToken)
    {
        if (!_razorCodeActionResolvers.TryGetValue(resolutionParams.Action, out var resolver))
        {
            var codeActionId = GetCodeActionId(resolutionParams);
            _logger.LogWarning($"No resolver registered for {codeActionId}");
            Debug.Fail($"No resolver registered for {codeActionId}.");
            return codeAction;
        }

        if (resolutionParams.Data is not JsonElement data)
        {
            return codeAction;
        }

        var edit = await resolver.ResolveAsync(documentContext, data, cancellationToken).ConfigureAwait(false);
        codeAction.Edit = edit;
        return codeAction;
    }

    private async Task<CodeAction> ResolveCSharpCodeActionAsync(DocumentContext documentContext, CodeAction codeAction, RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
    {
        if (TryGetResolver(resolutionParams, _csharpCodeActionResolvers, out var resolver))
        {
            return await resolver.ResolveAsync(documentContext, codeAction, cancellationToken).ConfigureAwait(false);
        }

        return codeAction;
    }

    private async Task<CodeAction> ResolveHtmlCodeActionAsync(DocumentContext documentContext, CodeAction codeAction, RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
    {
        if (TryGetResolver(resolutionParams, _htmlCodeActionResolvers, out var resolver))
        {
            return await resolver.ResolveAsync(documentContext, codeAction, cancellationToken).ConfigureAwait(false);
        }

        return codeAction;
    }

    private bool TryGetResolver<TResolver>(RazorCodeActionResolutionParams resolutionParams, FrozenDictionary<string, TResolver> resolvers, [NotNullWhen(true)] out TResolver? resolver)
          where TResolver : ICodeActionResolver
    {
        if (!resolvers.TryGetValue(resolutionParams.Action, out resolver))
        {
            var codeActionId = GetCodeActionId(resolutionParams);
            _logger.LogWarning($"No resolver registered for {codeActionId}");
            Debug.Fail($"No resolver registered for {codeActionId}.");
            return false;
        }

        return resolver is not null;
    }

    private static FrozenDictionary<string, T> CreateResolverMap<T>(IEnumerable<T> codeActionResolvers)
        where T : ICodeActionResolver
    {
        using var _ = StringDictionaryPool<T>.GetPooledObject(out var resolverMap);

        foreach (var resolver in codeActionResolvers)
        {
            if (resolverMap.ContainsKey(resolver.Action))
            {
                Debug.Fail($"Duplicate resolver action for {resolver.Action} of type {typeof(T)}.");
            }

            resolverMap[resolver.Action] = resolver;
        }

        return resolverMap.ToFrozenDictionary();
    }

    private static string GetCodeActionId(RazorCodeActionResolutionParams resolutionParams) =>
        $"`{resolutionParams.Language}.{resolutionParams.Action}`";

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CodeActionResolveEndpoint instance)
    {
        public Task<CodeAction> ResolveRazorCodeActionAsync(DocumentContext documentContext, CodeAction codeAction, RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
            => instance.ResolveRazorCodeActionAsync(documentContext, codeAction, resolutionParams, cancellationToken);

        public Task<CodeAction> ResolveCSharpCodeActionAsync(DocumentContext documentContext, CodeAction codeAction, RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
            => instance.ResolveCSharpCodeActionAsync(documentContext, codeAction, resolutionParams, cancellationToken);

        public Task<CodeAction> ResolveHtmlCodeActionAsync(DocumentContext documentContext, CodeAction codeAction, RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
            => instance.ResolveCSharpCodeActionAsync(documentContext, codeAction, resolutionParams, cancellationToken);
    }
}
