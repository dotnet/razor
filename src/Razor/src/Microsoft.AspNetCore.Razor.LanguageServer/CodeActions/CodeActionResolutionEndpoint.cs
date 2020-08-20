// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionResolutionEndpoint : IRazorCodeActionResolutionHandler, ILSPCodeActionResolverHandler
    {
        private static readonly string AssociatedServerCapability = "codeActionsResolveProvider";

        private readonly IReadOnlyDictionary<string, RazorCodeActionResolver> _resolvers;
        private readonly ILogger _logger;

        public CodeActionResolutionEndpoint(
            IEnumerable<RazorCodeActionResolver> resolvers,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<CodeActionResolutionEndpoint>();

            if (resolvers is null)
            {
                throw new ArgumentNullException(nameof(resolvers));
            }

            var resolverMap = new Dictionary<string, RazorCodeActionResolver>();
            foreach (var resolver in resolvers)
            {
                if (resolverMap.ContainsKey(resolver.Action))
                {
                    Debug.Fail($"Duplicate resolver action for {resolver.Action}.");
                }
                resolverMap[resolver.Action] = resolver;
            }
            _resolvers = resolverMap;
        }

        // Register VS LSP code action resolution server capability
        public RegistrationExtensionResult GetRegistration() => new RegistrationExtensionResult(AssociatedServerCapability, true);

        // Handle the Razor VSCode `razor/resolveCodeAction` command
        public async Task<RazorCodeActionResolutionResponse> Handle(RazorCodeActionResolutionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogDebug($"Resolving action {request.Action} with data {request.Data}.");

            if (!_resolvers.TryGetValue(request.Action, out var resolver))
            {
                Debug.Fail($"No resolver registered for {request.Action}.");
                return new RazorCodeActionResolutionResponse();
            }

            var edit = await resolver.ResolveAsync(request.Data, cancellationToken).ConfigureAwait(false);
            return new RazorCodeActionResolutionResponse() { Edit = edit };
        }

        // Handle the VS LSP `textDocument/codeActionResolve` endpoint
        public async Task<RazorCodeAction> Handle(RazorCodeAction request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!(request.Data is JObject paramsObj))
            {
                Debug.Fail($"Invalid CodeAction Received {request.Title}.");
                return request;
            }

            var resolutionParams = paramsObj.ToObject<RazorCodeActionResolutionParams>();
            _logger.LogDebug($"Resolving action {resolutionParams.Action} with data {resolutionParams.Data}.");

            if (!_resolvers.TryGetValue(resolutionParams.Action, out var resolver))
            {
                Debug.Fail($"No resolver registered for {resolutionParams.Action}.");
                return request;
            }

            request.Edit = await resolver.ResolveAsync(resolutionParams.Data, cancellationToken).ConfigureAwait(false);
            return request;
        }
    }
}
