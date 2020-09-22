// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionResolutionEndpoint : IRazorCodeActionResolveHandler
    {
        private static readonly string CodeActionsResolveProviderCapability = "codeActionsResolveProvider";

        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly IReadOnlyDictionary<string, RazorCodeActionResolver> _razorCodeActionResolvers;
        private readonly IReadOnlyDictionary<string, CSharpCodeActionResolver> _csharpCodeActionResolvers;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;
        private readonly IClientLanguageServer _languageServer;
        private readonly CSharpFormatter _csharpFormatter;

        public CodeActionResolutionEndpoint(
            RazorDocumentMappingService documentMappingService,
            IEnumerable<RazorCodeActionResolver> razorCodeActionResolvers,
            IEnumerable<CSharpCodeActionResolver> csharpCodeActionResolvers,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory,
            IClientLanguageServer languageServer,
            FilePathNormalizer filePathNormalizer,
            ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor)
        {
            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (razorCodeActionResolvers is null)
            {
                throw new ArgumentNullException(nameof(razorCodeActionResolvers));
            }

            if (csharpCodeActionResolvers is null)
            {
                throw new ArgumentNullException(nameof(csharpCodeActionResolvers));
            }

            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (filePathNormalizer is null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            if (projectSnapshotManagerAccessor is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));
            }

            _documentMappingService = documentMappingService;
            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<CodeActionResolutionEndpoint>();
            _languageServer = languageServer;


            var razorResolverMap = new Dictionary<string, RazorCodeActionResolver>();
            foreach (var resolver in razorCodeActionResolvers)
            {
                if (razorResolverMap.ContainsKey(resolver.Action))
                {
                    Debug.Fail($"Duplicate resolver action for {resolver.Action}.");
                }
                razorResolverMap[resolver.Action] = resolver;
            }
            _razorCodeActionResolvers = razorResolverMap;


            var csharpResolverMap = new Dictionary<string, CSharpCodeActionResolver>();
            foreach (var resolver in csharpCodeActionResolvers)
            {
                if (csharpResolverMap.ContainsKey(resolver.Action))
                {
                    Debug.Fail($"Duplicate resolver action for {resolver.Action}.");
                }
                csharpResolverMap[resolver.Action] = resolver;
            }
            _csharpCodeActionResolvers = csharpResolverMap;


            _csharpFormatter = new CSharpFormatter(documentMappingService, languageServer, projectSnapshotManagerAccessor, filePathNormalizer);
        }

        // Register VS LSP code action resolution server capability
        public RegistrationExtensionResult GetRegistration() => new RegistrationExtensionResult(CodeActionsResolveProviderCapability, true);

        public async Task<RazorCodeAction> Handle(RazorCodeAction request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!(request.Data is JObject paramsObj))
            {
                Debug.Fail($"Invalid CodeAction Received '{request.Title}'.");
                return request;
            }

            var resolutionParams = paramsObj.ToObject<RazorCodeActionResolutionParams>();

            _logger.LogInformation($"Resolving workspace edit for action {GetCodeActionId(resolutionParams)}.");

            switch (resolutionParams.Language)
            {
                case LanguageServerConstants.Languages.Razor:
                    return await ResolveRazorCodeAction(
                        request,
                        resolutionParams,
                        cancellationToken).ConfigureAwait(false);
                case LanguageServerConstants.Languages.CSharp:
                    return await ResolveCSharpCodeAction(
                        request,
                        resolutionParams,
                        cancellationToken);
                default:
                    Debug.Fail($"Invalid CodeAction.Data.Language. Received {GetCodeActionId(resolutionParams)}.");
                    return request;
            }
        }

        // Internal for testing
        internal async Task<RazorCodeAction> ResolveRazorCodeAction(
            RazorCodeAction request,
            RazorCodeActionResolutionParams resolutionParams,
            CancellationToken cancellationToken)
        {
            if (!_razorCodeActionResolvers.TryGetValue(resolutionParams.Action, out var resolver))
            {
                Debug.Fail($"No resolver registered for {GetCodeActionId(resolutionParams)}.");
                return null;
            }

            request.Edit = await resolver.ResolveAsync(resolutionParams.Data as JObject, cancellationToken).ConfigureAwait(false);
            return request;
        }

        // Internal for testing
        internal async Task<RazorCodeAction> ResolveCSharpCodeAction(
            RazorCodeAction codeAction,
            RazorCodeActionResolutionParams resolutionParams,
            CancellationToken cancellationToken)
        {
            if (!(resolutionParams.Data is JObject csharpParamsObj))
            {
                Debug.Fail($"Invalid CSharp CodeAction Received.");
                return null;
            }

            var csharpParams = csharpParamsObj.ToObject<CSharpCodeActionParams>();
            codeAction.Data = csharpParams.Data;

            if (!_csharpCodeActionResolvers.TryGetValue(resolutionParams.Action, out var resolver))
            {
                Debug.Fail($"No resolver registered for {GetCodeActionId(resolutionParams)}.");
                return codeAction;
            }

            var resolvedCodeAction = await resolver.ResolveAsync(csharpParams, codeAction, cancellationToken);
            return resolvedCodeAction;
        }

        private static string GetCodeActionId(RazorCodeActionResolutionParams resolutionParams) =>
            $"`{resolutionParams.Language}.{resolutionParams.Action}`";
    }
}
